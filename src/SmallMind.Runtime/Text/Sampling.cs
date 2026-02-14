using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;
using SmallMind.Abstractions.Telemetry;
using SmallMind.Core.Core;
using SmallMind.Tokenizers;
using SmallMind.Transformers;

namespace SmallMind.Runtime
{
    /// <summary>
    /// Implements text generation with greedy decoding, temperature sampling, and top-k filtering.
    /// Pure C# implementation.
    /// </summary>
    [Obsolete("Use InferenceSession instead. InferenceSession provides TopP, MinP, repetition penalties, output constraints, and async streaming. This class will be removed in v1.0.")]
    internal class Sampling
    {
        private readonly TransformerModel _model;
        private readonly ITokenizer _tokenizer;
        private readonly int _blockSize;

        // Reusable buffers to reduce allocations
        private float[]? _probabilityBuffer;
        private List<int>? _contextCroppedBuffer;
        private float[]? _logitsLastBuffer;
        private float[]? _topKFilteredBuffer;

        // KV-cache state for efficient decode
        private bool _kvCacheActive = false;
        private int _currentPosition = 0;

        // Reusable decode tensor for single-token generation
        private float[] _decodeData = new float[1];
        private int[] _decodeShape = new int[] { 1, 1 };
        private Tensor? _decodeTensor;

        public Sampling(TransformerModel model, ITokenizer tokenizer, int blockSize)
        {
            _model = model;
            _tokenizer = tokenizer;
            _blockSize = blockSize;
        }

        /// <summary>
        /// Generate text from a prompt.
        /// </summary>
        public string Generate(
            string prompt,
            int maxNewTokens,
            double temperature = 1.0,
            int topK = 0,
            double topP = 1.0,
            int? seed = null,
            bool showPerf = false,
            bool isPerfJsonMode = false,
            PerformanceMetrics? metrics = null,
            IRuntimeLogger? logger = null)
        {
            logger = logger ?? NullRuntimeLogger.Instance;

            _model.Eval();

            Random random;
            if (seed.HasValue)
            {
                random = new Random(seed.Value);
            }
            else
            {
                random = new Random();
            }

            // Encode the prompt
            var context = _tokenizer.Encode(prompt);
            if (context.Count == 0)
            {
                logger.Warn("Empty prompt, starting with empty context");
                context = new List<int> { 0 }; // Start with first token in vocab
            }

            int inputTokens = context.Count;

            // Initialize explainability sink if provided
            // Use provided metrics or create new one if perf tracking is enabled
            bool isMetricsOwner = false;
            if (metrics == null && (showPerf || isPerfJsonMode))
            {
                metrics = new PerformanceMetrics();
                isMetricsOwner = true;
            }

            if (!isPerfJsonMode)
            {
                logger.Info($"Generating {maxNewTokens} tokens...");
                logger.Info($"Temperature: {temperature}, Top-k: {topK}, Top-p: {topP}");
                logger.Info($"Prompt: \"{prompt}\"");
                if (showPerf)
                {
                    logger.Info("Performance tracking enabled");
                }
                logger.Info("---");
            }

            var totalStopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Start metrics tracking
            int requestId = -1;
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

            try
            {
                for (int i = 0; i < maxNewTokens; i++)
                {
                    Tensor logits;

                    if (!_kvCacheActive)
                    {
                        // PREFILL PHASE: Process full prompt to populate KV cache

                        // Reset and enable KV-cache
                        _model.ResetKVCache();
                        _model.EnableKVCache();

                        // Crop context to last blockSize tokens (avoid LINQ allocation)
                        List<int> contextCropped;
                        if (context.Count <= _blockSize)
                        {
                            contextCropped = context;
                        }
                        else
                        {
                            // Reuse buffer instead of allocating new List
                            if (_contextCroppedBuffer == null)
                            {
                                _contextCroppedBuffer = new List<int>(_blockSize);
                            }
                            else
                            {
                                _contextCroppedBuffer.Clear();
                            }

                            int startIdx = context.Count - _blockSize;
                            for (int idx = startIdx; idx < context.Count; idx++)
                            {
                                _contextCroppedBuffer.Add(context[idx]);
                            }
                            contextCropped = _contextCroppedBuffer;
                        }

                        int promptLength = contextCropped.Count;

                        // Build tensor from full prompt: shape (1, promptLength)
                        var prefillData = new float[promptLength];
                        for (int j = 0; j < promptLength; j++)
                        {
                            prefillData[j] = contextCropped[j];
                        }
                        var prefillTensor = new Tensor(prefillData, new int[] { 1, promptLength });

                        // Forward pass with position offset 0
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

                    // Get logits for the last position: (vocab_size,)
                    // logits shape: (1, T, vocab_size), we want position (0, T-1, :)
                    int T = logits.Shape[1];  // Sequence length (1 for decode, promptLength for prefill)
                    int vocabSize = logits.Shape[2];

                    // Reuse logitsLast buffer
                    int bufferLength = _logitsLastBuffer?.Length ?? 0;
                    if (bufferLength < vocabSize)
                    {
                        _logitsLastBuffer = new float[vocabSize];
                        bufferLength = vocabSize;
                    }

                    int lastPosOffset = (T - 1) * vocabSize; // Offset for last position in batch 0
                    // JIT can eliminate bounds checks when loop bound matches buffer length
                    for (int v = 0; v < bufferLength; v++)
                    {
                        _logitsLastBuffer[v] = logits.Data[lastPosOffset + v];
                    }

                    // Apply temperature - operate on buffer directly with SIMD
                    if (temperature != 1.0)
                    {
                        ApplyTemperatureSIMD(_logitsLastBuffer, bufferLength, (float)temperature);
                    }

                    // Apply top-k filtering - returns reference to buffer or filtered buffer
                    float[] logitsToSample = _logitsLastBuffer;
                    if (topK > 0)
                    {
                        logitsToSample = ApplyTopK(_logitsLastBuffer, vocabSize, topK);
                    }

                    // Convert to probabilities (softmax)
                    var probs = Softmax(logitsToSample, vocabSize);

                    // Apply top-p (nucleus) filtering if configured
                    if (topP < 1.0)
                    {
                        probs = ApplyTopP(probs, vocabSize, topP);
                    }

                    // Sample from the distribution
                    var nextToken = SampleFromProbs(probs, random);

                    // Add to context
                    context.Add(nextToken);

                    // Record first token for TTFT metric
                    if (metrics != null && !firstTokenRecorded)
                    {
                        metrics.RecordFirstToken(requestId);
                        firstTokenRecorded = true;
                    }

                    // Optional: print progress
                    if (!showPerf && !isPerfJsonMode && (i + 1) % 50 == 0)
                    {
                        logger.Debug(".");
                    }
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

            totalStopwatch.Stop();

            // Record completion
            if (metrics != null)
            {
                metrics.RecordRequestComplete(requestId, inputTokens, maxNewTokens, success: true);
            }

            if (!showPerf && !isPerfJsonMode && maxNewTokens >= 50)
            {
                logger.Debug(""); // New line after progress dots
            }

            // Output performance metrics
            if (isMetricsOwner && metrics != null)
            {
                metrics.Stop();
                var summary = metrics.GetSummary(maxTokensRequested: maxNewTokens, concurrencyLevel: 1);

                if (isPerfJsonMode)
                {
                    // JSON output only
                    logger.Info(MetricsFormatter.FormatJson(summary));
                }
                else if (showPerf)
                {
                    // Text output
                    logger.Info(MetricsFormatter.FormatText(summary));
                }
            }

            // Decode and return
            return _tokenizer.Decode(context);
        }

        /// <summary>
        /// Apply top-k filtering to logits (in-place when possible, using reusable buffer)
        /// </summary>
        private float[] ApplyTopK(float[] logits, int logitsLength, int k)
        {
            if (k >= logitsLength)
            {
                return logits;
            }

            // Rent buffer for sorting to reduce allocations
            float[]? rentedBuffer = null;
            try
            {
                rentedBuffer = System.Buffers.ArrayPool<float>.Shared.Rent(logitsLength);
                Array.Copy(logits, rentedBuffer, logitsLength);

                // Partial sort - only need to find k-th largest
                Array.Sort(rentedBuffer, 0, logitsLength);
                Array.Reverse(rentedBuffer, 0, logitsLength); // Now in descending order
                float kthValue = rentedBuffer[Math.Min(k - 1, logitsLength - 1)];

                // Reuse filtered buffer instead of allocating new array
                if (_topKFilteredBuffer == null || _topKFilteredBuffer.Length < logitsLength)
                {
                    _topKFilteredBuffer = new float[logitsLength];
                }

                // Set all values below k-th to -inf
                for (int i = 0; i < logitsLength; i++)
                {
                    _topKFilteredBuffer[i] = logits[i] >= kthValue ? logits[i] : float.NegativeInfinity;
                }

                return _topKFilteredBuffer;
            }
            finally
            {
                if (rentedBuffer != null)
                {
                    System.Buffers.ArrayPool<float>.Shared.Return(rentedBuffer);
                }
            }
        }

        /// <summary>
        /// Apply top-p (nucleus) sampling to probabilities.
        /// Keeps only the smallest set of tokens whose cumulative probability exceeds topP.
        /// Re-normalizes the filtered distribution.
        /// </summary>
        /// <param name="probs">Probability distribution (must sum to 1.0)</param>
        /// <param name="probsLength">Length of the probability array</param>
        /// <param name="topP">Cumulative probability threshold (0.0 to 1.0)</param>
        /// <returns>Filtered and re-normalized probability distribution</returns>
        private float[] ApplyTopP(float[] probs, int probsLength, double topP)
        {
            // Sort probabilities in descending order while tracking original indices
            // Use ArrayPool to avoid allocation
            var sortedProbs = System.Buffers.ArrayPool<float>.Shared.Rent(probsLength);
            var sortedIndices = System.Buffers.ArrayPool<int>.Shared.Rent(probsLength);

            try
            {
                // Initialize with probabilities and indices
                for (int i = 0; i < probsLength; i++)
                {
                    sortedProbs[i] = probs[i];
                    sortedIndices[i] = i;
                }

                // Sort descending by probability (using simple bubble sort for small vocab or insertion sort)
                // For better performance with large vocab, could use Array.Sort with custom comparer
                Array.Sort(sortedProbs, sortedIndices, 0, probsLength);
                Array.Reverse(sortedProbs, 0, probsLength);
                Array.Reverse(sortedIndices, 0, probsLength);

                // Find the cutoff index where cumulative probability exceeds topP
                float cumSum = 0.0f;
                int cutoffIndex = probsLength;

                for (int i = 0; i < probsLength; i++)
                {
                    cumSum += sortedProbs[i];
                    if (cumSum >= topP)
                    {
                        cutoffIndex = i + 1; // Include this token
                        break;
                    }
                }

                // Zero out probabilities below the cutoff
                for (int i = 0; i < probsLength; i++)
                {
                    probs[i] = 0.0f;
                }

                // Keep only top-p tokens and re-normalize
                float newSum = 0.0f;
                for (int i = 0; i < cutoffIndex; i++)
                {
                    int originalIndex = sortedIndices[i];
                    probs[originalIndex] = sortedProbs[i];
                    newSum += sortedProbs[i];
                }

                // Re-normalize to ensure probabilities sum to 1.0
                if (newSum > 0.0f)
                {
                    for (int i = 0; i < probsLength; i++)
                    {
                        if (probs[i] > 0.0f)
                        {
                            probs[i] /= newSum;
                        }
                    }
                }

                return probs;
            }
            finally
            {
                System.Buffers.ArrayPool<float>.Shared.Return(sortedProbs);
                System.Buffers.ArrayPool<int>.Shared.Return(sortedIndices);
            }
        }

        /// <summary>
        /// Apply temperature scaling to logits using SIMD acceleration.
        /// Divides each logit by the temperature value for sampling control.
        /// Lower temperature = more deterministic, higher = more random.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ApplyTemperatureSIMD(float[] logits, int length, float temperature)
        {
            float invTemp = 1.0f / temperature;
            int i = 0;

            // AVX-512 path (16 floats per iteration)
            if (Avx512F.IsSupported && length >= 16)
            {
                var vInvTemp = Vector512.Create(invTemp);
                unsafe
                {
                    fixed (float* pLogits = logits)
                    {
                        for (; i <= length - 16; i += 16)
                        {
                            var v = Avx512F.LoadVector512(pLogits + i);
                            Avx512F.Store(pLogits + i, Avx512F.Multiply(v, vInvTemp));
                        }
                    }
                }
            }
            // ARM NEON path (4 floats per iteration)
            else if (AdvSimd.Arm64.IsSupported && length >= 4)
            {
                var vInvTemp = Vector128.Create(invTemp);
                unsafe
                {
                    fixed (float* pLogits = logits)
                    {
                        for (; i <= length - 4; i += 4)
                        {
                            var v = AdvSimd.LoadVector128(pLogits + i);
                            AdvSimd.Store(pLogits + i, AdvSimd.Multiply(v, vInvTemp));
                        }
                    }
                }
            }

            // Vector<T> fallback for remaining elements
            if (Vector.IsHardwareAccelerated)
            {
                var vInvTemp = new Vector<float>(invTemp);
                int vectorSize = Vector<float>.Count;

                unsafe
                {
                    fixed (float* pLogits = logits)
                    {
                        for (; i <= length - vectorSize; i += vectorSize)
                        {
                            var v = Unsafe.Read<Vector<float>>(pLogits + i);
                            Unsafe.Write(pLogits + i, v * vInvTemp);
                        }
                    }
                }
            }

            // Scalar remainder
            for (; i < length; i++)
            {
                logits[i] *= invTemp;
            }
        }

        /// <summary>
        /// SIMD-optimized Softmax operation - converts logits to probabilities.
        /// Uses hardware intrinsics for max-finding and normalization.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float[] Softmax(float[] logits, int logitsLength)
        {
            // Reuse probability buffer to reduce allocations
            // Allocate new buffer if size doesn't match to avoid stale data
            if (_probabilityBuffer == null || _probabilityBuffer.Length != logitsLength)
            {
                _probabilityBuffer = new float[logitsLength];
            }

            // Step 1: Find max with SIMD acceleration for numerical stability
            int i = 0;
            float max = float.NegativeInfinity;

            // AVX-512 max finding (16 floats)
            if (Avx512F.IsSupported && logitsLength >= 16)
            {
                unsafe
                {
                    fixed (float* pLogits = logits)
                    {
                        var maxVec512 = Vector512.Create(float.NegativeInfinity);
                        for (; i <= logitsLength - 16; i += 16)
                        {
                            var v = Avx512F.LoadVector512(pLogits + i);
                            maxVec512 = Avx512F.Max(maxVec512, v);
                        }

                        // Reduce 512 -> 256 -> scalar
                        var upper = Avx512F.ExtractVector256(maxVec512, 1);
                        var lower = Avx512F.ExtractVector256(maxVec512, 0);
                        var maxVec256 = Avx.Max(upper, lower);

                        float* temp = stackalloc float[8];
                        Avx.Store(temp, maxVec256);
                        for (int j = 0; j < 8; j++)
                        {
                            if (temp[j] > max && temp[j] != float.NegativeInfinity)
                                max = temp[j];
                        }
                    }
                }
            }

            // Vector<T> fallback for max finding
            if (Vector.IsHardwareAccelerated)
            {
                var maxVec = new Vector<float>(max);
                int vectorSize = Vector<float>.Count;
                for (; i <= logitsLength - vectorSize; i += vectorSize)
                {
                    var v = new Vector<float>(logits, i);
                    maxVec = Vector.Max(maxVec, v);
                }

                // Horizontal max reduction
                for (int j = 0; j < vectorSize; j++)
                {
                    if (maxVec[j] > max && maxVec[j] != float.NegativeInfinity)
                        max = maxVec[j];
                }
            }

            // Scalar remainder for max
            for (; i < logitsLength; i++)
            {
                if (logits[i] != float.NegativeInfinity && logits[i] > max)
                {
                    max = logits[i];
                }
            }

            // Step 2: Compute exp(x - max) and sum
            // NOTE: exp() has no SIMD intrinsic, remains scalar
            float sum = 0f;
            for (i = 0; i < logitsLength; i++)
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

            // Step 3: Normalize with SIMD
            if (sum > 0)
            {
                float invSum = 1.0f / sum;
                i = 0;

                // AVX-512 normalization (16 floats)
                if (Avx512F.IsSupported && logitsLength >= 16)
                {
                    var vInvSum = Vector512.Create(invSum);
                    unsafe
                    {
                        fixed (float* pProbs = _probabilityBuffer)
                        {
                            for (; i <= logitsLength - 16; i += 16)
                            {
                                var v = Avx512F.LoadVector512(pProbs + i);
                                Avx512F.Store(pProbs + i, Avx512F.Multiply(v, vInvSum));
                            }
                        }
                    }
                }

                // Vector<T> fallback for normalization
                if (Vector.IsHardwareAccelerated)
                {
                    var vInvSum = new Vector<float>(invSum);
                    int vectorSize = Vector<float>.Count;

                    unsafe
                    {
                        fixed (float* pProbs = _probabilityBuffer)
                        {
                            for (; i <= logitsLength - vectorSize; i += vectorSize)
                            {
                                var v = Unsafe.Read<Vector<float>>(pProbs + i);
                                Unsafe.Write(pProbs + i, v * vInvSum);
                            }
                        }
                    }
                }

                // Scalar remainder
                for (; i < logitsLength; i++)
                {
                    _probabilityBuffer[i] *= invSum;
                }
            }

            return _probabilityBuffer;
        }

        /// <summary>
        /// Sample a token index from a probability distribution
        /// </summary>
        private int SampleFromProbs(float[] probs, Random random)
        {
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

            // Fallback
            return probs.Length - 1;
        }

    }
}
