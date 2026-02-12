using SmallMind.Tokenizers;
using SmallMind.Core.Core;
using SmallMind.Transformers;
using System;
using System.Collections.Generic;

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
            PerformanceMetrics? metrics = null)
        {
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
                Console.WriteLine("Warning: Empty prompt, starting with empty context");
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
                Console.WriteLine($"\nGenerating {maxNewTokens} tokens...");
                Console.WriteLine($"Temperature: {temperature}, Top-k: {topK}, Top-p: {topP}");
                Console.WriteLine($"Prompt: \"{prompt}\"");
                if (showPerf)
                {
                    Console.WriteLine("Performance tracking enabled");
                }
                Console.WriteLine("---");
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

                    // Apply temperature - operate on buffer directly
                    if (temperature != 1.0)
                    {
                        for (int v = 0; v < bufferLength; v++)
                        {
                            _logitsLastBuffer[v] /= (float)temperature;
                        }
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
                        Console.Write(".");
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
                Console.WriteLine(); // New line after progress dots
            }

            // Output performance metrics
            if (isMetricsOwner && metrics != null)
            {
                metrics.Stop();
                var summary = metrics.GetSummary(maxTokensRequested: maxNewTokens, concurrencyLevel: 1);
                
                if (isPerfJsonMode)
                {
                    // JSON output only
                    Console.WriteLine(MetricsFormatter.FormatJson(summary));
                }
                else if (showPerf)
                {
                    // Text output
                    Console.WriteLine(MetricsFormatter.FormatText(summary));
                }
            }

            // Decode and return
            var generated = _tokenizer.Decode(context);
            return generated;
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
        /// Compute softmax over an array (reuses buffer for performance)
        /// </summary>
        private float[] Softmax(float[] logits, int logitsLength)
        {
            // Reuse probability buffer to reduce allocations
            // Allocate new buffer if size doesn't match to avoid stale data
            if (_probabilityBuffer == null || _probabilityBuffer.Length != logitsLength)
            {
                _probabilityBuffer = new float[logitsLength];
            }
            
            // Find max for numerical stability
            float max = float.NegativeInfinity;
            for (int i = 0; i < logitsLength; i++)
            {
                if (logits[i] != float.NegativeInfinity)
                {
                    max = MathF.Max(max, logits[i]);
                }
            }

            // Compute exp and sum
            float sum = 0;
            for (int i = 0; i < logitsLength; i++)
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
                for (int i = 0; i < logitsLength; i++)
                {
                    _probabilityBuffer[i] /= sum;
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
