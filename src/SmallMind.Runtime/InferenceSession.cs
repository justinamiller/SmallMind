using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using SmallMind.Core.Core;
using SmallMind.Core.Exceptions;
using SmallMind.Core.Rng;
using SmallMind.Runtime.Scheduling;
using SmallMind.Tokenizers;
using SmallMind.Transformers;

namespace SmallMind.Runtime
{
    /// <summary>
    /// Isolated inference session with bounded resources and thread-safe state management.
    /// Each session maintains its own KV cache and generation state.
    /// Safe for concurrent execution across multiple sessions.
    /// Supports deterministic token scheduling for reproducibility.
    /// </summary>
    public sealed class InferenceSession : IDisposable
    {
        private readonly TransformerModel _model;
        private readonly ITokenizer _tokenizer;
        private readonly ProductionInferenceOptions _options;
        private readonly DeterministicRng? _deterministicRng;
        private readonly System.Random _random;
        private readonly int _blockSize;
        
        // Reusable buffers to reduce allocations
        private float[]? _probabilityBuffer;
        
        // Deterministic scheduler (optional)
        private readonly DeterministicScheduler? _scheduler;
        private TokenScheduleResult? _currentSchedule;
        
        private bool _disposed;
        
        /// <summary>
        /// Gets the session ID (for tracking/logging).
        /// </summary>
        public string SessionId { get; }
        
        /// <summary>
        /// Gets the current schedule (if schedule tracking is enabled).
        /// </summary>
        public TokenScheduleResult? CurrentSchedule => _currentSchedule;

        /// <summary>
        /// Gets the options used for this session.
        /// </summary>
        public ProductionInferenceOptions Options => _options;
        
        /// <summary>
        /// Creates a new inference session.
        /// </summary>
        /// <param name="model">Transformer model (weights are shared, read-only)</param>
        /// <param name="tokenizer">Tokenizer (read-only)</param>
        /// <param name="options">Inference options (cloned internally)</param>
        /// <param name="blockSize">Model block size for context window</param>
        /// <param name="sessionId">Optional session ID for tracking</param>
        public InferenceSession(
            TransformerModel model,
            ITokenizer tokenizer,
            ProductionInferenceOptions options,
            int blockSize,
            string? sessionId = null)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _tokenizer = tokenizer ?? throw new ArgumentNullException(nameof(tokenizer));
            _blockSize = blockSize;
            
            // Clone options to prevent external modifications
            _options = options?.Clone() ?? throw new ArgumentNullException(nameof(options));
            _options.Validate();
            
            // Initialize RNG
            if (_options.Seed.HasValue)
            {
                _deterministicRng = new DeterministicRng(_options.Seed.Value);
                _random = new System.Random(_options.Seed.Value); // Fallback
            }
            else
            {
                _random = new System.Random();
            }
            
            // Initialize scheduler if schedule tracking is enabled
            if (_options.EnableScheduleTracking)
            {
                _scheduler = new DeterministicScheduler();
            }
            
            SessionId = sessionId ?? Guid.NewGuid().ToString("N");
            
            // Set model to eval mode
            _model.Eval();
        }
        
        /// <summary>
        /// Generate text from a prompt (non-streaming).
        /// </summary>
        /// <param name="prompt">Input text prompt</param>
        /// <param name="metrics">Optional performance metrics collector</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Generated text including the original prompt</returns>
        public async Task<string> GenerateAsync(
            string prompt,
            PerformanceMetrics? metrics = null,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            
            // Create timeout cancellation if configured
            using var timeoutCts = _options.MaxTimeMs > 0 
                ? new CancellationTokenSource(_options.MaxTimeMs) 
                : null;
            
            using var linkedCts = timeoutCts != null
                ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token)
                : null;
            
            var effectiveToken = linkedCts?.Token ?? cancellationToken;
            
            try
            {
                // Encode and validate input
                var context = _tokenizer.Encode(prompt);
                ValidateAndTruncateInput(context);
                
                int inputTokens = context.Count;
                int requestId = -1;
                
                // Create schedule if tracking is enabled
                if (_scheduler != null)
                {
                    var promptArray = context.ToArray();
                    _currentSchedule = _scheduler.Schedule(
                        promptArray,
                        _options.MaxNewTokens,
                        _options.SchedulingPolicy,
                        _options.Seed.HasValue ? (uint)_options.Seed.Value : null);
                }
                
                // Start metrics tracking
                if (metrics != null)
                {
                    if (!metrics.IsEnabled)
                    {
                        metrics.Start();
                    }
                    requestId = metrics.RecordRequestStart();
                    metrics.RecordInferenceStart(requestId);
                }
                
                bool firstTokenRecorded = false;
                
                // Generate tokens
                for (int i = 0; i < _options.MaxNewTokens; i++)
                {
                    effectiveToken.ThrowIfCancellationRequested();
                    
                    // Check context limit
                    if (_options.MaxContextTokens > 0 && context.Count >= _options.MaxContextTokens)
                    {
                        break;
                    }
                    
                    var nextToken = await GenerateNextTokenAsync(context, effectiveToken);
                    context.Add(nextToken);
                    
                    // Record first token for TTFT
                    if (metrics != null && !firstTokenRecorded)
                    {
                        metrics.RecordFirstToken(requestId);
                        firstTokenRecorded = true;
                    }
                }
                
                // Record completion
                if (metrics != null)
                {
                    metrics.RecordRequestComplete(requestId, inputTokens, context.Count - inputTokens, success: true);
                }
                
                // Decode and return
                return _tokenizer.Decode(context);
            }
            catch (OperationCanceledException) when (timeoutCts?.IsCancellationRequested == true)
            {
                throw new InferenceTimeoutException(_options.MaxTimeMs);
            }
        }
        
        /// <summary>
        /// Generate text as a stream of tokens.
        /// </summary>
        /// <param name="prompt">Input text prompt</param>
        /// <param name="metrics">Optional performance metrics collector</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Async enumerable of generated tokens</returns>
        public async IAsyncEnumerable<GeneratedToken> GenerateStreamAsync(
            string prompt,
            PerformanceMetrics? metrics = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            
            // Create timeout cancellation if configured
            using var timeoutCts = _options.MaxTimeMs > 0 
                ? new CancellationTokenSource(_options.MaxTimeMs) 
                : null;
            
            using var linkedCts = timeoutCts != null
                ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token)
                : null;
            
            var effectiveToken = linkedCts?.Token ?? cancellationToken;
            
            // Encode and validate input
            var context = _tokenizer.Encode(prompt);
            ValidateAndTruncateInput(context);
            
            int inputTokens = context.Count;
            int requestId = -1;
            
            // Create schedule if tracking is enabled
            if (_scheduler != null)
            {
                var promptArray = context.ToArray();
                _currentSchedule = _scheduler.Schedule(
                    promptArray,
                    _options.MaxNewTokens,
                    _options.SchedulingPolicy,
                    _options.Seed.HasValue ? (uint)_options.Seed.Value : null);
            }
            
            // Start metrics tracking
            if (metrics != null)
            {
                if (!metrics.IsEnabled)
                {
                    metrics.Start();
                }
                requestId = metrics.RecordRequestStart();
                metrics.RecordInferenceStart(requestId);
            }
            
            bool firstTokenRecorded = false;
            
            // Generate tokens (no try-catch to allow yield)
            // Timeout is handled by cancellation token
            for (int i = 0; i < _options.MaxNewTokens; i++)
            {
                // Check for timeout
                if (effectiveToken.IsCancellationRequested)
                {
                    if (timeoutCts?.IsCancellationRequested == true)
                    {
                        throw new InferenceTimeoutException(_options.MaxTimeMs);
                    }
                    effectiveToken.ThrowIfCancellationRequested();
                }
                
                // Check context limit
                if (_options.MaxContextTokens > 0 && context.Count >= _options.MaxContextTokens)
                {
                    break;
                }
                
                var nextToken = await GenerateNextTokenAsync(context, effectiveToken);
                context.Add(nextToken);
                
                // Record first token for TTFT
                if (metrics != null && !firstTokenRecorded)
                {
                    metrics.RecordFirstToken(requestId);
                    firstTokenRecorded = true;
                }
                
                // Decode just this token
                var tokenText = _tokenizer.Decode(new List<int> { nextToken });
                
                yield return new GeneratedToken(
                    tokenId: nextToken,
                    text: tokenText,
                    index: i,
                    logProb: null // Note: Log probability calculation not yet implemented (future enhancement)
                );
            }
            
            // Record completion
            if (metrics != null)
            {
                metrics.RecordRequestComplete(requestId, inputTokens, context.Count - inputTokens, success: true);
            }
        }
        
        private async Task<int> GenerateNextTokenAsync(List<int> context, CancellationToken cancellationToken)
        {
            // Tier-0 optimization: Remove Task.Run from per-token compute (synchronous compute, async only for cancellation)
            // Check cancellation before compute
            cancellationToken.ThrowIfCancellationRequested();
            
            // Crop context to last blockSize tokens
            List<int> contextCropped;
            if (context.Count <= _blockSize)
            {
                contextCropped = context;
            }
            else
            {
                // Manual copy of last blockSize tokens (avoid LINQ allocation)
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
            
            // Forward pass (synchronous compute)
            var logits = _model.Forward(contextTensor);
            
            // Get logits for last position
            int T = contextCropped.Count;
            int vocabSize = logits.Shape[2];
            
            // Tier-0 optimization: Reuse logits buffer instead of allocating per token
            if (_probabilityBuffer == null || _probabilityBuffer.Length != vocabSize)
            {
                _probabilityBuffer = new float[vocabSize];
            }
            
            var logitsLast = _probabilityBuffer; // Reuse buffer
            int lastPosOffset = (T - 1) * vocabSize;
            for (int v = 0; v < vocabSize; v++)
            {
                logitsLast[v] = logits.Data[lastPosOffset + v];
            }
            
            // Apply temperature
            if (_options.Temperature != 1.0)
            {
                for (int v = 0; v < vocabSize; v++)
                {
                    logitsLast[v] /= (float)_options.Temperature;
                }
            }
            
            // Apply top-k filtering
            if (_options.TopK > 0)
            {
                logitsLast = ApplyTopK(logitsLast, _options.TopK);
            }
            
            // Convert to probabilities (softmax)
            var probs = Softmax(logitsLast);
            
            // Sample from the distribution
            return SampleFromProbs(probs);
        }
        
        private void ValidateAndTruncateInput(List<int> tokens)
        {
            if (_options.MaxInputTokens <= 0)
            {
                return; // No limit
            }
            
            if (tokens.Count > _options.MaxInputTokens)
            {
                if (_options.TruncateInput)
                {
                    // Truncate to last MaxInputTokens (keep most recent context)
                    int removeCount = tokens.Count - _options.MaxInputTokens;
                    tokens.RemoveRange(0, removeCount);
                }
                else
                {
                    throw new ResourceLimitException(
                        "MaxInputTokens",
                        _options.MaxInputTokens,
                        tokens.Count,
                        "Set TruncateInput=true to automatically truncate, or reduce input size.");
                }
            }
        }
        
        private float[] ApplyTopK(float[] logits, int k)
        {
            if (k >= logits.Length)
            {
                return logits;
            }
            
            // Rent buffer for sorting to reduce allocations
            float[]? rentedBuffer = null;
            try
            {
                rentedBuffer = ArrayPool<float>.Shared.Rent(logits.Length);
                Array.Copy(logits, rentedBuffer, logits.Length);
                
                // Partial sort - only need to find k-th largest
                Array.Sort(rentedBuffer, 0, logits.Length);
                Array.Reverse(rentedBuffer, 0, logits.Length); // Now in descending order
                float kthValue = rentedBuffer[Math.Min(k - 1, logits.Length - 1)];
                
                // Set all values below k-th to -inf
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
            // Reuse probability buffer to reduce allocations
            if (_probabilityBuffer == null || _probabilityBuffer.Length != logits.Length)
            {
                _probabilityBuffer = new float[logits.Length];
            }
            
            // Find max for numerical stability
            float max = float.NegativeInfinity;
            for (int i = 0; i < logits.Length; i++)
            {
                if (logits[i] != float.NegativeInfinity)
                {
                    max = Math.Max(max, logits[i]);
                }
            }
            
            // Compute exp and sum
            float sum = 0;
            for (int i = 0; i < logits.Length; i++)
            {
                if (logits[i] != float.NegativeInfinity)
                {
                    _probabilityBuffer[i] = MathF.Exp(logits[i] - max);
                    sum += _probabilityBuffer[i];
                }
                else
                {
                    _probabilityBuffer[i] = 0;
                }
            }
            
            // Normalize
            if (sum > 0)
            {
                for (int i = 0; i < _probabilityBuffer.Length; i++)
                {
                    _probabilityBuffer[i] /= sum;
                }
            }
            
            return _probabilityBuffer;
        }
        
        private int SampleFromProbs(float[] probs)
        {
            double target = _deterministicRng?.NextDouble() ?? _random.NextDouble();
            double cumSum = 0.0;
            
            for (int i = 0; i < probs.Length; i++)
            {
                cumSum += probs[i];
                if (cumSum >= target)
                {
                    return i;
                }
            }
            
            // Fallback
            return probs.Length - 1;
        }
        
        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(InferenceSession));
            }
        }
        
        public void Dispose()
        {
            if (!_disposed)
            {
                _probabilityBuffer = null;
                _disposed = true;
            }
        }
    }
}
