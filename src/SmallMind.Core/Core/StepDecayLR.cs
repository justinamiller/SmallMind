namespace SmallMind.Core.Core
{
    /// <summary>
    /// Step decay learning rate schedule with warmup
    /// Reduces learning rate by a factor at specified intervals
    /// </summary>
    internal class StepDecayLR : ILearningRateScheduler
    {
        private readonly float _baseLr;
        private readonly float _decayFactor;
        private readonly int _decaySteps;
        private readonly int _warmupSteps;

        /// <summary>
        /// Create step decay learning rate scheduler
        /// </summary>
        /// <param name="baseLr">Initial learning rate</param>
        /// <param name="decayFactor">Factor to multiply learning rate by at each decay step</param>
        /// <param name="decaySteps">Number of steps between each decay</param>
        /// <param name="warmupSteps">Number of warmup steps (default: 0)</param>
        public StepDecayLR(float baseLr, float decayFactor, int decaySteps, int warmupSteps = 0)
        {
            _baseLr = baseLr;
            _decayFactor = decayFactor;
            _decaySteps = decaySteps;
            _warmupSteps = warmupSteps;
        }

        /// <summary>
        /// Get the learning rate with step decay
        /// </summary>
        public float GetLearningRate(int step)
        {
            // Linear warmup phase
            if (step < _warmupSteps)
            {
                return _baseLr * (step + 1) / _warmupSteps;
            }

            // Step decay phase
            int adjustedStep = step - _warmupSteps;
            int numDecays = adjustedStep / _decaySteps;
            return _baseLr * MathF.Pow(_decayFactor, numDecays);
        }
    }
}
