using System;
using System.Collections.Generic;
using System.Linq;

namespace TinyLLM
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

        public Sampling(TransformerModel model, Tokenizer tokenizer, int blockSize)
        {
            _model = model;
            _tokenizer = tokenizer;
            _blockSize = blockSize;
        }

        /// <summary>
        /// Generate text from a prompt.
        /// </summary>
        public string Generate(string prompt, int maxNewTokens, double temperature = 1.0, int topK = 0, int? seed = null, bool showPerf = false)
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

            Console.WriteLine($"\nGenerating {maxNewTokens} tokens...");
            Console.WriteLine($"Temperature: {temperature}, Top-k: {topK}");
            Console.WriteLine($"Prompt: \"{prompt}\"");
            if (showPerf)
            {
                Console.WriteLine("Performance tracking enabled");
            }
            Console.WriteLine("---");

            var totalStopwatch = System.Diagnostics.Stopwatch.StartNew();

            for (int i = 0; i < maxNewTokens; i++)
            {
                // Crop context to last blockSize tokens
                var contextCropped = context.Count <= _blockSize 
                    ? context 
                    : context.Skip(context.Count - _blockSize).ToList();

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

                // Optional: print progress
                if (!showPerf && (i + 1) % 50 == 0)
                {
                    Console.Write(".");
                }
            }

            totalStopwatch.Stop();

            if (!showPerf && maxNewTokens >= 50)
            {
                Console.WriteLine(); // New line after progress dots
            }

            if (showPerf)
            {
                double totalTimeSeconds = totalStopwatch.Elapsed.TotalSeconds;
                double tokensPerSec = totalTimeSeconds > 0 ? maxNewTokens / totalTimeSeconds : 0;
                Console.WriteLine($"\nGeneration completed in {totalTimeSeconds:F2}s");
                Console.WriteLine($"Tokens generated: {maxNewTokens}");
                Console.WriteLine($"Generation speed: {tokensPerSec:F2} tokens/sec");
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

            // Find k-th largest value
            var sorted = logits.OrderByDescending(x => x).ToArray();
            float kthValue = sorted[Math.Min(k, sorted.Length - 1)];

            // Set all values below k-th to -inf
            var filtered = new float[logits.Length];
            for (int i = 0; i < logits.Length; i++)
            {
                filtered[i] = logits[i] >= kthValue ? logits[i] : float.NegativeInfinity;
            }

            return filtered;
        }

        /// <summary>
        /// Compute softmax over an array
        /// </summary>
        private float[] Softmax(float[] logits)
        {
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
            var probs = new float[logits.Length];
            for (int i = 0; i < logits.Length; i++)
            {
                if (logits[i] != float.NegativeInfinity)
                {
                    probs[i] = MathF.Exp(logits[i] - max);
                    sum += probs[i];
                }
            }

            // Normalize
            if (sum > 0)
            {
                for (int i = 0; i < probs.Length; i++)
                {
                    probs[i] /= sum;
                }
            }

            return probs;
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
