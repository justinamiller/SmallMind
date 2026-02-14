namespace SmallMind.Core.Core
{
    /// <summary>
    /// Constant learning rate (no scheduling)
    /// </summary>
    internal class ConstantLR : ILearningRateScheduler
    {
        private readonly float _lr;

        /// <summary>
        /// Create constant learning rate scheduler
        /// </summary>
        /// <param name="lr">Learning rate</param>
        public ConstantLR(float lr)
        {
            _lr = lr;
        }

        /// <summary>
        /// Get the learning rate (always constant)
        /// </summary>
        public float GetLearningRate(int step) => _lr;
    }
}
