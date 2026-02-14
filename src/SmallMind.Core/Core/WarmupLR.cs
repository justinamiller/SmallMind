namespace SmallMind.Core.Core
{
    /// <summary>
    /// Linear warmup followed by constant learning rate
    /// </summary>
    internal class WarmupLR : ILearningRateScheduler
    {
        private readonly float _baseLr;
        private readonly int _warmupSteps;

        /// <summary>
        /// Create warmup learning rate scheduler
        /// </summary>
        /// <param name="baseLr">Target learning rate after warmup</param>
        /// <param name="warmupSteps">Number of warmup steps</param>
        public WarmupLR(float baseLr, int warmupSteps)
        {
            _baseLr = baseLr;
            _warmupSteps = warmupSteps;
        }

        /// <summary>
        /// Get the learning rate with linear warmup
        /// </summary>
        public float GetLearningRate(int step)
        {
            if (step < _warmupSteps)
            {
                return _baseLr * (step + 1) / _warmupSteps;
            }
            return _baseLr;
        }
    }
}
