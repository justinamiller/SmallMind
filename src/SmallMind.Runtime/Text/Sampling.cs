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
        // Note: Cannot reuse contextData or shape buffers as Tensor validates exact sizing and holds references

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
                Console.WriteLine($"Temperature: {temperature}, Top-k: {topK}");
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

            for (int i = 0; i < maxNewTokens; i++)
            {
                // Crop context to last blockSize tokens (avoid LINQ allocation)
                // Reuse buffer to eliminate per-token allocation
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

                // Convert to tensor: (1, T)
                // Allocate exact-size array (Tensor validates data.Length == shape size)
                int contextSize = contextCropped.Count;
                var contextData = new float[contextSize];
                for (int j = 0; j < contextSize; j++)
                {
                    contextData[j] = contextCropped[j];
                }
                
                // Note: Cannot reuse shape buffer as Tensor holds reference to it
                // These are small allocations (int[] and float[]) but unavoidable due to Tensor API design
                var contextTensor = new Tensor(contextData, new int[] { 1, contextSize });

                // Forward pass: (1, T, vocab_size)
                var logits = _model.Forward(contextTensor);

                // Get logits for the last position: (vocab_size,)
                // logits shape: (1, T, vocab_size), we want position (0, T-1, :)
                int T = contextSize;
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

                // Apply top-k filtering - returns reference to buffer or filtered buffer
                float[] logitsToSample = _logitsLastBuffer;
                if (topK > 0)
                {
                    logitsToSample = ApplyTopK(_logitsLastBuffer, vocabSize, topK);
                }

                // Convert to probabilities (softmax)
                var probs = Softmax(logitsToSample, vocabSize);

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
            if (_probabilityBuffer == null || _probabilityBuffer.Length < logitsLength)
            {
                _probabilityBuffer = new float[logitsLength];
            }
            
            // Find max for numerical stability
            float max = float.NegativeInfinity;
            for (int i = 0; i < logitsLength; i++)
            {
                if (logits[i] != float.NegativeInfinity)
                {
                    max = Math.Max(max, logits[i]);
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
