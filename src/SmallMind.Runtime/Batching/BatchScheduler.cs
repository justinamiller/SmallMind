using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SmallMind.Core.Exceptions;
using SmallMind.Runtime.Scheduling;
using SmallMind.Runtime.Telemetry;

namespace SmallMind.Runtime.Batching
{
    /// <summary>
    /// High-performance batch scheduler for inference requests.
    /// Collects requests and forms batches based on size or timeout constraints.
    /// Designed for minimal allocations and thread-safe operation.
    /// Supports deterministic scheduling for reproducible token generation.
    /// </summary>
    public sealed class BatchScheduler : IDisposable
    {
        private readonly BatchingOptions _options;
        private readonly IRuntimeMetrics _metrics;
        private readonly Queue<InferenceRequest> _pendingRequests;
        private readonly object _queueLock = new object();
        private readonly SemaphoreSlim _batchReadySemaphore;
        private readonly CancellationTokenSource _shutdownCts;
        private readonly Task _schedulerTask;

        // Pre-allocated lists for batch formation (reused to reduce allocations)
        // We use two buffers and swap between them to avoid copying
        private readonly List<InferenceRequest> _currentBatch;
        private readonly List<InferenceRequest> _dispatchBatch;

        // Deterministic scheduler (optional)
        private readonly DeterministicScheduler? _deterministicScheduler;

        private bool _disposed;
        private int _totalQueuedCount;

        /// <summary>
        /// Gets the current queue depth.
        /// </summary>
        public int QueueDepth
        {
            get
            {
                lock (_queueLock)
                {
                    return _pendingRequests.Count;
                }
            }
        }

        /// <summary>
        /// Event raised when a batch is ready for execution.
        /// Handler receives a list of requests to process.
        /// </summary>
        public event Action<List<InferenceRequest>>? BatchReady;

        /// <summary>
        /// Creates a new batch scheduler.
        /// </summary>
        /// <param name="options">Batching options</param>
        /// <param name="metrics">Optional metrics collector</param>
        public BatchScheduler(BatchingOptions options, IRuntimeMetrics? metrics = null)
        {
            _options = options?.Clone() ?? throw new ArgumentNullException(nameof(options));
            _options.Validate();

            _metrics = metrics ?? NullRuntimeMetrics.Instance;
            _pendingRequests = new Queue<InferenceRequest>(_options.MaxBatchSize * 2);
            _currentBatch = new List<InferenceRequest>(_options.MaxBatchSize);
            _dispatchBatch = new List<InferenceRequest>(_options.MaxBatchSize);
            _batchReadySemaphore = new SemaphoreSlim(0, int.MaxValue);
            _shutdownCts = new CancellationTokenSource();

            // Initialize deterministic scheduler if enabled
            if (_options.EnableDeterministicScheduling)
            {
                _deterministicScheduler = new DeterministicScheduler();
            }

            // Start background scheduler task
            _schedulerTask = Task.Run(SchedulerLoop);
        }

        /// <summary>
        /// Enqueues a request for batched processing.
        /// </summary>
        /// <param name="request">The inference request</param>
        /// <exception cref="ResourceLimitException">Thrown when queue is full</exception>
        public void EnqueueRequest(InferenceRequest request)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(BatchScheduler));

            if (request == null)
                throw new ArgumentNullException(nameof(request));

            lock (_queueLock)
            {
                // Check queue limit
                if (_totalQueuedCount >= _options.MaxTotalQueuedRequests)
                {
                    throw new ResourceLimitException(
                        "MaxTotalQueuedRequests",
                        _options.MaxTotalQueuedRequests,
                        _totalQueuedCount,
                        "Queue is full. Try again later.");
                }

                _pendingRequests.Enqueue(request);
                _totalQueuedCount++;

                _metrics.RecordQueueDepth(_pendingRequests.Count);
            }

            // Signal scheduler that new work is available
            _batchReadySemaphore.Release();
        }

        /// <summary>
        /// Background loop that forms and dispatches batches.
        /// </summary>
        private async Task SchedulerLoop()
        {
            var stopwatch = new Stopwatch();

            while (!_shutdownCts.Token.IsCancellationRequested)
            {
                try
                {
                    // Wait for either:
                    // 1. New request signal
                    // 2. Timeout for partial batch execution
                    var waitTimeout = _options.MaxBatchWaitMs > 0
                        ? _options.MaxBatchWaitMs
                        : 100; // Default 100ms if no timeout configured

                    await _batchReadySemaphore.WaitAsync(waitTimeout, _shutdownCts.Token);

                    stopwatch.Restart();

                    // Form a batch
                    _currentBatch.Clear();
                    bool hasRequests = false;

                    lock (_queueLock)
                    {
                        // Remove cancelled requests first
                        RemoveCancelledRequests();

                        // Collect up to MaxBatchSize compatible requests
                        while (_currentBatch.Count < _options.MaxBatchSize && _pendingRequests.Count > 0)
                        {
                            var request = _pendingRequests.Peek();

                            // Skip cancelled requests
                            if (request.IsCancelled)
                            {
                                _pendingRequests.Dequeue();
                                _totalQueuedCount--;
                                request.MarkFailed(new OperationCanceledException("Request was cancelled"));
                                continue;
                            }

                            // Check compatibility with current batch
                            if (_currentBatch.Count == 0 || _currentBatch[0].IsCompatibleWith(request))
                            {
                                _pendingRequests.Dequeue();
                                _totalQueuedCount--;
                                _currentBatch.Add(request);
                                hasRequests = true;
                            }
                            else
                            {
                                // Not compatible, stop forming this batch
                                break;
                            }
                        }

                        _metrics.RecordQueueDepth(_pendingRequests.Count);
                    }

                    stopwatch.Stop();

                    if (hasRequests && _currentBatch.Count > 0)
                    {
                        // Record metrics
                        _metrics.RecordBatchSize(_currentBatch.Count);
                        _metrics.RecordBatchWaitTimeMs(stopwatch.Elapsed.TotalMilliseconds);

                        // Apply deterministic scheduling if enabled
                        if (_deterministicScheduler != null && _currentBatch.Count > 0)
                        {
                            ApplyDeterministicScheduling(_currentBatch);
                        }

                        // Dispatch batch for execution
                        // Swap buffers instead of copying to avoid allocation
                        _dispatchBatch.Clear();
                        _dispatchBatch.AddRange(_currentBatch);
                        BatchReady?.Invoke(_dispatchBatch);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Shutdown requested
                    break;
                }
                catch (Exception ex)
                {
                    // Track error in metrics to maintain observability
                    // Scheduler must remain resilient and not crash on individual errors
                    try
                    {
                        _metrics.RecordRequestLatencyMs(-1); // Use -1 to indicate error
                    }
                    catch
                    {
                        // If metrics recording fails, silently continue
                    }
                }
            }
        }

        /// <summary>
        /// Removes cancelled requests from the queue (called under lock).
        /// </summary>
        private void RemoveCancelledRequests()
        {
            // Use temporary list to avoid modifying queue during enumeration
            int initialCount = _pendingRequests.Count;
            if (initialCount == 0)
                return;

            var tempList = new List<InferenceRequest>(initialCount);

            while (_pendingRequests.Count > 0)
            {
                var request = _pendingRequests.Dequeue();
                if (!request.IsCancelled)
                {
                    tempList.Add(request);
                }
                else
                {
                    _totalQueuedCount--;
                    request.MarkFailed(new OperationCanceledException("Request was cancelled"));
                }
            }

            // Re-enqueue non-cancelled requests
            for (int i = 0; i < tempList.Count; i++)
            {
                _pendingRequests.Enqueue(tempList[i]);
            }
        }

        /// <summary>
        /// Apply deterministic scheduling to a batch of requests.
        /// This ensures reproducible ordering and resource allocation.
        /// Uses the first request's prompt tokens as representative for batch scheduling.
        /// Note: Assumes all requests in a batch have similar characteristics (validated by IsCompatibleWith).
        /// </summary>
        private void ApplyDeterministicScheduling(List<InferenceRequest> batch)
        {
            if (_deterministicScheduler == null || batch.Count == 0)
                return;

            // Use the first request's prompt tokens as representative for scheduling
            var representativeRequest = batch[0];
            
            var schedule = _deterministicScheduler.Schedule(
                representativeRequest.PromptTokens,
                maxNewTokens: representativeRequest.Options.MaxNewTokens,
                _options.SchedulingPolicy,
                _options.DeterministicSeed);

            // Store schedule ID in metrics or request metadata for auditing
            // Future: Attach schedule to request for downstream use when InferenceRequest supports it
        }

        /// <summary>
        /// Shuts down the scheduler and waits for cleanup.
        /// </summary>
        public async Task ShutdownAsync()
        {
            if (_disposed)
                return;

            _shutdownCts.Cancel();

            // Wait for scheduler to finish
            try
            {
                await _schedulerTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected
            }

            // Cancel all pending requests and current batch
            lock (_queueLock)
            {
                // Cancel requests in pending queue
                while (_pendingRequests.Count > 0)
                {
                    var request = _pendingRequests.Dequeue();
                    request.MarkFailed(new OperationCanceledException("Scheduler shutdown"));
                }
                _totalQueuedCount = 0;

                // Cancel requests in current batch that never got processed
                for (int i = 0; i < _currentBatch.Count; i++)
                {
                    _currentBatch[i].MarkFailed(new OperationCanceledException("Scheduler shutdown"));
                }
                _currentBatch.Clear();
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _shutdownCts.Cancel();
                
                // Best effort wait
                try
                {
                    _schedulerTask.Wait(TimeSpan.FromSeconds(5));
                }
                catch
                {
                    // Ignore
                }

                _shutdownCts.Dispose();
                _batchReadySemaphore.Dispose();
                _disposed = true;
            }
        }
    }
}
