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
    public class Sampling
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
            double minP = 0.0,
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
                Console.WriteLine($"Temperature: {temperature}, Top-k: {topK}, Top-p: {topP}, Min-p: {minP}");
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
                    if (_logitsLastBuffer == null || _logitsLastBuffer.Length < vocabSize)
                    {
                        _logitsLastBuffer = new float[vocabSize];
                    }
                    
                    int lastPosOffset = (T - 1) * vocabSize; // Offset for last position in batch 0
                    for (int v = 0; v < vocabSize; v++)
                    {
                        _logitsLastBuffer[v] = logits.Data[lastPosOffset + v];
                    }

                    // Apply temperature - operate on buffer directly
                    if (temperature != 1.0)
                    {
                        for (int v = 0; v < vocabSize; v++)
                        {
                            _logitsLastBuffer[v] /= (float)temperature;
                        }
                    }

                    // Apply sampling strategies in order: temperature -> top-k -> min-p -> top-p
                    float[] logitsToSample = _logitsLastBuffer;
                    
                    // Apply top-k filtering
                    if (topK > 0)
                    {
                        logitsToSample = ApplyTopK(_logitsLastBuffer, vocabSize, topK);
                    }

                    // Convert to probabilities (softmax) - needed for top-p and min-p
                    var probs = Softmax(logitsToSample, vocabSize);
                    
                    // Apply min-p filtering (remove tokens below min_p * max_prob)
                    if (minP > 0.0)
                    {
                        probs = ApplyMinP(probs, vocabSize, (float)minP);
                    }
                    
                    // Apply top-p (nucleus) filtering
                    if (topP < 1.0)
                    {
                        probs = ApplyTopP(probs, vocabSize, (float)topP);
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

        /// <summary>
        /// Apply top-p (nucleus) sampling - keep tokens with cumulative probability &lt;= p.
        /// Industry standard sampling method used in GPT-3, GPT-4, and most modern LLMs.
        /// </summary>
        /// <param name="probs">Probability distribution (must be normalized)</param>
        /// <param name="probsLength">Length of probability array</param>
        /// <param name="p">Cumulative probability threshold (0.0 to 1.0)</param>
        /// <returns>Filtered probability distribution</returns>
        private float[] ApplyTopP(float[] probs, int probsLength, float p)
        {
            if (p >= 1.0f || p <= 0.0f)
            {
                return probs;
            }

            // Create sorted indices by probability (descending)
            var indices = new int[probsLength];
            for (int i = 0; i < probsLength; i++)
            {
                indices[i] = i;
            }
            
            // Sort indices by probability (descending)
            Array.Sort(indices, (a, b) => probs[b].CompareTo(probs[a]));
            
            // Find cutoff index where cumulative probability exceeds p
            float cumProb = 0.0f;
            int cutoffIndex = probsLength;
            
            for (int i = 0; i < probsLength; i++)
            {
                cumProb += probs[indices[i]];
                if (cumProb > p)
                {
                    cutoffIndex = i + 1; // Include this token
                    break;
                }
            }
            
            // Create filtered distribution
            var filtered = new float[probsLength];
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
                for (int i = 0; i < probsLength; i++)
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
        /// <param name="probs">Probability distribution (must be normalized)</param>
        /// <param name="probsLength">Length of probability array</param>
        /// <param name="minP">Minimum probability threshold relative to max (0.0 to 1.0)</param>
        /// <returns>Filtered probability distribution</returns>
        private float[] ApplyMinP(float[] probs, int probsLength, float minP)
        {
            if (minP <= 0.0f)
            {
                return probs;
            }

            // Find maximum probability
            float maxProb = 0.0f;
            for (int i = 0; i < probsLength; i++)
            {
                if (probs[i] > maxProb)
                {
                    maxProb = probs[i];
                }
            }
            
            // Calculate threshold
            float threshold = minP * maxProb;
            
            // Filter and renormalize
            var filtered = new float[probsLength];
            float sumFiltered = 0.0f;
            
            for (int i = 0; i < probsLength; i++)
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
                for (int i = 0; i < probsLength; i++)
                {
                    if (filtered[i] > 0)
                    {
                        filtered[i] /= sumFiltered;
                    }
                }
            }
            
            return filtered;
        }

    }
}
