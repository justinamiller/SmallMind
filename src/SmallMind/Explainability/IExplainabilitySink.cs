namespace SmallMind.Explainability
{
    /// <summary>
    /// Interface for sinks that receive explainability data during generation.
    /// </summary>
    public interface IExplainabilitySink
    {
        /// <summary>
        /// Gets a value indicating whether the sink is enabled and will accept data.
        /// When false, generation should skip all explainability capture.
        /// </summary>
        bool IsEnabled { get; }

        /// <summary>
        /// Called when generation starts.
        /// </summary>
        /// <param name="ctx">Context for the generation session.</param>
        void OnGenerationStart(ExplainabilityContext ctx);

        /// <summary>
        /// Called for each token generation step.
        /// </summary>
        /// <param name="step">Data for this step.</param>
        void OnTokenStep(TokenStepData step);

        /// <summary>
        /// Called when generation completes.
        /// </summary>
        /// <param name="summary">Summary of the generation session.</param>
        void OnGenerationEnd(ExplainabilitySummary summary);
    }
}
