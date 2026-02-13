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
