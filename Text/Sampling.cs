using System;
using System.Collections.Generic;
using TinyLLM.Core;

namespace TinyLLM.Text
{
    /// <summary>
    /// Implements text generation with greedy decoding, temperature sampling, and top-k filtering.
    /// Pure C# implementation.
    /// </summary>
    public class Sampling
    {
        private readonly TransformerModel _model;
        private readonly Tokenizer _tokenizer;
        private readonly int _blockSize;
        
        // Reusable buffers to reduce allocations
        private float[]? _probabilityBuffer;

        public Sampling(TransformerModel model, Tokenizer tokenizer, int blockSize)
        {
            _model = model;
            _tokenizer = tokenizer;
            _blockSize = blockSize;
        }

        /// <summary>
        /// Generate text from a prompt.
        /// </summary>
        public string Generate(string prompt, int maxNewTokens, double temperature = 1.0, int topK = 0, int? seed = null, bool showPerf = false, bool isPerfJsonMode = false, PerformanceMetrics? metrics = null)
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
                List<int> contextCropped;
                if (context.Count <= _blockSize)
                {
                    contextCropped = context;
                }
                else
                {
                    // Manual copy of last blockSize tokens (faster than Skip().ToList())
                    contextCropped = new List<int>(_blockSize);
                    int startIdx = context.Count - _blockSize;
                    for (int idx = startIdx; idx < context.Count; idx++)
                    {
                        contextCropped.Add(context[idx]);
                    }
                }

                // Convert to tensor: (1, T)
                var contextData = new float[contextCropped.Count];
                for (int j = 0; j < contextCropped.Count; j++)
                {
                    contextData[j] = contextCropped[j];
                }
                var contextTensor = new Tensor(contextData, new int[] { 1, contextCropped.Count });

                // Forward pass: (1, T, vocab_size)
                var logits = _model.Forward(contextTensor);

                // Get logits for the last position: (vocab_size,)
                // logits shape: (1, T, vocab_size), we want position (0, T-1, :)
                int T = contextCropped.Count;
                int vocabSize = logits.Shape[2];
                var logitsLast = new float[vocabSize];
                int lastPosOffset = (T - 1) * vocabSize; // Offset for last position in batch 0
                for (int v = 0; v < vocabSize; v++)
                {
                    logitsLast[v] = logits.Data[lastPosOffset + v];
                }

                // Apply temperature
                if (temperature != 1.0)
                {
                    for (int v = 0; v < vocabSize; v++)
                    {
                        logitsLast[v] /= (float)temperature;
                    }
                }

                // Apply top-k filtering
                if (topK > 0)
                {
                    logitsLast = ApplyTopK(logitsLast, topK);
                }

                // Convert to probabilities (softmax)
                var probs = Softmax(logitsLast);

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
        /// Apply top-k filtering to logits
        /// </summary>
        private float[] ApplyTopK(float[] logits, int k)
        {
            if (k >= logits.Length)
            {
                return logits;
            }

            // Find k-th largest value using partial sort (QuickSelect-like approach)
            // Copy to avoid modifying original
            var values = new float[logits.Length];
            Array.Copy(logits, values, logits.Length);
            
            // Partial sort - only need to find k-th largest
            Array.Sort(values);
            Array.Reverse(values); // Now in descending order
            float kthValue = values[Math.Min(k - 1, values.Length - 1)];

            // Set all values below k-th to -inf
            var filtered = new float[logits.Length];
            for (int i = 0; i < logits.Length; i++)
            {
                filtered[i] = logits[i] >= kthValue ? logits[i] : float.NegativeInfinity;
            }

            return filtered;
        }

        /// <summary>
        /// Compute softmax over an array (reuses buffer for performance)
        /// </summary>
        private float[] Softmax(float[] logits)
        {
            // Reuse probability buffer to reduce allocations
            if (_probabilityBuffer == null || _probabilityBuffer.Length != logits.Length)
            {
                _probabilityBuffer = new float[logits.Length];
            }
            
            // Find max for numerical stability
            float max = float.NegativeInfinity;
            foreach (var val in logits)
            {
                if (val != float.NegativeInfinity)
                {
                    max = Math.Max(max, val);
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
