namespace SmallMind.Core.Core
{
    /// <summary>
    /// Exponential decay learning rate schedule with warmup
    /// </summary>
    internal class ExponentialDecayLR : ILearningRateScheduler
    {
        private readonly float _baseLr;
        private readonly float _decayRate;
        private readonly int _warmupSteps;

        /// <summary>
        /// Create exponential decay learning rate scheduler
        /// </summary>
        /// <param name="baseLr">Initial learning rate</param>
        /// <param name="decayRate">Decay rate per step (e.g., 0.96 means 4% decay per step)</param>
        /// <param name="warmupSteps">Number of warmup steps (default: 0)</param>
        public ExponentialDecayLR(float baseLr, float decayRate, int warmupSteps = 0)
        {
            _baseLr = baseLr;
            _decayRate = decayRate;
            _warmupSteps = warmupSteps;
        }

        /// <summary>
        /// Get the learning rate with exponential decay
        /// </summary>
        public float GetLearningRate(int step)
        {
            // Linear warmup phase
            if (step < _warmupSteps)
            {
                return _baseLr * (step + 1) / _warmupSteps;
            }

            // Exponential decay phase
            int adjustedStep = step - _warmupSteps;
            return _baseLr * MathF.Pow(_decayRate, adjustedStep);
        }
    }
}
