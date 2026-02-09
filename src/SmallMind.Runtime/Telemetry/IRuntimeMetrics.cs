using System.Threading;

namespace SmallMind.Runtime.Telemetry
{
    /// <summary>
    /// Metrics collection interface for runtime performance tracking.
    /// Designed for minimal overhead with atomic operations.
    /// </summary>
    internal interface IRuntimeMetrics
    {
        void RecordCacheHit();
        void RecordCacheMiss();
        void RecordCacheEviction();
        void RecordBatchSize(int size);
        void RecordBatchWaitTimeMs(double ms);
        void RecordQueueDepth(int depth);
        void RecordTokensPerSecond(double tokensPerSec);
        void RecordRequestLatencyMs(double latencyMs);
    }

    /// <summary>
    /// In-memory metrics collector with atomic counters.
    /// </summary>
    internal sealed class InMemoryRuntimeMetrics : IRuntimeMetrics
    {
        private long _cacheHits;
        private long _cacheMisses;
        private long _cacheEvictions;
        private long _totalBatchSize;
        private long _batchCount;
        private long _totalBatchWaitMs;
        private long _totalQueueDepth;
        private long _queueDepthSamples;
        private long _totalTokensPerSec;
        private long _tokensPerSecSamples;
        private long _totalLatencyMs;
        private long _latencySamples;

        public long CacheHits => Interlocked.Read(ref _cacheHits);
        public long CacheMisses => Interlocked.Read(ref _cacheMisses);
        public long CacheEvictions => Interlocked.Read(ref _cacheEvictions);
        public double AverageBatchSize => _batchCount == 0 ? 0 : (double)_totalBatchSize / _batchCount;
        public double AverageBatchWaitMs => _batchCount == 0 ? 0 : (double)_totalBatchWaitMs / _batchCount;
        public double AverageQueueDepth => _queueDepthSamples == 0 ? 0 : (double)_totalQueueDepth / _queueDepthSamples;
        public double AverageTokensPerSecond => _tokensPerSecSamples == 0 ? 0 : (double)_totalTokensPerSec / _tokensPerSecSamples;
        public double AverageLatencyMs => _latencySamples == 0 ? 0 : (double)_totalLatencyMs / _latencySamples;

        public void RecordCacheHit() => Interlocked.Increment(ref _cacheHits);
        public void RecordCacheMiss() => Interlocked.Increment(ref _cacheMisses);
        public void RecordCacheEviction() => Interlocked.Increment(ref _cacheEvictions);

        public void RecordBatchSize(int size)
        {
            Interlocked.Add(ref _totalBatchSize, size);
            Interlocked.Increment(ref _batchCount);
        }

        public void RecordBatchWaitTimeMs(double ms)
        {
            Interlocked.Add(ref _totalBatchWaitMs, (long)ms);
        }

        public void RecordQueueDepth(int depth)
        {
            Interlocked.Add(ref _totalQueueDepth, depth);
            Interlocked.Increment(ref _queueDepthSamples);
        }

        public void RecordTokensPerSecond(double tokensPerSec)
        {
            Interlocked.Add(ref _totalTokensPerSec, (long)tokensPerSec);
            Interlocked.Increment(ref _tokensPerSecSamples);
        }

        public void RecordRequestLatencyMs(double latencyMs)
        {
            Interlocked.Add(ref _totalLatencyMs, (long)latencyMs);
            Interlocked.Increment(ref _latencySamples);
        }

        public void Reset()
        {
            Interlocked.Exchange(ref _cacheHits, 0);
            Interlocked.Exchange(ref _cacheMisses, 0);
            Interlocked.Exchange(ref _cacheEvictions, 0);
            Interlocked.Exchange(ref _totalBatchSize, 0);
            Interlocked.Exchange(ref _batchCount, 0);
            Interlocked.Exchange(ref _totalBatchWaitMs, 0);
            Interlocked.Exchange(ref _totalQueueDepth, 0);
            Interlocked.Exchange(ref _queueDepthSamples, 0);
            Interlocked.Exchange(ref _totalTokensPerSec, 0);
            Interlocked.Exchange(ref _tokensPerSecSamples, 0);
            Interlocked.Exchange(ref _totalLatencyMs, 0);
            Interlocked.Exchange(ref _latencySamples, 0);
        }
    }

    /// <summary>
    /// Null metrics that does nothing (for minimal overhead).
    /// </summary>
    internal sealed class NullRuntimeMetrics : IRuntimeMetrics
    {
        public static readonly NullRuntimeMetrics Instance = new NullRuntimeMetrics();
        
        private NullRuntimeMetrics() { }

        public void RecordCacheHit() { }
        public void RecordCacheMiss() { }
        public void RecordCacheEviction() { }
        public void RecordBatchSize(int size) { }
        public void RecordBatchWaitTimeMs(double ms) { }
        public void RecordQueueDepth(int depth) { }
        public void RecordTokensPerSecond(double tokensPerSec) { }
        public void RecordRequestLatencyMs(double latencyMs) { }
    }
}
