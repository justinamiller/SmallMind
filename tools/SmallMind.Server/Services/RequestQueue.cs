namespace SmallMind.Server.Services;

public sealed class RequestQueue : IDisposable
{
    private readonly SemaphoreSlim _concurrencySemaphore;
    private readonly SemaphoreSlim _queueSemaphore;
    private int _inflightCount;
    private bool _disposed;

    public RequestQueue(int maxConcurrentRequests, int maxQueueDepth)
    {
        if (maxConcurrentRequests <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxConcurrentRequests));
        if (maxQueueDepth <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxQueueDepth));

        _concurrencySemaphore = new SemaphoreSlim(maxConcurrentRequests, maxConcurrentRequests);
        _queueSemaphore = new SemaphoreSlim(maxQueueDepth, maxQueueDepth);
    }

    public int InflightCount => _inflightCount;

    public async Task<QueueSlot> EnqueueAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(RequestQueue));

        if (!await _queueSemaphore.WaitAsync(0, cancellationToken))
        {
            return new QueueSlot(null, null, null, false);
        }

        try
        {
            await _concurrencySemaphore.WaitAsync(cancellationToken);
            Interlocked.Increment(ref _inflightCount);
            return new QueueSlot(this, _concurrencySemaphore, _queueSemaphore, true);
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
    }

    public readonly struct QueueSlot : IDisposable
    {
        private readonly RequestQueue? _queue;
        private readonly SemaphoreSlim? _concurrencySemaphore;
        private readonly SemaphoreSlim? _queueSemaphore;

        public bool Success { get; }

        internal QueueSlot(RequestQueue? queue, SemaphoreSlim? concurrencySemaphore, SemaphoreSlim? queueSemaphore, bool success)
        {
            _queue = queue;
            _concurrencySemaphore = concurrencySemaphore;
            _queueSemaphore = queueSemaphore;
            Success = success;
        }

        public void Dispose()
        {
            if (_concurrencySemaphore != null && _queueSemaphore != null && _queue != null)
            {
                Interlocked.Decrement(ref _queue._inflightCount);
                _concurrencySemaphore.Release();
                _queueSemaphore.Release();
            }
        }
    }
}
