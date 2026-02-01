using SmallMind.Core.Core;
using SmallMind.Tokenizers;
using SmallMind.Transformers;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SmallMind.Runtime.PretrainedModels
{
    /// <summary>
    /// Text classification model implementation.
    /// Classifies text into predefined categories.
    /// </summary>
    public class TextClassificationModel : ITextClassificationModel
    {
        private readonly TransformerModel _model;
        private readonly ITokenizer _tokenizer;
        private readonly string _name;
        private readonly string _description;
        private readonly DomainType _domain;
        private readonly List<string> _labels;

        public TaskType Task => TaskType.TextClassification;
        public DomainType Domain => _domain;
        public TransformerModel Model => _model;
        public string Name => _name;
        public string Description => _description;
        public IReadOnlyList<string> Labels => _labels.AsReadOnly();

        /// <summary>
        /// Create a text classification model.
        /// </summary>
        /// <param name="model">Underlying transformer model</param>
        /// <param name="tokenizer">Tokenizer for text processing</param>
        /// <param name="labels">Classification labels</param>
        /// <param name="domain">Domain specialization</param>
        /// <param name="name">Model name</param>
        /// <param name="description">Model description</param>
        public TextClassificationModel(
            TransformerModel model,
            ITokenizer tokenizer,
            string[] labels,
            DomainType domain = DomainType.General,
            string? name = null,
            string? description = null)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _tokenizer = tokenizer ?? throw new ArgumentNullException(nameof(tokenizer));
            
            if (labels == null || labels.Length == 0)
            {
                throw new ArgumentException("At least one label must be provided", nameof(labels));
            }
            
            _labels = new List<string>(labels);
            _domain = domain;
            _name = name ?? $"TextClassification-{domain}";
            _description = description ?? $"Text classification model for {domain} domain with {labels.Length} labels";
        }

        /// <summary>
        /// Classify input text into one of the predefined labels.
        /// </summary>
        public string Classify(string text)
        {
            var probs = ClassifyWithProbabilities(text);
            return probs.OrderByDescending(x => x.Value).First().Key;
        }

        /// <summary>
        /// Classify input text and return probabilities for all labels.
        /// </summary>
        public Dictionary<string, float> ClassifyWithProbabilities(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                // Return uniform distribution for empty text
                var uniformProb = 1.0f / _labels.Count;
                return _labels.ToDictionary(label => label, _ => uniformProb);
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

            // Get final token's logits
            var finalLogits = ExtractFinalLogits(logits, seqLen);

            // Convert to probabilities
            var probs = Softmax(finalLogits);

            // Map probabilities to labels
            var labelProbs = MapToLabels(probs);

            return labelProbs;
        }

        private float[] ExtractFinalLogits(Tensor logits, int seqLen)
        {
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

        private Dictionary<string, float> MapToLabels(float[] probs)
        {
            // Aggregate probabilities for each label
            // This is a simplified heuristic - real models would have specific output heads
            var labelProbs = new Dictionary<string, float>();
            
            // Divide probability space among labels
            int tokensPerLabel = Math.Max(1, probs.Length / _labels.Count);
            
            for (int i = 0; i < _labels.Count; i++)
            {
                int startIdx = i * tokensPerLabel;
                int endIdx = (i == _labels.Count - 1) ? probs.Length : (i + 1) * tokensPerLabel;
                
                // Sum probabilities in this segment
                float labelProb = 0f;
                for (int j = startIdx; j < endIdx && j < probs.Length; j++)
                {
                    labelProb += probs[j];
                }
                
                labelProbs[_labels[i]] = labelProb;
            }

            // Normalize to ensure sum = 1
            var total = labelProbs.Values.Sum();
            if (total > 0)
            {
                var normalized = new Dictionary<string, float>();
                foreach (var kvp in labelProbs)
                {
                    normalized[kvp.Key] = kvp.Value / total;
                }
                return normalized;
            }

            return labelProbs;
        }
    }
}
