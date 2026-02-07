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
        
        // KV-cache state for efficient decode
        private bool _kvCacheActive = false;
        private int _currentPosition = 0;
        
        // Reusable decode tensor for single-token generation
        private float[] _decodeData = new float[1];
        private int[] _decodeShape = new int[] { 1, 1 };
        private Tensor? _decodeTensor;
        
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
        public async ValueTask<string> GenerateAsync(
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
                    
                    // Check block size limit to prevent exceeding model's positional embeddings
                    if (context.Count >= _blockSize)
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
            finally
            {
                // Cleanup: Disable KV-cache after generation completes
                if (_kvCacheActive)
                {
                    _model.DisableKVCache();
                    _kvCacheActive = false;
                    _currentPosition = 0;
                }
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
            finally
            {
                // Cleanup: Disable KV-cache after generation completes
                if (_kvCacheActive)
                {
                    _model.DisableKVCache();
                    _kvCacheActive = false;
                    _currentPosition = 0;
                }
            }
        }
        
        private async Task<int> GenerateNextTokenAsync(List<int> context, CancellationToken cancellationToken)
        {
            // Check cancellation before compute
            cancellationToken.ThrowIfCancellationRequested();
            
            Tensor logits;
            
            if (!_kvCacheActive)
            {
                // PREFILL PHASE: Process full prompt to populate KV cache
                
                // Reset and enable KV-cache
                _model.ResetKVCache();
                _model.EnableKVCache();
                
                // Crop context to last blockSize tokens if needed
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
                
                int promptLength = contextCropped.Count;
                
                // Build tensor from full prompt: shape (1, promptLength)
                // Use exact allocation for Tensor (validates data.Length == shape product)
                var prefillData = new float[promptLength];
                for (int j = 0; j < promptLength; j++)
                {
                    prefillData[j] = contextCropped[j];
                }
                var prefillTensor = new Tensor(prefillData, new int[] { 1, promptLength });
                
                // Forward pass with position offset 0 (start of sequence)
                logits = _model.Forward(prefillTensor, positionOffset: 0);
                
                // Set position for next decode step
                _currentPosition = promptLength;
                _kvCacheActive = true;
            }
            else
            {
                // DECODE PHASE: Single-token forward with KV cache
                
                // Get the last token from context
                int lastToken = context[context.Count - 1];
                
                // Reuse decode tensor (zero allocation)
                _decodeData[0] = lastToken;
                if (_decodeTensor == null)
                {
                    _decodeTensor = new Tensor(_decodeData, _decodeShape);
                }
                
                // Forward pass with current position offset
                logits = _model.Forward(_decodeTensor, positionOffset: _currentPosition);
                
                // Increment position for next token
                _currentPosition++;
            }
            
            // Get logits for last position
            int T = logits.Shape[1];  // Sequence length in output (1 for decode, promptLength for prefill)
            int vocabSize = logits.Shape[2];
            
            // Reuse logits buffer instead of allocating per token
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
            
            // Apply min-p filtering (remove tokens below min_p * max_prob)
            if (_options.MinP > 0.0)
            {
                probs = ApplyMinP(probs, (float)_options.MinP);
            }
            
            // Apply top-p (nucleus) filtering
            if (_options.TopP < 1.0)
            {
                probs = ApplyTopP(probs, (float)_options.TopP);
            }
            
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
                    max = MathF.Max(max, logits[i]);
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
        
        /// <summary>
        /// Apply top-p (nucleus) sampling - keep tokens with cumulative probability &lt;= p.
        /// Industry standard sampling method used in GPT-3, GPT-4, and most modern LLMs.
        /// </summary>
        private float[] ApplyTopP(float[] probs, float p)
        {
            if (p >= 1.0f || p <= 0.0f)
            {
                return probs;
            }

            // Create sorted indices by probability (descending)
            var indices = new int[probs.Length];
            for (int i = 0; i < probs.Length; i++)
            {
                indices[i] = i;
            }
            
            // Sort indices by probability (descending)
            Array.Sort(indices, (a, b) => probs[b].CompareTo(probs[a]));
            
            // Find cutoff index where cumulative probability exceeds p
            float cumProb = 0.0f;
            int cutoffIndex = probs.Length;
            
            for (int i = 0; i < probs.Length; i++)
            {
                cumProb += probs[indices[i]];
                if (cumProb > p)
                {
                    cutoffIndex = i + 1; // Include this token
                    break;
                }
            }
            
            // Create filtered distribution
            var filtered = new float[probs.Length];
            float sumFiltered = 0.0f;
            
            for (int i = 0; i < cutoffIndex; i++)
            {
                int idx = indices[i];
                filtered[idx] = probs[idx];
                sumFiltered += probs[idx];
            }
            
            // Renormalize the filtered distribution
            if (sumFiltered > 0)
            {
                for (int i = 0; i < probs.Length; i++)
                {
                    if (filtered[i] > 0)
                    {
                        filtered[i] /= sumFiltered;
                    }
                }
            }
            
            return filtered;
        }

        /// <summary>
        /// Apply min-p sampling - remove tokens with probability below min_p * max_probability.
        /// More adaptive than top-p as threshold adjusts based on confidence of top token.
        /// </summary>
        private float[] ApplyMinP(float[] probs, float minP)
        {
            if (minP <= 0.0f)
            {
                return probs;
            }

            // Find maximum probability
            float maxProb = 0.0f;
            for (int i = 0; i < probs.Length; i++)
            {
                if (probs[i] > maxProb)
                {
                    maxProb = probs[i];
                }
            }
            
            // Calculate threshold
            float threshold = minP * maxProb;
            
            // Filter and renormalize
            var filtered = new float[probs.Length];
            float sumFiltered = 0.0f;
            
            for (int i = 0; i < probs.Length; i++)
            {
                if (probs[i] >= threshold)
                {
                    filtered[i] = probs[i];
                    sumFiltered += probs[i];
                }
            }
            
            // Renormalize
            if (sumFiltered > 0)
            {
                for (int i = 0; i < probs.Length; i++)
                {
                    if (filtered[i] > 0)
                    {
                        filtered[i] /= sumFiltered;
                    }
                }
            }
            
            return filtered;
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
