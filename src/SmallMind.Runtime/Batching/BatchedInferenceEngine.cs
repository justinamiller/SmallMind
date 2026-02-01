using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using SmallMind.Core.Core;
using SmallMind.Core.Exceptions;
using SmallMind.Runtime.Cache;
using SmallMind.Runtime.Telemetry;
using SmallMind.Tokenizers;
using SmallMind.Transformers;

namespace SmallMind.Runtime.Batching
{
    /// <summary>
    /// Batched inference engine that processes multiple requests together for improved throughput.
    /// Routes requests through the batch scheduler and executes batches efficiently.
    /// </summary>
    public sealed class BatchedInferenceEngine : IDisposable
    {
        private readonly TransformerModel _model;
        private readonly ITokenizer _tokenizer;
        private readonly int _blockSize;
        private readonly BatchingOptions _batchingOptions;
        private readonly BatchScheduler _scheduler;
        private readonly IRuntimeMetrics _metrics;
        private readonly SemaphoreSlim _executionSemaphore;

        private bool _disposed;

        /// <summary>
        /// Gets the batching options for this engine.
        /// </summary>
        public BatchingOptions BatchingOptions => _batchingOptions;

        /// <summary>
        /// Creates a new batched inference engine.
        /// </summary>
        /// <param name="model">Transformer model (weights are shared, read-only)</param>
        /// <param name="tokenizer">Tokenizer (read-only)</param>
        /// <param name="blockSize">Model block size for context window</param>
        /// <param name="batchingOptions">Batching configuration</param>
        /// <param name="metrics">Optional metrics collector</param>
        public BatchedInferenceEngine(
            TransformerModel model,
            ITokenizer tokenizer,
            int blockSize,
            BatchingOptions batchingOptions,
            IRuntimeMetrics? metrics = null)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _tokenizer = tokenizer ?? throw new ArgumentNullException(nameof(tokenizer));
            _blockSize = blockSize;
            _batchingOptions = batchingOptions?.Clone() ?? throw new ArgumentNullException(nameof(batchingOptions));
            _batchingOptions.Validate();

            _metrics = metrics ?? NullRuntimeMetrics.Instance;

            // Ensure model is in eval mode
            _model.Eval();

            // Create scheduler if batching is enabled
            if (_batchingOptions.Enabled)
            {
                _scheduler = new BatchScheduler(_batchingOptions, _metrics);
                _scheduler.BatchReady += OnBatchReady;

                // Limit concurrent batch executions to prevent resource exhaustion
                _executionSemaphore = new SemaphoreSlim(1, 1);
            }
            else
            {
                // No scheduler needed for non-batched mode
                _executionSemaphore = new SemaphoreSlim(Environment.ProcessorCount, Environment.ProcessorCount);
            }
        }

        /// <summary>
        /// Generate text from a prompt (non-streaming).
        /// </summary>
        /// <param name="prompt">Input text prompt</param>
        /// <param name="options">Inference options</param>
        /// <param name="metrics">Optional performance metrics collector</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Generated text including the original prompt</returns>
        public async Task<string> GenerateAsync(
            string prompt,
            ProductionInferenceOptions options,
            PerformanceMetrics? metrics = null,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (!_batchingOptions.Enabled)
            {
                // Fallback to direct generation
                return await GenerateDirectAsync(prompt, options, metrics, cancellationToken);
            }

            // Encode prompt
            var tokens = _tokenizer.Encode(prompt);
            var tokenArray = tokens.ToArray();

            // Create request
            var sessionId = SessionId.NewId();
            var request = new InferenceRequest(sessionId, tokenArray, options, cancellationToken);

            try
            {
                // Enqueue for batched processing
                _scheduler.EnqueueRequest(request);

                // Collect all generated tokens
                var generatedTokens = new List<GeneratedToken>();

                await foreach (var token in request.ResponseReader.ReadAllAsync(cancellationToken))
                {
                    generatedTokens.Add(token);
                }

                // Reconstruct full token sequence
                var allTokens = new List<int>(tokenArray);
                for (int i = 0; i < generatedTokens.Count; i++)
                {
                    allTokens.Add(generatedTokens[i].TokenId);
                }

                return _tokenizer.Decode(allTokens);
            }
            finally
            {
                request.Dispose();
            }
        }

        /// <summary>
        /// Generate text as a stream of tokens.
        /// </summary>
        /// <param name="prompt">Input text prompt</param>
        /// <param name="options">Inference options</param>
        /// <param name="metrics">Optional performance metrics collector</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Async enumerable of generated tokens</returns>
        public async IAsyncEnumerable<GeneratedToken> GenerateStreamingAsync(
            string prompt,
            ProductionInferenceOptions options,
            PerformanceMetrics? metrics = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (!_batchingOptions.Enabled)
            {
                // Fallback to direct generation
                await foreach (var token in GenerateDirectStreamingAsync(prompt, options, metrics, cancellationToken))
                {
                    yield return token;
                }
                yield break;
            }

            // Encode prompt
            var tokens = _tokenizer.Encode(prompt);
            var tokenArray = tokens.ToArray();

            // Create request
            var sessionId = SessionId.NewId();
            var request = new InferenceRequest(sessionId, tokenArray, options, cancellationToken);

            try
            {
                // Enqueue for batched processing
                _scheduler.EnqueueRequest(request);

                // Stream tokens as they're generated
                await foreach (var token in request.ResponseReader.ReadAllAsync(cancellationToken))
                {
                    yield return token;
                }
            }
            finally
            {
                request.Dispose();
            }
        }

        /// <summary>
        /// Called when a batch is ready for execution.
        /// Processes all requests in the batch together.
        /// </summary>
        private void OnBatchReady(List<InferenceRequest> batch)
        {
            if (batch == null || batch.Count == 0)
                return;

            // Execute batch asynchronously (fire and forget)
            _ = Task.Run(async () =>
            {
                await _executionSemaphore.WaitAsync();
                try
                {
                    await ProcessBatchAsync(batch);
                }
                catch (Exception ex)
                {
                    // Mark all requests as failed
                    for (int i = 0; i < batch.Count; i++)
                    {
                        batch[i].MarkFailed(ex);
                    }
                }
                finally
                {
                    _executionSemaphore.Release();
                }
            });
        }

        /// <summary>
        /// Processes a batch of requests together.
        /// For prefill-only batching, we process the initial prompt tokens together,
        /// then fall back to individual generation.
        /// </summary>
        private async Task ProcessBatchAsync(List<InferenceRequest> batch)
        {
            if (_batchingOptions.PrefillOnly)
            {
                // Prefill-only: batch the initial forward pass, then generate individually
                await ProcessBatchPrefillOnlyAsync(batch);
            }
            else
            {
                // Full batching: batch both prefill and decode (more complex)
                // For now, fall back to individual processing
                // TODO: Implement full batched decode
                for (int i = 0; i < batch.Count; i++)
                {
                    await ProcessSingleRequestAsync(batch[i]);
                }
            }
        }

        /// <summary>
        /// Processes a batch with prefill-only batching.
        /// The initial prompt processing is batched, then each request continues individually.
        /// </summary>
        private async Task ProcessBatchPrefillOnlyAsync(List<InferenceRequest> batch)
        {
            // For simplicity in this implementation, we process each request individually
            // A full implementation would:
            // 1. Pad all prompts to same length
            // 2. Create batched tensor [batch_size, max_seq_len]
            // 3. Run single forward pass
            // 4. Continue generation individually

            // Process each request individually
            var tasks = new Task[batch.Count];
            for (int i = 0; i < batch.Count; i++)
            {
                var request = batch[i];
                tasks[i] = Task.Run(() => ProcessSingleRequestAsync(request));
            }

            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// Processes a single request (fallback for non-batched or individual generation).
        /// </summary>
        private async Task ProcessSingleRequestAsync(InferenceRequest request)
        {
            try
            {
                if (request.IsCancelled)
                {
                    request.MarkFailed(new OperationCanceledException("Request was cancelled"));
                    return;
                }

                // Build context from prompt tokens
                var context = new List<int>(request.PromptTokens);
                var options = request.Options;

                // Generate tokens
                for (int i = 0; i < options.MaxNewTokens; i++)
                {
                    if (request.IsCancelled)
                    {
                        request.MarkFailed(new OperationCanceledException("Request was cancelled"));
                        return;
                    }

                    // Check context limit
                    if (options.MaxContextTokens > 0 && context.Count >= options.MaxContextTokens)
                    {
                        break;
                    }

                    var nextToken = await GenerateNextTokenAsync(context, options, request.CancellationToken);
                    context.Add(nextToken);

                    // Decode token
                    var tokenText = _tokenizer.Decode(new List<int> { nextToken });

                    var generatedToken = new GeneratedToken(
                        tokenId: nextToken,
                        text: tokenText,
                        index: i,
                        logProb: null
                    );

                    // Send to response channel
                    await request.ResponseWriter.WriteAsync(generatedToken, request.CancellationToken);

                    request.CurrentPosition = i + 1;
                    request.GeneratedTokenCount = i + 1;
                }

                request.MarkComplete();
            }
            catch (Exception ex)
            {
                request.MarkFailed(ex);
            }
        }

        /// <summary>
        /// Generates the next token for a single request.
        /// </summary>
        private async Task<int> GenerateNextTokenAsync(
            List<int> context,
            ProductionInferenceOptions options,
            CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Crop context to last blockSize tokens
                List<int> contextCropped;
                if (context.Count <= _blockSize)
                {
                    contextCropped = context;
                }
                else
                {
                    contextCropped = new List<int>(_blockSize);
                    int startIdx = context.Count - _blockSize;
                    for (int idx = startIdx; idx < context.Count; idx++)
                    {
                        contextCropped.Add(context[idx]);
                    }
                }

                // Convert to tensor
                var contextData = new float[contextCropped.Count];
                for (int j = 0; j < contextCropped.Count; j++)
                {
                    contextData[j] = contextCropped[j];
                }
                var contextTensor = new Tensor(contextData, new int[] { 1, contextCropped.Count });

                // Forward pass
                var logits = _model.Forward(contextTensor);

                // Get logits for last position
                int T = contextCropped.Count;
                int vocabSize = logits.Shape[2];
                var logitsLast = new float[vocabSize];
                int lastPosOffset = (T - 1) * vocabSize;
                for (int v = 0; v < vocabSize; v++)
                {
                    logitsLast[v] = logits.Data[lastPosOffset + v];
                }

                // Apply temperature
                if (options.Temperature != 1.0)
                {
                    for (int v = 0; v < vocabSize; v++)
                    {
                        logitsLast[v] /= (float)options.Temperature;
                    }
                }

                // Apply top-k filtering
                if (options.TopK > 0)
                {
                    logitsLast = ApplyTopK(logitsLast, options.TopK);
                }

                // Convert to probabilities (softmax)
                var probs = Softmax(logitsLast);

                // Sample from the distribution
                return SampleFromProbs(probs, options);
            }, cancellationToken);
        }

        private float[] ApplyTopK(float[] logits, int k)
        {
            if (k >= logits.Length)
                return logits;

            float[]? rentedBuffer = null;
            try
            {
                rentedBuffer = ArrayPool<float>.Shared.Rent(logits.Length);
                Array.Copy(logits, rentedBuffer, logits.Length);

                Array.Sort(rentedBuffer, 0, logits.Length);
                Array.Reverse(rentedBuffer, 0, logits.Length);
                float kthValue = rentedBuffer[Math.Min(k - 1, logits.Length - 1)];

                var filtered = new float[logits.Length];
                for (int i = 0; i < logits.Length; i++)
                {
                    filtered[i] = logits[i] >= kthValue ? logits[i] : float.NegativeInfinity;
                }

                return filtered;
            }
            finally
            {
                if (rentedBuffer != null)
                {
                    ArrayPool<float>.Shared.Return(rentedBuffer);
                }
            }
        }

        private float[] Softmax(float[] logits)
        {
            var probs = new float[logits.Length];

            float max = float.NegativeInfinity;
            for (int i = 0; i < logits.Length; i++)
            {
                if (logits[i] != float.NegativeInfinity)
                {
                    max = Math.Max(max, logits[i]);
                }
            }

            float sum = 0;
            for (int i = 0; i < logits.Length; i++)
            {
                if (logits[i] != float.NegativeInfinity)
                {
                    probs[i] = MathF.Exp(logits[i] - max);
                    sum += probs[i];
                }
                else
                {
                    probs[i] = 0;
                }
            }

            if (sum > 0)
            {
                for (int i = 0; i < probs.Length; i++)
                {
                    probs[i] /= sum;
                }
            }

            return probs;
        }

        [ThreadStatic]
        private static Random? _threadLocalRandom;

        private int SampleFromProbs(float[] probs, ProductionInferenceOptions options)
        {
            Random random;
            
            if (options.Seed.HasValue)
            {
                // Always create new deterministic random with seed for reproducibility
                // Note: This creates overhead but ensures deterministic behavior
                random = new Random(options.Seed.Value);
            }
            else
            {
                // Use thread-local random for efficiency in non-deterministic mode
                _threadLocalRandom ??= new Random();
                random = _threadLocalRandom;
            }

            double target = random.NextDouble();
            double cumSum = 0.0;

            for (int i = 0; i < probs.Length; i++)
            {
                cumSum += probs[i];
                if (cumSum >= target)
                {
                    return i;
                }
            }

            return probs.Length - 1;
        }

        // Fallback non-batched generation
        private async Task<string> GenerateDirectAsync(
            string prompt,
            ProductionInferenceOptions options,
            PerformanceMetrics? metrics,
            CancellationToken cancellationToken)
        {
            await _executionSemaphore.WaitAsync(cancellationToken);
            try
            {
                using var session = new InferenceSession(_model, _tokenizer, options, _blockSize);
                return await session.GenerateAsync(prompt, metrics, cancellationToken);
            }
            finally
            {
                _executionSemaphore.Release();
            }
        }

        private async IAsyncEnumerable<GeneratedToken> GenerateDirectStreamingAsync(
            string prompt,
            ProductionInferenceOptions options,
            PerformanceMetrics? metrics,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await _executionSemaphore.WaitAsync(cancellationToken);
            InferenceSession? session = null;
            try
            {
                session = new InferenceSession(_model, _tokenizer, options, _blockSize);
                await foreach (var token in session.GenerateStreamAsync(prompt, metrics, cancellationToken))
                {
                    yield return token;
                }
            }
            finally
            {
                session?.Dispose();
                _executionSemaphore.Release();
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(BatchedInferenceEngine));
            }
        }

        public async Task ShutdownAsync()
        {
            if (_disposed)
                return;

            if (_scheduler != null)
            {
                await _scheduler.ShutdownAsync();
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _scheduler?.Dispose();
                _executionSemaphore?.Dispose();
                _disposed = true;
            }
        }
    }
}
