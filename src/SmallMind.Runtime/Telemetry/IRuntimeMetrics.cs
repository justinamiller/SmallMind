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
        
        // New methods for prefill/decode tracking
        void RecordPrefillStart(int tokenCount);
        void RecordPrefillEnd(int tokenCount, double elapsedMs);
        void RecordDecodeStart();
        void RecordDecodeEnd(double elapsedMs);
        void RecordTTFT(double elapsedMs);
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
        
        // Prefill/decode tracking
        private long _prefillCount;
        private long _prefillTokens;
        private long _prefillTimeMs;
        private long _decodeCount;
        private long _decodeTimeMs;
        private long _ttftMs;
        private long _ttftSamples;

        public long CacheHits => Interlocked.Read(ref _cacheHits);
        public long CacheMisses => Interlocked.Read(ref _cacheMisses);
        public long CacheEvictions => Interlocked.Read(ref _cacheEvictions);
        public double AverageBatchSize => _batchCount == 0 ? 0 : (double)_totalBatchSize / _batchCount;
        public double AverageBatchWaitMs => _batchCount == 0 ? 0 : (double)_totalBatchWaitMs / _batchCount;
        public double AverageQueueDepth => _queueDepthSamples == 0 ? 0 : (double)_totalQueueDepth / _queueDepthSamples;
        public double AverageTokensPerSecond => _tokensPerSecSamples == 0 ? 0 : (double)_totalTokensPerSec / _tokensPerSecSamples;
        public double AverageLatencyMs => _latencySamples == 0 ? 0 : (double)_totalLatencyMs / _latencySamples;
        
        // Prefill/decode stats
        public long PrefillCount => Interlocked.Read(ref _prefillCount);
        public double PrefillTokensPerSecond => _prefillTimeMs == 0 ? 0 : (_prefillTokens * 1000.0) / _prefillTimeMs;
        public double DecodeTokensPerSecond => _decodeTimeMs == 0 ? 0 : (_decodeCount * 1000.0) / _decodeTimeMs;
        public double AverageTTFT => _ttftSamples == 0 ? 0 : (double)_ttftMs / _ttftSamples;

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
        
        public void RecordPrefillStart(int tokenCount)
        {
            // Optional: could track in-flight prefills
        }
        
        public void RecordPrefillEnd(int tokenCount, double elapsedMs)
        {
            Interlocked.Increment(ref _prefillCount);
            Interlocked.Add(ref _prefillTokens, tokenCount);
            Interlocked.Add(ref _prefillTimeMs, (long)elapsedMs);
        }
        
        public void RecordDecodeStart()
        {
            // Optional: could track in-flight decodes
        }
        
        public void RecordDecodeEnd(double elapsedMs)
        {
            Interlocked.Increment(ref _decodeCount);
            Interlocked.Add(ref _decodeTimeMs, (long)elapsedMs);
        }
        
        public void RecordTTFT(double elapsedMs)
        {
            Interlocked.Add(ref _ttftMs, (long)elapsedMs);
            Interlocked.Increment(ref _ttftSamples);
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
            Interlocked.Exchange(ref _prefillCount, 0);
            Interlocked.Exchange(ref _prefillTokens, 0);
            Interlocked.Exchange(ref _prefillTimeMs, 0);
            Interlocked.Exchange(ref _decodeCount, 0);
            Interlocked.Exchange(ref _decodeTimeMs, 0);
            Interlocked.Exchange(ref _ttftMs, 0);
            Interlocked.Exchange(ref _ttftSamples, 0);
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
        public void RecordPrefillStart(int tokenCount) { }
        public void RecordPrefillEnd(int tokenCount, double elapsedMs) { }
        public void RecordDecodeStart() { }
        public void RecordDecodeEnd(double elapsedMs) { }
        public void RecordTTFT(double elapsedMs) { }
    }
}
