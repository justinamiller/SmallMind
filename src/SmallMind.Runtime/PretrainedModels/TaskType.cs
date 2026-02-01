namespace SmallMind.Runtime.PretrainedModels
{
    /// <summary>
    /// Defines the types of pre-trained model tasks supported.
    /// </summary>
    public enum TaskType
    {
        /// <summary>
        /// Text generation (default task for base Transformer).
        /// </summary>
        TextGeneration,

        /// <summary>
        /// Text classification into predefined categories.
        /// </summary>
        TextClassification,

        /// <summary>
        /// Sentiment analysis (positive, negative, neutral).
        /// </summary>
        SentimentAnalysis,

        /// <summary>
        /// Text summarization.
        /// </summary>
        Summarization,

        /// <summary>
        /// Question answering based on context.
        /// </summary>
        QuestionAnswering
    }
}
