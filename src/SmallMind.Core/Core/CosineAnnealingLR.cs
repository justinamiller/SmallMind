namespace SmallMind.Core.Core
{
    /// <summary>
    /// Cosine annealing learning rate schedule with warmup
    /// </summary>
    internal class CosineAnnealingLR : ILearningRateScheduler
    {
        private readonly float _baseLr;
        private readonly float _minLr;
        private readonly int _warmupSteps;
        private readonly int _totalSteps;

        /// <summary>
        /// Create cosine annealing learning rate scheduler
        /// </summary>
        /// <param name="baseLr">Maximum learning rate</param>
        /// <param name="minLr">Minimum learning rate</param>
        /// <param name="totalSteps">Total training steps</param>
        /// <param name="warmupSteps">Number of warmup steps (default: 0)</param>
        public CosineAnnealingLR(float baseLr, float minLr, int totalSteps, int warmupSteps = 0)
        {
            _baseLr = baseLr;
            _minLr = minLr;
            _totalSteps = totalSteps;
            _warmupSteps = warmupSteps;
        }

        /// <summary>
        /// Get the learning rate with cosine annealing
        /// </summary>
        public float GetLearningRate(int step)
        {
            // Linear warmup phase
            if (step < _warmupSteps)
            {
                return _baseLr * (step + 1) / _warmupSteps;
            }

            // Cosine annealing phase
            int adjustedStep = step - _warmupSteps;
            int adjustedTotal = _totalSteps - _warmupSteps;

            if (adjustedStep >= adjustedTotal)
            {
                return _minLr;
            }

            float cosine = MathF.Cos(MathF.PI * adjustedStep / adjustedTotal);
            return _minLr + (_baseLr - _minLr) * 0.5f * (1.0f + cosine);
        }
    }
}
