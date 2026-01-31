using System;
using System.Collections.Generic;
using System.Linq;
using TorchSharp;
using static TorchSharp.torch;

namespace TinyLLM
{
    /// <summary>
    /// Implements text generation with greedy decoding, temperature sampling, and top-k filtering.
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
        /// <param name="prompt">Starting text</param>
        /// <param name="maxNewTokens">Number of tokens to generate</param>
        /// <param name="temperature">Sampling temperature (1.0 = unchanged, lower = more conservative, higher = more random)</param>
        /// <param name="topK">If > 0, only sample from top-k most likely tokens</param>
        /// <param name="seed">Random seed for reproducibility</param>
        public string Generate(string prompt, int maxNewTokens, double temperature = 1.0, int topK = 0, int? seed = null)
        {
            _model.eval();

            Random random;
            if (seed.HasValue)
            {
                random = new Random(seed.Value);
                torch.manual_seed(seed.Value);
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
            Console.WriteLine("---");

            using (var _ = torch.no_grad())
            {
                for (int i = 0; i < maxNewTokens; i++)
                {
                    // Crop context to last blockSize tokens
                    var contextCropped = context.Count <= _blockSize 
                        ? context 
                        : context.Skip(context.Count - _blockSize).ToList();

                    // Convert to tensor: (1, T)
                    var contextTensor = torch.tensor(
                        contextCropped.Select(x => (long)x).ToArray(), 
                        dtype: ScalarType.Int64
                    ).unsqueeze(0);

                    // Forward pass: (1, T, vocab_size)
                    var logits = _model.forward(contextTensor);

                    // Get logits for the last position: (1, vocab_size)
                    var logitsLast = logits.index(TensorIndex.Colon, TensorIndex.Single(-1), TensorIndex.Colon).squeeze(0);

                    // Apply temperature
                    if (temperature != 1.0)
                    {
                        logitsLast = logitsLast / temperature;
                    }

                    // Apply top-k filtering
                    if (topK > 0)
                    {
                        logitsLast = ApplyTopK(logitsLast, topK);
                    }

                    // Convert to probabilities
                    var probs = functional.softmax(logitsLast, dim: -1);

                    // Sample from the distribution
                    var nextToken = SampleFromProbs(probs, random);

                    // Add to context
                    context.Add((int)nextToken);

                    // Dispose tensors
                    contextTensor.Dispose();
                    logits.Dispose();
                    logitsLast.Dispose();
                    probs.Dispose();
                }
            }

            // Decode and return
            var generated = _tokenizer.Decode(context);
            return generated;
        }

        /// <summary>
        /// Apply top-k filtering to logits.
        /// Sets all logits outside the top-k to negative infinity.
        /// </summary>
        private Tensor ApplyTopK(Tensor logits, int k)
        {
            var vocabSize = logits.shape[0];
            if (k >= vocabSize)
            {
                return logits;
            }

            // Get top-k values and indices
            var (topkValues, topkIndices) = logits.topk(k);

            // Get the k-th largest value
            var kthValue = topkValues.index(TensorIndex.Single(-1));

            // Set all values below k-th to -inf
            var filtered = logits.masked_fill(logits < kthValue, float.NegativeInfinity);

            topkValues.Dispose();
            topkIndices.Dispose();
            kthValue.Dispose();

            return filtered;
        }

        /// <summary>
        /// Sample a token index from a probability distribution.
        /// </summary>
        private long SampleFromProbs(Tensor probs, Random random)
        {
            // Convert to CPU and get array
            var probsArray = probs.cpu().data<float>().ToArray();

            // Sample using cumulative distribution
            var cumSum = 0.0;
            var target = random.NextDouble();

            for (int i = 0; i < probsArray.Length; i++)
            {
                cumSum += probsArray[i];
                if (cumSum >= target)
                {
                    return i;
                }
            }

            // Fallback (shouldn't happen with proper normalization)
            return probsArray.Length - 1;
        }
    }
}
