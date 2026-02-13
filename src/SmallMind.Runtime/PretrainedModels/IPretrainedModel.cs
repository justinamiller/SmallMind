using SmallMind.Transformers;

namespace SmallMind.Runtime.PretrainedModels
{
    /// <summary>
    /// Base interface for all pre-trained models.
    /// </summary>
    internal interface IPretrainedModel
    {
        /// <summary>
        /// The task this model is designed for.
        /// </summary>
        TaskType Task { get; }

        /// <summary>
        /// The domain this model is specialized for.
        /// </summary>
        DomainType Domain { get; }

        /// <summary>
        /// The underlying Transformer model.
        /// </summary>
        TransformerModel Model { get; }

        /// <summary>
        /// Model name and version.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Model description.
        /// </summary>
        string Description { get; }
    }

    /// <summary>
    /// Interface for text classification models.
    /// </summary>
    internal interface ITextClassificationModel : IPretrainedModel
    {
        /// <summary>
        /// Available classification labels.
        /// </summary>
        IReadOnlyList<string> Labels { get; }

        /// <summary>
        /// Classify input text into one of the predefined labels.
        /// </summary>
        /// <param name="text">Input text to classify</param>
        /// <returns>Predicted label</returns>
        string Classify(string text);

        /// <summary>
        /// Classify input text and return probabilities for all labels.
        /// </summary>
        /// <param name="text">Input text to classify</param>
        /// <returns>Dictionary mapping labels to probabilities</returns>
        Dictionary<string, float> ClassifyWithProbabilities(string text);
    }

    /// <summary>
    /// Interface for sentiment analysis models.
    /// </summary>
    internal interface ISentimentAnalysisModel : IPretrainedModel
    {
        /// <summary>
        /// Analyze sentiment of input text.
        /// </summary>
        /// <param name="text">Input text to analyze</param>
        /// <returns>Sentiment label (Positive, Negative, or Neutral)</returns>
        string AnalyzeSentiment(string text);

        /// <summary>
        /// Analyze sentiment and return confidence scores.
        /// </summary>
        /// <param name="text">Input text to analyze</param>
        /// <returns>Dictionary with sentiment scores</returns>
        Dictionary<string, float> AnalyzeSentimentWithScores(string text);
    }

    /// <summary>
    /// Interface for summarization models.
    /// </summary>
    internal interface ISummarizationModel : IPretrainedModel
    {
        /// <summary>
        /// Generate a summary of the input text.
        /// </summary>
        /// <param name="text">Input text to summarize</param>
        /// <param name="maxLength">Maximum length of summary</param>
        /// <returns>Summary text</returns>
        string Summarize(string text, int maxLength = 50);
    }

    /// <summary>
    /// Interface for question answering models.
    /// </summary>
    internal interface IQuestionAnsweringModel : IPretrainedModel
    {
        /// <summary>
        /// Answer a question based on provided context.
        /// </summary>
        /// <param name="question">Question to answer</param>
        /// <param name="context">Context containing the answer</param>
        /// <returns>Answer text</returns>
        string Answer(string question, string context);

        /// <summary>
        /// Answer a question and return confidence score.
        /// </summary>
        /// <param name="question">Question to answer</param>
        /// <param name="context">Context containing the answer</param>
        /// <returns>Answer with confidence score</returns>
        (string answer, float confidence) AnswerWithConfidence(string question, string context);
    }
}
