namespace SmallMind.Core.Core
{
    /// <summary>
    /// One-cycle learning rate schedule (triangular policy)
    /// Increases learning rate to max, then decreases to min over total steps
    /// </summary>
    internal class OneCycleLR : ILearningRateScheduler
    {
        private readonly float _maxLr;
        private readonly float _minLr;
        private readonly int _totalSteps;
        private readonly float _pctStart;

        /// <summary>
        /// Create one-cycle learning rate scheduler
        /// </summary>
        /// <param name="maxLr">Maximum learning rate</param>
        /// <param name="minLr">Minimum learning rate</param>
        /// <param name="totalSteps">Total training steps</param>
        /// <param name="pctStart">Percentage of cycle spent increasing LR (default: 0.3)</param>
        public OneCycleLR(float maxLr, float minLr, int totalSteps, float pctStart = 0.3f)
        {
            _maxLr = maxLr;
            _minLr = minLr;
            _totalSteps = totalSteps;
            _pctStart = Math.Clamp(pctStart, 0.0f, 1.0f);
        }

        /// <summary>
        /// Get the learning rate following one-cycle policy
        /// </summary>
        public float GetLearningRate(int step)
        {
            int increaseSteps = (int)(_totalSteps * _pctStart);

            if (step < increaseSteps)
            {
                // Increasing phase
                float progress = (float)step / increaseSteps;
                return _minLr + (_maxLr - _minLr) * progress;
            }
            else
            {
                // Decreasing phase
                float progress = (float)(step - increaseSteps) / (_totalSteps - increaseSteps);
                return _maxLr - (_maxLr - _minLr) * progress;
            }
        }
    }
}
