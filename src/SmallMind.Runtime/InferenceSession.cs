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
        
        // Additional buffers for Top-P and Min-P sampling (reused per token, zero allocation)
        private int[]? _sortedIndicesBuffer;
        private float[]? _sortedProbsBuffer;
        
        // Repetition penalty tracking (sparse, reusable across tokens)
        private int[]? _seenTokenIds;
        private int[]? _seenCounts;
        private int _seenTokensCount = 0;
        
        // Stop sequence detection sliding window (only allocated if stop sequences configured)
        private char[]? _stopSequenceBuffer;
        private int _stopSequenceBufferPos = 0;
        private int _stopSequenceBufferSize = 0;
        
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
                FinishReason finishReason = FinishReason.None;
                
                // Generate tokens
                for (int i = 0; i < _options.MaxNewTokens; i++)
                {
                    effectiveToken.ThrowIfCancellationRequested();
                    
                    // Check context limit
                    if (_options.MaxContextTokens > 0 && context.Count >= _options.MaxContextTokens)
                    {
                        finishReason = FinishReason.MaxContext;
                        break;
                    }
                    
                    // Check block size limit to prevent exceeding model's positional embeddings
                    if (context.Count >= _blockSize)
                    {
                        finishReason = FinishReason.MaxContext;
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
                    
                    // Check for stop token (EOS or configured stop tokens)
                    if (IsStopToken(nextToken, out var stopReason))
                    {
                        finishReason = stopReason;
                        break;
                    }
                    
                    // Check for stop sequences (text-based)
                    if (_options.StopSequences.Length > 0)
                    {
                        // Decode just the new token to check stop sequence
                        var tokenText = _tokenizer.Decode(new List<int> { nextToken });
                        if (CheckStopSequence(tokenText))
                        {
                            finishReason = FinishReason.StopSequence;
                            break;
                        }
                    }
                }
                
                // Set finish reason if loop completed normally
                if (finishReason == FinishReason.None)
                {
                    finishReason = FinishReason.MaxTokens;
                }
                
                // Record completion
                if (metrics != null)
                {
                    metrics.RecordRequestComplete(requestId, inputTokens, context.Count - inputTokens, success: true);
                }
                
                // Decode and return (optionally remove stop sequence)
                var result = _tokenizer.Decode(context);
                
                if (finishReason == FinishReason.StopSequence && _options.RemoveStopSequenceFromOutput)
                {
                    // Find and remove the stop sequence from the end
                    for (int i = 0; i < _options.StopSequences.Length; i++)
                    {
                        if (result.EndsWith(_options.StopSequences[i]))
                        {
                            result = result.Substring(0, result.Length - _options.StopSequences[i].Length);
                            break;
                        }
                    }
                }
                
                return result;
            }
            catch (OperationCanceledException) when (timeoutCts?.IsCancellationRequested == true)
            {
                // Timeout occurred - throw timeout exception
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
                FinishReason finishReason = FinishReason.None;
                
                // Generate tokens
                for (int i = 0; i < _options.MaxNewTokens; i++)
                {
                    // Check for timeout
                    if (effectiveToken.IsCancellationRequested)
                    {
                        if (timeoutCts?.IsCancellationRequested == true)
                        {
                            finishReason = FinishReason.Timeout;
                            // Yield final token with timeout reason
                            yield return new GeneratedToken(
                                tokenId: -1,
                                text: "",
                                index: i,
                                logProb: null,
                                finishReason: FinishReason.Timeout
                            );
                            break;
                        }
                        effectiveToken.ThrowIfCancellationRequested();
                    }
                    
                    // Check context limit
                    if (_options.MaxContextTokens > 0 && context.Count >= _options.MaxContextTokens)
                    {
                        finishReason = FinishReason.MaxContext;
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
                    
                    // Check for stop token
                    bool isStop = IsStopToken(nextToken, out var stopReason);
                    
                    // Check for stop sequences
                    bool isStopSeq = false;
                    if (_options.StopSequences.Length > 0)
                    {
                        isStopSeq = CheckStopSequence(tokenText);
                        if (isStopSeq)
                        {
                            stopReason = FinishReason.StopSequence;
                        }
                    }
                    
                    // Determine finish reason for this token
                    var tokenFinishReason = FinishReason.None;
                    if (isStop || isStopSeq)
                    {
                        tokenFinishReason = stopReason;
                        finishReason = stopReason;
                    }
                    else if (i == _options.MaxNewTokens - 1)
                    {
                        tokenFinishReason = FinishReason.MaxTokens;
                        finishReason = FinishReason.MaxTokens;
                    }
                    
                    yield return new GeneratedToken(
                        tokenId: nextToken,
                        text: tokenText,
                        index: i,
                        logProb: null,
                        finishReason: tokenFinishReason
                    );
                    
                    // Stop if we hit a stop condition
                    if (isStop || isStopSeq)
                    {
                        break;
                    }
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
            
            // SAMPLING PIPELINE (order matters):
            
            // 1. Apply repetition/presence/frequency penalties (before temperature)
            ApplyRepetitionPenalties(logitsLast, context);
            
            // 2. Apply temperature scaling
            if (_options.Temperature != 1.0)
            {
                for (int v = 0; v < vocabSize; v++)
                {
                    logitsLast[v] /= (float)_options.Temperature;
                }
            }
            
            // 3. Apply top-k filtering
            if (_options.TopK > 0)
            {
                logitsLast = ApplyTopK(logitsLast, _options.TopK);
            }
            
            // 4. Convert to probabilities (softmax)
            var probs = Softmax(logitsLast);
            
            // 5. Apply top-p (nucleus) sampling
            if (_options.TopP < 1.0)
            {
                ApplyTopP(probs, _options.TopP);
            }
            
            // 6. Apply min-p sampling
            if (_options.MinP > 0.0)
            {
                ApplyMinP(probs, _options.MinP);
            }
            
            // 6.5. Apply output constraints (Phase 5)
            if (_options.OutputConstraint != null)
            {
                ApplyOutputConstraints(logitsLast, context);
                // Recompute probabilities after constraint masking
                probs = Softmax(logitsLast);
            }
            
            // 7. Sample from the distribution
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
        /// Apply repetition penalties to logits based on tokens in recent context window.
        /// Modifies logits in-place. Zero allocations after first call.
        /// </summary>
        private void ApplyRepetitionPenalties(float[] logits, List<int> context)
        {
            // Early exit if no penalties enabled
            if (_options.RepetitionPenalty == 1.0f && 
                _options.PresencePenalty == 0.0f && 
                _options.FrequencyPenalty == 0.0f)
            {
                return;
            }
            
            // Determine window size (default to 256 if not specified, or full context if smaller)
            int windowSize = _options.RepetitionWindow > 0 
                ? _options.RepetitionWindow 
                : Math.Min(context.Count, 256);
            int startIdx = Math.Max(0, context.Count - windowSize);
            int actualWindow = context.Count - startIdx;
            
            // Allocate sparse tracking structures on first use (reused across tokens)
            if (_seenTokenIds == null || _seenTokenIds.Length < actualWindow)
            {
                _seenTokenIds = new int[actualWindow];
                _seenCounts = new int[actualWindow];
            }
            
            // Build frequency map using sparse arrays (no dictionary allocations)
            _seenTokensCount = 0;
            for (int i = startIdx; i < context.Count; i++)
            {
                int tokenId = context[i];
                
                // Linear search in sparse array (fast for small windows)
                int foundIdx = -1;
                for (int j = 0; j < _seenTokensCount; j++)
                {
                    if (_seenTokenIds![j] == tokenId)
                    {
                        foundIdx = j;
                        break;
                    }
                }
                
                if (foundIdx >= 0)
                {
                    _seenCounts![foundIdx]++;
                }
                else
                {
                    // Add new token
                    _seenTokenIds![_seenTokensCount] = tokenId;
                    _seenCounts![_seenTokensCount] = 1;
                    _seenTokensCount++;
                }
            }
            
            // Apply penalties to logits
            for (int i = 0; i < _seenTokensCount; i++)
            {
                int tokenId = _seenTokenIds![i];
                int count = _seenCounts![i];
                
                // Apply repetition penalty
                if (_options.RepetitionPenalty != 1.0f)
                {
                    if (logits[tokenId] > 0)
                    {
                        logits[tokenId] /= _options.RepetitionPenalty;
                    }
                    else
                    {
                        logits[tokenId] *= _options.RepetitionPenalty;
                    }
                }
                
                // Apply presence penalty
                if (_options.PresencePenalty != 0.0f)
                {
                    logits[tokenId] -= _options.PresencePenalty;
                }
                
                // Apply frequency penalty
                if (_options.FrequencyPenalty != 0.0f)
                {
                    logits[tokenId] -= count * _options.FrequencyPenalty;
                }
            }
        }
        
        /// <summary>
        /// Apply Top-P (nucleus) sampling to probabilities.
        /// Modifies probs in-place to set pruned tokens to 0, then re-normalizes.
        /// Zero allocations after first call.
        /// </summary>
        private void ApplyTopP(float[] probs, double topP)
        {
            if (topP >= 1.0)
            {
                return; // No filtering
            }
            
            int vocabSize = probs.Length;
            
            // Allocate index and value buffers on first use (reused across tokens)
            if (_sortedIndicesBuffer == null || _sortedIndicesBuffer.Length < vocabSize)
            {
                _sortedIndicesBuffer = new int[vocabSize];
                _sortedProbsBuffer = new float[vocabSize];
            }
            
            // Build index array and copy probabilities
            for (int i = 0; i < vocabSize; i++)
            {
                _sortedIndicesBuffer[i] = i;
                _sortedProbsBuffer![i] = probs[i];
            }
            
            // Sort indices by probability (descending) using Array.Sort with custom comparison
            // This is an in-place sort with minimal allocations
            Array.Sort(_sortedProbsBuffer, _sortedIndicesBuffer, 0, vocabSize, 
                Comparer<float>.Create((a, b) => b.CompareTo(a))); // Descending
            
            // Find cutoff index where cumulative probability >= topP
            float cumProb = 0.0f;
            int cutoffIndex = vocabSize;
            for (int i = 0; i < vocabSize; i++)
            {
                cumProb += _sortedProbsBuffer![i];
                if (cumProb >= topP)
                {
                    cutoffIndex = i + 1; // Include this token
                    break;
                }
            }
            
            // Zero out tokens beyond cutoff
            for (int i = cutoffIndex; i < vocabSize; i++)
            {
                int tokenIdx = _sortedIndicesBuffer[i];
                probs[tokenIdx] = 0.0f;
            }
            
            // Re-normalize
            float sum = 0.0f;
            for (int i = 0; i < vocabSize; i++)
            {
                sum += probs[i];
            }
            
            if (sum > 0)
            {
                for (int i = 0; i < vocabSize; i++)
                {
                    probs[i] /= sum;
                }
            }
        }
        
        /// <summary>
        /// Apply Min-P sampling to probabilities.
        /// Removes tokens with probability less than minP * max_probability.
        /// Modifies probs in-place.
        /// </summary>
        private void ApplyMinP(float[] probs, double minP)
        {
            if (minP <= 0.0)
            {
                return; // Disabled
            }
            
            // Find max probability
            float maxProb = 0.0f;
            for (int i = 0; i < probs.Length; i++)
            {
                if (probs[i] > maxProb)
                {
                    maxProb = probs[i];
                }
            }
            
            // Apply threshold
            float threshold = maxProb * (float)minP;
            for (int i = 0; i < probs.Length; i++)
            {
                if (probs[i] < threshold)
                {
                    probs[i] = 0.0f;
                }
            }
            
            // Re-normalize
            float sum = 0.0f;
            for (int i = 0; i < probs.Length; i++)
            {
                sum += probs[i];
            }
            
            if (sum > 0)
            {
                for (int i = 0; i < probs.Length; i++)
                {
                    probs[i] /= sum;
                }
            }
        }
        
        /// <summary>
        /// Apply output constraints to mask disallowed tokens (Phase 5).
        /// Modifies logits in-place by setting disallowed tokens to -infinity.
        /// </summary>
        private void ApplyOutputConstraints(float[] logits, List<int> context)
        {
            // Decode current context to get generated text so far
            string generatedSoFar = _tokenizer.Decode(context);
            
            int maskedCount = 0;
            int vocabSize = logits.Length;
            
            // Check each candidate token
            for (int tokenId = 0; tokenId < vocabSize; tokenId++)
            {
                // Skip already masked tokens
                if (float.IsNegativeInfinity(logits[tokenId]))
                {
                    continue;
                }
                
                // Decode candidate token
                string tokenText = _tokenizer.Decode(new List<int> { tokenId });
                
                // Check if token is allowed by constraint
                if (!_options.OutputConstraint!.IsTokenAllowed(generatedSoFar, tokenId, tokenText))
                {
                    logits[tokenId] = float.NegativeInfinity;
                    maskedCount++;
                }
            }
            
            // If all tokens masked, force structural tokens (JSON recovery)
            if (maskedCount == vocabSize || AllLogitsMasked(logits))
            {
                ForceStructuralToken(logits, generatedSoFar);
            }
        }
        
        /// <summary>
        /// Check if all logits are masked.
        /// </summary>
        private bool AllLogitsMasked(float[] logits)
        {
            for (int i = 0; i < logits.Length; i++)
            {
                if (!float.IsNegativeInfinity(logits[i]))
                {
                    return false;
                }
            }
            return true;
        }
        
        /// <summary>
        /// Force likely structural tokens when all options are masked.
        /// This helps JSON mode recovery when stuck.
        /// </summary>
        private void ForceStructuralToken(float[] logits, string generatedSoFar)
        {
            // Try to force closing braces/brackets based on JSON state
            try
            {
                // Attempt to encode common structural tokens
                var closeBrace = _tokenizer.Encode("}");
                var closeBracket = _tokenizer.Encode("]");
                var quote = _tokenizer.Encode("\"");
                var comma = _tokenizer.Encode(",");
                
                // Unmask these structural tokens
                if (closeBrace.Count > 0)
                    logits[closeBrace[0]] = 0.0f;
                if (closeBracket.Count > 0)
                    logits[closeBracket[0]] = 0.0f;
                if (quote.Count > 0)
                    logits[quote[0]] = 0.0f;
                if (comma.Count > 0)
                    logits[comma[0]] = 0.0f;
            }
            catch
            {
                // If encoding fails, just unmask first valid token
                for (int i = 0; i < logits.Length; i++)
                {
                    if (!float.IsNegativeInfinity(logits[i]))
                    {
                        logits[i] = 0.0f;
                        break;
                    }
                }
            }
        }
        
        /// <summary>
        /// Check if a token is a stop token (EOS or configured stop token).
        /// </summary>
        private bool IsStopToken(int tokenId, out FinishReason reason)
        {
            // Check EOS token
            if (_tokenizer.Info.EosTokenId >= 0 && tokenId == _tokenizer.Info.EosTokenId)
            {
                reason = FinishReason.EndOfSequence;
                return true;
            }
            
            // Check configured stop tokens (linear scan is fine for small arrays)
            if (_options.StopTokenIds.Length > 0)
            {
                for (int i = 0; i < _options.StopTokenIds.Length; i++)
                {
                    if (_options.StopTokenIds[i] == tokenId)
                    {
                        reason = FinishReason.StopToken;
                        return true;
                    }
                }
            }
            
            reason = FinishReason.None;
            return false;
        }
        
        /// <summary>
        /// Initialize stop sequence detection buffer.
        /// Only called once if stop sequences are configured.
        /// </summary>
        private void InitializeStopSequenceBuffer()
        {
            if (_options.StopSequences.Length == 0)
            {
                return;
            }
            
            // Find max stop sequence length
            int maxLen = 0;
            for (int i = 0; i < _options.StopSequences.Length; i++)
            {
                if (_options.StopSequences[i].Length > maxLen)
                {
                    maxLen = _options.StopSequences[i].Length;
                }
            }
            
            // Allocate buffer with margin (2x max length to handle overlaps)
            _stopSequenceBufferSize = maxLen * 2;
            _stopSequenceBuffer = new char[_stopSequenceBufferSize];
            _stopSequenceBufferPos = 0;
        }
        
        /// <summary>
        /// Add decoded text to stop sequence buffer and check for matches.
        /// Returns true if a stop sequence is found.
        /// </summary>
        private bool CheckStopSequence(string decodedText)
        {
            if (_options.StopSequences.Length == 0)
            {
                return false;
            }
            
            // Initialize buffer on first use
            if (_stopSequenceBuffer == null)
            {
                InitializeStopSequenceBuffer();
            }
            
            // Append decoded text to ring buffer
            for (int i = 0; i < decodedText.Length; i++)
            {
                _stopSequenceBuffer![_stopSequenceBufferPos] = decodedText[i];
                _stopSequenceBufferPos = (_stopSequenceBufferPos + 1) % _stopSequenceBufferSize;
            }
            
            // Build current window string (only as much as we've written)
            int totalWritten = Math.Min(_stopSequenceBufferPos, _stopSequenceBufferSize);
            bool hasWrapped = (_stopSequenceBufferPos >= _stopSequenceBufferSize);
            
            if (!hasWrapped && totalWritten > 0)
            {
                // Haven't wrapped yet, simple case
                var windowSpan = new ReadOnlySpan<char>(_stopSequenceBuffer, 0, _stopSequenceBufferPos);
                string window = new string(windowSpan);
                
                // Check each stop sequence
                for (int i = 0; i < _options.StopSequences.Length; i++)
                {
                    if (window.Contains(_options.StopSequences[i]))
                    {
                        return true;
                    }
                }
            }
            else if (hasWrapped)
            {
                // Buffer is full, need to check with wrap-around
                // Reconstruct the string from the ring buffer
                var reconstructed = new char[_stopSequenceBufferSize];
                for (int i = 0; i < _stopSequenceBufferSize; i++)
                {
                    int idx = (_stopSequenceBufferPos + i) % _stopSequenceBufferSize;
                    reconstructed[i] = _stopSequenceBuffer![idx];
                }
                string window = new string(reconstructed);
                
                // Check each stop sequence
                for (int i = 0; i < _options.StopSequences.Length; i++)
                {
                    if (window.Contains(_options.StopSequences[i]))
                    {
                        return true;
                    }
                }
            }
            
            return false;
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
                _sortedIndicesBuffer = null;
                _sortedProbsBuffer = null;
                _seenTokenIds = null;
                _seenCounts = null;
                _stopSequenceBuffer = null;
                _disposed = true;
            }
        }
    }
}
