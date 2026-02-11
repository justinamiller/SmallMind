using System;
using System.Threading;

namespace SmallMind.Abstractions.Telemetry
{
    /// <summary>
    /// Runtime metrics interface for performance tracking.
    /// Designed for minimal overhead with atomic operations.
    /// Implementations must be thread-safe.
    /// </summary>
    public interface IRuntimeMetrics
    {
        /// <summary>
        /// Records the start of model loading.
        /// </summary>
        void RecordModelLoadStart();

        /// <summary>
        /// Records the end of model loading.
        /// </summary>
        /// <param name="elapsedMs">Elapsed time in milliseconds.</param>
        void RecordModelLoadEnd(double elapsedMs);

        /// <summary>
        /// Records the start of inference.
        /// </summary>
        void RecordInferenceStart();

        /// <summary>
        /// Records the first token generation.
        /// </summary>
        /// <param name="elapsedMs">Time to first token in milliseconds.</param>
        void RecordFirstToken(double elapsedMs);

        /// <summary>
        /// Records a token generation event (optional, be careful with volume).
        /// </summary>
        void RecordTokenGenerated();

        /// <summary>
        /// Records the end of inference.
        /// </summary>
        /// <param name="status">Stop status.</param>
        /// <param name="reason">Reason code for stopping.</param>
        void RecordInferenceStop(string status, RuntimeStopReason reason);

        /// <summary>
        /// Records tokens per second rate.
        /// </summary>
        /// <param name="tokensPerSec">Tokens generated per second.</param>
        void RecordTokensPerSecond(double tokensPerSec);

        /// <summary>
        /// Records prompt tokens processed.
        /// </summary>
        /// <param name="count">Number of prompt tokens.</param>
        void RecordPromptTokens(int count);

        /// <summary>
        /// Records completion tokens generated.
        /// </summary>
        /// <param name="count">Number of completion tokens.</param>
        void RecordCompletionTokens(int count);

        /// <summary>
        /// Records KV cache size.
        /// </summary>
        /// <param name="sizeBytes">Cache size in bytes.</param>
        void RecordKvCacheSize(long sizeBytes);

        /// <summary>
        /// Records a KV cache hit.
        /// </summary>
        void RecordKvCacheHit();

        /// <summary>
        /// Records a KV cache miss.
        /// </summary>
        void RecordKvCacheMiss();

        /// <summary>
        /// Records estimated memory high-water mark.
        /// </summary>
        /// <param name="memoryBytes">Memory usage in bytes.</param>
        void RecordMemoryHighWaterMark(long memoryBytes);
    }

    /// <summary>
    /// In-memory runtime metrics with atomic counters.
    /// </summary>
    public sealed class InMemoryRuntimeMetrics : IRuntimeMetrics
    {
        private long _modelLoadCount;
        private long _modelLoadTimeMs;
        private long _inferenceCount;
        private long _totalTimeToFirstTokenMs;
        private long _firstTokenSamples;
        private long _tokensGenerated;
        private long _totalTokensPerSec;
        private long _tokensPerSecSamples;
        private long _promptTokensTotal;
        private long _completionTokensTotal;
        private long _kvCacheSizeBytes;
        private long _kvCacheHits;
        private long _kvCacheMisses;
        private long _memoryHighWaterMarkBytes;

        // Read-only properties for consumers
        /// <summary>Gets the total number of model loads.</summary>
        public long ModelLoadCount => Interlocked.Read(ref _modelLoadCount);

        /// <summary>Gets the average model load time in milliseconds.</summary>
        public double AverageModelLoadTimeMs => _modelLoadCount == 0 ? 0 : (double)_modelLoadTimeMs / _modelLoadCount;

        /// <summary>Gets the total number of inferences.</summary>
        public long InferenceCount => Interlocked.Read(ref _inferenceCount);

        /// <summary>Gets the average time to first token in milliseconds.</summary>
        public double AverageTimeToFirstTokenMs => _firstTokenSamples == 0 ? 0 : (double)_totalTimeToFirstTokenMs / _firstTokenSamples;

        /// <summary>Gets the total number of tokens generated.</summary>
        public long TokensGenerated => Interlocked.Read(ref _tokensGenerated);

        /// <summary>Gets the average tokens per second.</summary>
        public double AverageTokensPerSecond => _tokensPerSecSamples == 0 ? 0 : (double)_totalTokensPerSec / _tokensPerSecSamples;

        /// <summary>Gets the total prompt tokens processed.</summary>
        public long PromptTokensTotal => Interlocked.Read(ref _promptTokensTotal);

        /// <summary>Gets the total completion tokens generated.</summary>
        public long CompletionTokensTotal => Interlocked.Read(ref _completionTokensTotal);

        /// <summary>Gets the current KV cache size in bytes.</summary>
        public long KvCacheSizeBytes => Interlocked.Read(ref _kvCacheSizeBytes);

        /// <summary>Gets the total KV cache hits.</summary>
        public long KvCacheHits => Interlocked.Read(ref _kvCacheHits);

        /// <summary>Gets the total KV cache misses.</summary>
        public long KvCacheMisses => Interlocked.Read(ref _kvCacheMisses);

        /// <summary>Gets the KV cache hit rate (0.0 to 1.0).</summary>
        public double KvCacheHitRate
        {
            get
            {
                long hits = KvCacheHits;
                long total = hits + KvCacheMisses;
                return total == 0 ? 0 : (double)hits / total;
            }
        }

        /// <summary>Gets the memory high-water mark in bytes.</summary>
        public long MemoryHighWaterMarkBytes => Interlocked.Read(ref _memoryHighWaterMarkBytes);

        /// <inheritdoc/>
        public void RecordModelLoadStart()
        {
            // Optional: track in-flight loads
        }

        /// <inheritdoc/>
        public void RecordModelLoadEnd(double elapsedMs)
        {
            Interlocked.Increment(ref _modelLoadCount);
            Interlocked.Add(ref _modelLoadTimeMs, (long)elapsedMs);
        }

        /// <inheritdoc/>
        public void RecordInferenceStart()
        {
            Interlocked.Increment(ref _inferenceCount);
        }

        /// <inheritdoc/>
        public void RecordFirstToken(double elapsedMs)
        {
            Interlocked.Add(ref _totalTimeToFirstTokenMs, (long)elapsedMs);
            Interlocked.Increment(ref _firstTokenSamples);
        }

        /// <inheritdoc/>
        public void RecordTokenGenerated()
        {
            Interlocked.Increment(ref _tokensGenerated);
        }

        /// <inheritdoc/>
        public void RecordInferenceStop(string status, RuntimeStopReason reason)
        {
            // Could track stop reasons in a dictionary if needed
        }

        /// <inheritdoc/>
        public void RecordTokensPerSecond(double tokensPerSec)
        {
            Interlocked.Add(ref _totalTokensPerSec, (long)tokensPerSec);
            Interlocked.Increment(ref _tokensPerSecSamples);
        }

        /// <inheritdoc/>
        public void RecordPromptTokens(int count)
        {
            Interlocked.Add(ref _promptTokensTotal, count);
        }

        /// <inheritdoc/>
        public void RecordCompletionTokens(int count)
        {
            Interlocked.Add(ref _completionTokensTotal, count);
        }

        /// <inheritdoc/>
        public void RecordKvCacheSize(long sizeBytes)
        {
            Interlocked.Exchange(ref _kvCacheSizeBytes, sizeBytes);
        }

        /// <inheritdoc/>
        public void RecordKvCacheHit()
        {
            Interlocked.Increment(ref _kvCacheHits);
        }

        /// <inheritdoc/>
        public void RecordKvCacheMiss()
        {
            Interlocked.Increment(ref _kvCacheMisses);
        }

        /// <inheritdoc/>
        public void RecordMemoryHighWaterMark(long memoryBytes)
        {
            // Use max value
            long current;
            long newValue = memoryBytes;
            do
            {
                current = Interlocked.Read(ref _memoryHighWaterMarkBytes);
                if (newValue <= current) return;
            }
            while (Interlocked.CompareExchange(ref _memoryHighWaterMarkBytes, newValue, current) != current);
        }

        /// <summary>
        /// Resets all metrics to zero.
        /// </summary>
        public void Reset()
        {
            Interlocked.Exchange(ref _modelLoadCount, 0);
            Interlocked.Exchange(ref _modelLoadTimeMs, 0);
            Interlocked.Exchange(ref _inferenceCount, 0);
            Interlocked.Exchange(ref _totalTimeToFirstTokenMs, 0);
            Interlocked.Exchange(ref _firstTokenSamples, 0);
            Interlocked.Exchange(ref _tokensGenerated, 0);
            Interlocked.Exchange(ref _totalTokensPerSec, 0);
            Interlocked.Exchange(ref _tokensPerSecSamples, 0);
            Interlocked.Exchange(ref _promptTokensTotal, 0);
            Interlocked.Exchange(ref _completionTokensTotal, 0);
            Interlocked.Exchange(ref _kvCacheSizeBytes, 0);
            Interlocked.Exchange(ref _kvCacheHits, 0);
            Interlocked.Exchange(ref _kvCacheMisses, 0);
            Interlocked.Exchange(ref _memoryHighWaterMarkBytes, 0);
        }
    }

    /// <summary>
    /// Null runtime metrics (does nothing).
    /// </summary>
    public sealed class NullRuntimeMetrics : IRuntimeMetrics
    {
        /// <summary>
        /// Gets the singleton instance.
        /// </summary>
        public static readonly NullRuntimeMetrics Instance = new();

        private NullRuntimeMetrics() { }

        /// <inheritdoc/>
        public void RecordModelLoadStart() { }
        /// <inheritdoc/>
        public void RecordModelLoadEnd(double elapsedMs) { }
        /// <inheritdoc/>
        public void RecordInferenceStart() { }
        /// <inheritdoc/>
        public void RecordFirstToken(double elapsedMs) { }
        /// <inheritdoc/>
        public void RecordTokenGenerated() { }
        /// <inheritdoc/>
        public void RecordInferenceStop(string status, RuntimeStopReason reason) { }
        /// <inheritdoc/>
        public void RecordTokensPerSecond(double tokensPerSec) { }
        /// <inheritdoc/>
        public void RecordPromptTokens(int count) { }
        /// <inheritdoc/>
        public void RecordCompletionTokens(int count) { }
        /// <inheritdoc/>
        public void RecordKvCacheSize(long sizeBytes) { }
        /// <inheritdoc/>
        public void RecordKvCacheHit() { }
        /// <inheritdoc/>
        public void RecordKvCacheMiss() { }
        /// <inheritdoc/>
        public void RecordMemoryHighWaterMark(long memoryBytes) { }
    }
}
