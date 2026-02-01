using SmallMind.Core.Core;
using SmallMind.Tokenizers;
using SmallMind.Transformers;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SmallMind.Runtime.PretrainedModels
{
    /// <summary>
    /// Sentiment analysis model implementation.
    /// Classifies text as Positive, Negative, or Neutral.
    /// </summary>
    public class SentimentAnalysisModel : ISentimentAnalysisModel
    {
        private readonly TransformerModel _model;
        private readonly ITokenizer _tokenizer;
        private readonly string _name;
        private readonly string _description;
        private readonly DomainType _domain;
        
        // Sentiment labels
        private static readonly string[] SentimentLabels = { "Positive", "Negative", "Neutral" };

        public TaskType Task => TaskType.SentimentAnalysis;
        public DomainType Domain => _domain;
        public TransformerModel Model => _model;
        public string Name => _name;
        public string Description => _description;

        /// <summary>
        /// Create a sentiment analysis model.
        /// </summary>
        /// <param name="model">Underlying transformer model</param>
        /// <param name="tokenizer">Tokenizer for text processing</param>
        /// <param name="domain">Domain specialization</param>
        /// <param name="name">Model name</param>
        /// <param name="description">Model description</param>
        public SentimentAnalysisModel(
            TransformerModel model, 
            ITokenizer tokenizer,
            DomainType domain = DomainType.General,
            string? name = null,
            string? description = null)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _tokenizer = tokenizer ?? throw new ArgumentNullException(nameof(tokenizer));
            _domain = domain;
            _name = name ?? $"SentimentAnalysis-{domain}";
            _description = description ?? $"Sentiment analysis model for {domain} domain";
        }

        /// <summary>
        /// Analyze sentiment of input text.
        /// </summary>
        public string AnalyzeSentiment(string text)
        {
            var scores = AnalyzeSentimentWithScores(text);
            return scores.OrderByDescending(x => x.Value).First().Key;
        }

        /// <summary>
        /// Analyze sentiment and return confidence scores.
        /// </summary>
        public Dictionary<string, float> AnalyzeSentimentWithScores(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return new Dictionary<string, float>
                {
                    { "Positive", 0f },
                    { "Negative", 0f },
                    { "Neutral", 1f }
                };
            }

            // Tokenize input
            var tokensList = _tokenizer.Encode(text);
            var tokens = tokensList.ToArray();
            
            // Truncate to model's block size
            if (tokens.Length > _model.BlockSize)
            {
                var truncated = new int[_model.BlockSize];
                Array.Copy(tokens, truncated, _model.BlockSize);
                tokens = truncated;
            }

            // Create input tensor with shape (1, T) for batch size 1
            int seqLen = tokens.Length;
            var inputData = new float[seqLen];
            for (int i = 0; i < seqLen; i++)
            {
                inputData[i] = tokens[i];
            }
            var inputTensor = new Tensor(inputData, new[] { 1, seqLen });
            
            // Forward pass through model
            var logits = _model.Forward(inputTensor);

            // Get final token's logits (for classification)
            var finalLogits = ExtractFinalLogits(logits, seqLen);

            // Convert to probabilities using softmax
            var probs = Softmax(finalLogits);

            // Map to sentiment scores
            // Use simple heuristic: average probability distribution over vocab
            // In a real pre-trained model, this would use specific output heads
            var scores = ComputeSentimentScores(probs);

            return scores;
        }

        private float[] ExtractFinalLogits(Tensor logits, int seqLen)
        {
            // Extract logits for the final position
            var vocabSize = _model.VocabSize;
            var finalLogits = new float[vocabSize];
            
            int startIdx = (seqLen - 1) * vocabSize;
            Array.Copy(logits.Data, startIdx, finalLogits, 0, vocabSize);
            
            return finalLogits;
        }

        private float[] Softmax(float[] logits)
        {
            var max = logits.Max();
            var exp = logits.Select(x => MathF.Exp(x - max)).ToArray();
            var sum = exp.Sum();
            return exp.Select(x => x / sum).ToArray();
        }

        private Dictionary<string, float> ComputeSentimentScores(float[] probs)
        {
            // Simple heuristic: use distribution of probabilities to infer sentiment
            // This is a placeholder - in a real model, you'd have specific output heads
            
            // Calculate basic statistics
            var mean = probs.Average();
            var variance = probs.Select(p => (p - mean) * (p - mean)).Average();
            var entropy = -probs.Where(p => p > 0).Sum(p => p * MathF.Log(p));
            
            // High entropy = neutral, low entropy with high values = strong sentiment
            var maxProb = probs.Max();
            var maxIdx = Array.IndexOf(probs, maxProb);
            
            float positive, negative, neutral;
            
            if (entropy > 4.0f) // High uncertainty
            {
                positive = 0.33f;
                negative = 0.33f;
                neutral = 0.34f;
            }
            else
            {
                // Use position in vocab and probability distribution as proxy
                // This is simplified - real models would have learned associations
                var topThird = maxIdx < probs.Length / 3;
                var bottomThird = maxIdx > 2 * probs.Length / 3;
                
                if (topThird)
                {
                    positive = maxProb * 0.6f + 0.2f;
                    negative = (1 - maxProb) * 0.3f;
                    neutral = 1.0f - positive - negative;
                }
                else if (bottomThird)
                {
                    negative = maxProb * 0.6f + 0.2f;
                    positive = (1 - maxProb) * 0.3f;
                    neutral = 1.0f - positive - negative;
                }
                else
                {
                    neutral = maxProb * 0.6f + 0.2f;
                    positive = (1 - maxProb) * 0.4f;
                    negative = 1.0f - neutral - positive;
                }
            }

            return new Dictionary<string, float>
            {
                { "Positive", positive },
                { "Negative", negative },
                { "Neutral", neutral }
            };
        }
    }
}
