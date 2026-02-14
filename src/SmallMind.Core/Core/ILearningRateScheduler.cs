namespace SmallMind.Core.Core
{
    /// <summary>
    /// Base interface for learning rate schedulers
    /// </summary>
    internal interface ILearningRateScheduler
    {
        /// <summary>
        /// Get the learning rate for the current step
        /// </summary>
        /// <param name="step">Current training step</param>
        /// <returns>Learning rate to use</returns>
        float GetLearningRate(int step);
    }
}
