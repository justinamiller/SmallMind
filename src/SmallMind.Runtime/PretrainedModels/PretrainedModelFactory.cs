using SmallMind.Core;
using SmallMind.Tokenizers;
using SmallMind.Transformers;

namespace SmallMind.Runtime.PretrainedModels
{
    /// <summary>
    /// Factory for creating and loading pre-trained models.
    /// </summary>
    internal static class PretrainedModelFactory
    {
        /// <summary>
        /// Load a pre-trained model from a checkpoint file.
        /// </summary>
        /// <param name="checkpointPath">Path to the .smnd checkpoint file</param>
        /// <param name="tokenizer">Tokenizer to use with the model</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Pre-trained model instance</returns>
        public static async Task<IPretrainedModel> LoadAsync(
            string checkpointPath,
            ITokenizer tokenizer,
            CancellationToken cancellationToken = default)
        {
            var store = new BinaryCheckpointStore();
            var checkpoint = await store.LoadAsync(checkpointPath, cancellationToken);

            // Get task and domain from metadata
            var taskType = checkpoint.Metadata.GetTaskType();
            var domainType = checkpoint.Metadata.GetDomainType();

            // Create model from checkpoint
            var model = CheckpointExtensions.FromCheckpoint(checkpoint);

            // Create appropriate pre-trained model wrapper
            return taskType switch
            {
                TaskType.SentimentAnalysis => new SentimentAnalysisModel(
                    model,
                    tokenizer,
                    domainType,
                    checkpoint.Metadata.GetModelName(),
                    checkpoint.Metadata.GetModelDescription()),

                TaskType.TextClassification => new TextClassificationModel(
                    model,
                    tokenizer,
                    checkpoint.Metadata.GetClassificationLabels(),
                    domainType,
                    checkpoint.Metadata.GetModelName(),
                    checkpoint.Metadata.GetModelDescription()),

                _ => throw new NotSupportedException($"Task type {taskType} not yet implemented for loading")
            };
        }

        /// <summary>
        /// Create a new sentiment analysis model.
        /// </summary>
        /// <param name="vocabSize">Vocabulary size</param>
        /// <param name="blockSize">Maximum sequence length</param>
        /// <param name="domain">Domain specialization</param>
        /// <param name="embedDim">Embedding dimension</param>
        /// <param name="numLayers">Number of transformer layers</param>
        /// <param name="numHeads">Number of attention heads</param>
        /// <param name="dropout">Dropout rate</param>
        /// <param name="seed">Random seed</param>
        /// <returns>New sentiment analysis model</returns>
        public static SentimentAnalysisModel CreateSentimentModel(
            int vocabSize,
            int blockSize = 128,
            DomainType domain = DomainType.General,
            int embedDim = 128,
            int numLayers = 4,
            int numHeads = 4,
            double dropout = 0.1,
            int seed = 42)
        {
            var model = new TransformerModel(
                vocabSize, blockSize, embedDim, numLayers, numHeads, dropout, seed);

            var tokenizer = CreateDefaultTokenizer();

            return new SentimentAnalysisModel(
                model,
                tokenizer,
                domain,
                $"SentimentAnalysis-{domain}-v1.0",
                $"Sentiment analysis model for {domain} domain");
        }

        /// <summary>
        /// Create a new text classification model.
        /// </summary>
        /// <param name="vocabSize">Vocabulary size</param>
        /// <param name="labels">Classification labels</param>
        /// <param name="blockSize">Maximum sequence length</param>
        /// <param name="domain">Domain specialization</param>
        /// <param name="embedDim">Embedding dimension</param>
        /// <param name="numLayers">Number of transformer layers</param>
        /// <param name="numHeads">Number of attention heads</param>
        /// <param name="dropout">Dropout rate</param>
        /// <param name="seed">Random seed</param>
        /// <returns>New text classification model</returns>
        public static TextClassificationModel CreateClassificationModel(
            int vocabSize,
            string[] labels,
            int blockSize = 128,
            DomainType domain = DomainType.General,
            int embedDim = 128,
            int numLayers = 4,
            int numHeads = 4,
            double dropout = 0.1,
            int seed = 42)
        {
            var model = new TransformerModel(
                vocabSize, blockSize, embedDim, numLayers, numHeads, dropout, seed);

            var tokenizer = CreateDefaultTokenizer();

            return new TextClassificationModel(
                model,
                tokenizer,
                labels,
                domain,
                $"TextClassification-{domain}-v1.0",
                $"Text classification model for {domain} domain");
        }

        /// <summary>
        /// Save a pre-trained model to a checkpoint file.
        /// </summary>
        /// <param name="pretrainedModel">Pre-trained model to save</param>
        /// <param name="checkpointPath">Path to save the checkpoint</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public static async Task SaveAsync(
            IPretrainedModel pretrainedModel,
            string checkpointPath,
            CancellationToken cancellationToken = default)
        {
            var checkpoint = pretrainedModel.Model.ToCheckpoint();

            // Add task and domain metadata
            checkpoint.Metadata.SetTaskType(pretrainedModel.Task);
            checkpoint.Metadata.SetDomainType(pretrainedModel.Domain);
            checkpoint.Metadata.SetModelName(pretrainedModel.Name);
            checkpoint.Metadata.SetModelDescription(pretrainedModel.Description);
            checkpoint.Metadata.SetModelVersion("1.0.0");

            // Add task-specific metadata
            if (pretrainedModel is ITextClassificationModel classificationModel)
            {
                var labelsArray = new string[classificationModel.Labels.Count];
                for (int i = 0; i < classificationModel.Labels.Count; i++)
                {
                    labelsArray[i] = classificationModel.Labels[i];
                }
                checkpoint.Metadata.SetClassificationLabels(labelsArray);
            }

            var store = new BinaryCheckpointStore();
            await store.SaveAsync(checkpoint, checkpointPath, cancellationToken);
        }

        private static ITokenizer CreateDefaultTokenizer()
        {
            // Create a tokenizer with common characters
            const string vocab = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 .,!?;:'\"-\n()[]{}@#$%&*+=/<>|\\~`";
            return new CharTokenizer(vocab);
        }
    }
}
