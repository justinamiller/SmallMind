using System.Collections.Concurrent;

namespace SmallMind.Server.Services;

public sealed class RequestQueue : IDisposable
{
    private readonly SemaphoreSlim _concurrencySemaphore;
    private readonly SemaphoreSlim _queueSemaphore;
    private readonly ConcurrentDictionary<string, ClientRateLimiter> _clientRateLimiters;
    private readonly TimeSpan _defaultTimeout;
    private readonly int _maxRequestsPerClient;
    private int _inflightCount;
    private bool _disposed;

    public RequestQueue(int maxConcurrentRequests, int maxQueueDepth, TimeSpan? defaultTimeout = null, int maxRequestsPerClient = 10)
    {
        if (maxConcurrentRequests <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxConcurrentRequests));
        if (maxQueueDepth <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxQueueDepth));

        _concurrencySemaphore = new SemaphoreSlim(maxConcurrentRequests, maxConcurrentRequests);
        _queueSemaphore = new SemaphoreSlim(maxQueueDepth, maxQueueDepth);
        _clientRateLimiters = new ConcurrentDictionary<string, ClientRateLimiter>();
        _defaultTimeout = defaultTimeout ?? TimeSpan.FromSeconds(30);
        _maxRequestsPerClient = maxRequestsPerClient;
    }

    public int InflightCount => _inflightCount;

    public async Task<QueueSlot> EnqueueAsync(string? clientId = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(RequestQueue));

        // Per-client rate limiting
        if (clientId != null)
        {
            var limiter = _clientRateLimiters.GetOrAdd(clientId, _ => new ClientRateLimiter(_maxRequestsPerClient));
            if (!limiter.TryAcquire())
            {
                return new QueueSlot(null, null, null, null, false, "Client rate limit exceeded");
            }
        }

        // Try to acquire queue slot (non-blocking)
        if (!await _queueSemaphore.WaitAsync(0, cancellationToken))
        {
            return new QueueSlot(null, null, null, null, false, "Queue full");
        }

        try
        {
            // Wait for concurrency slot with timeout
            var effectiveTimeout = timeout ?? _defaultTimeout;
            using var timeoutCts = new CancellationTokenSource(effectiveTimeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            if (!await _concurrencySemaphore.WaitAsync(0, linkedCts.Token))
            {
                // Couldn't acquire immediately - wait with timeout
                try
                {
                    await _concurrencySemaphore.WaitAsync(linkedCts.Token);
                }
                catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
                {
                    _queueSemaphore.Release();
                    return new QueueSlot(null, null, null, clientId, false, "Queue timeout");
                }
            }

            Interlocked.Increment(ref _inflightCount);
            return new QueueSlot(this, _concurrencySemaphore, _queueSemaphore, clientId, true, null);
        }
        catch
        {
            _queueSemaphore.Release();
            throw;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        _concurrencySemaphore.Dispose();
        _queueSemaphore.Dispose();
        
        foreach (var limiter in _clientRateLimiters.Values)
        {
            limiter.Dispose();
        }
        _clientRateLimiters.Clear();
    }

    private sealed class ClientRateLimiter : IDisposable
    {
        private readonly int _maxRequests;
        private int _currentCount;
        private readonly Timer _resetTimer;
        private readonly object _lock = new();

        public ClientRateLimiter(int maxRequests)
        {
            _maxRequests = maxRequests;
            _currentCount = maxRequests;
            // Reset every 60 seconds
            _resetTimer = new Timer(_ => ResetLimiter(), null, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(60));
        }

        public bool TryAcquire()
        {
            lock (_lock)
            {
                if (_currentCount > 0)
                {
                    _currentCount--;
                    return true;
                }
                return false;
            }
        }

        public void Release()
        {
            lock (_lock)
            {
                if (_currentCount < _maxRequests)
                {
                    _currentCount++;
                }
            }
        }

        private void ResetLimiter()
        {
            lock (_lock)
            {
                _currentCount = _maxRequests;
            }
        }

        public void Dispose()
        {
            _resetTimer?.Dispose();
        }
    }

    public readonly struct QueueSlot : IDisposable
    {
        private readonly RequestQueue? _queue;
        private readonly SemaphoreSlim? _concurrencySemaphore;
        private readonly SemaphoreSlim? _queueSemaphore;
        private readonly string? _clientId;

        public bool Success { get; }
        public string? FailureReason { get; }

        internal QueueSlot(RequestQueue? queue, SemaphoreSlim? concurrencySemaphore, SemaphoreSlim? queueSemaphore, string? clientId, bool success, string? failureReason)
        {
            _queue = queue;
            _concurrencySemaphore = concurrencySemaphore;
            _queueSemaphore = queueSemaphore;
            _clientId = clientId;
            Success = success;
            FailureReason = failureReason;
        }

        public void Dispose()
        {
            if (_concurrencySemaphore != null && _queueSemaphore != null && _queue != null)
            {
                Interlocked.Decrement(ref _queue._inflightCount);
                _concurrencySemaphore.Release();
                _queueSemaphore.Release();

                // Release client rate limit
                if (_clientId != null && _queue._clientRateLimiters.TryGetValue(_clientId, out var limiter))
                {
                    limiter.Release();
                }
            }
        }
    }
}
