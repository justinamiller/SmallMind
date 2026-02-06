using System;
using System.Collections.Generic;

namespace SmallMind.Core.Core
{
    /// <summary>
    /// AdamW optimizer for training neural networks with gradient clipping support
    /// </summary>
    public sealed class AdamW
    {
        private List<Tensor> _parameters;
        private float _lr;
        private float _beta1;
        private float _beta2;
        private float _eps;
        private float _weightDecay;
        private float _gradClipValue;
        
        private List<float[]> _m; // First moment estimates
        private List<float[]> _v; // Second moment estimates
        private int _t; // Time step

        /// <summary>
        /// Create AdamW optimizer
        /// </summary>
        /// <param name="parameters">Parameters to optimize</param>
        /// <param name="lr">Learning rate</param>
        /// <param name="beta1">Exponential decay rate for first moment estimates</param>
        /// <param name="beta2">Exponential decay rate for second moment estimates</param>
        /// <param name="eps">Small constant for numerical stability</param>
        /// <param name="weightDecay">Weight decay coefficient (L2 regularization)</param>
        /// <param name="gradClipValue">Maximum gradient value (0 = no clipping)</param>
        public AdamW(List<Tensor> parameters, float lr = 0.001f, float beta1 = 0.9f, 
                     float beta2 = 0.999f, float eps = 1e-8f, float weightDecay = 0.01f, float gradClipValue = 0.0f)
        {
            _parameters = parameters;
            _lr = lr;
            _beta1 = beta1;
            _beta2 = beta2;
            _eps = eps;
            _weightDecay = weightDecay;
            _gradClipValue = gradClipValue;
            _t = 0;

            // Initialize moment estimates - pre-size to parameter count
            _m = new List<float[]>(_parameters.Count);
            _v = new List<float[]>(_parameters.Count);
            
            for (int p = 0; p < _parameters.Count; p++)
            {
                _m.Add(new float[_parameters[p].Size]);
                _v.Add(new float[_parameters[p].Size]);
            }
        }

        /// <summary>
        /// Perform one optimization step with optional gradient clipping
        /// </summary>
        public void Step()
        {
            _t++;
            
            // Pre-compute bias correction factors outside inner loop (CRITICAL OPTIMIZATION)
            // These were previously computed millions of times per step via MathF.Pow() in the inner loop
            float beta1T = MathF.Pow(_beta1, _t);
            float beta2T = MathF.Pow(_beta2, _t);
            float beta1Correction = 1.0f / (1.0f - beta1T);
            float beta2Correction = 1.0f / (1.0f - beta2T);
            
            // Pre-compute beta complements
            float oneMinusBeta1 = 1.0f - _beta1;
            float oneMinusBeta2 = 1.0f - _beta2;
            
            for (int p = 0; p < _parameters.Count; p++)
            {
                var param = _parameters[p];
                if (param.Grad == null) continue;

                var m = _m[p];
                var v = _v[p];
                Span<float> gradSpan = param.Grad;
                Span<float> dataSpan = param.Data;
                
                // Performance note: We use separate paths for clipping vs no-clipping
                // to avoid a branch in the innermost loop (called millions of times).
                // This duplication is intentional for maximum performance.
                if (_gradClipValue > 0)
                {
                    // Path with fused gradient clipping
                    for (int i = 0; i < param.Size; i++)
                    {
                        // Clip gradient (preserves original gradSpan for diagnostic purposes)
                        float grad = Math.Clamp(gradSpan[i], -_gradClipValue, _gradClipValue);
                        
                        // Update biased first moment estimate
                        m[i] = _beta1 * m[i] + oneMinusBeta1 * grad;
                        
                        // Update biased second moment estimate
                        v[i] = _beta2 * v[i] + oneMinusBeta2 * grad * grad;
                        
                        // Compute bias-corrected moment estimates (using pre-computed corrections)
                        float mHat = m[i] * beta1Correction;
                        float vHat = v[i] * beta2Correction;
                        
                        // Update parameters with weight decay (AdamW)
                        dataSpan[i] -= _lr * (mHat / (MathF.Sqrt(vHat) + _eps) + _weightDecay * dataSpan[i]);
                    }
                }
                else
                {
                    // Optimized path without clipping (avoids Clamp overhead)
                    for (int i = 0; i < param.Size; i++)
                    {
                        float grad = gradSpan[i];
                        
                        // Update biased first moment estimate
                        m[i] = _beta1 * m[i] + oneMinusBeta1 * grad;
                        
                        // Update biased second moment estimate
                        v[i] = _beta2 * v[i] + oneMinusBeta2 * grad * grad;
                        
                        // Compute bias-corrected moment estimates (using pre-computed corrections)
                        float mHat = m[i] * beta1Correction;
                        float vHat = v[i] * beta2Correction;
                        
                        // Update parameters with weight decay (AdamW)
                        dataSpan[i] -= _lr * (mHat / (MathF.Sqrt(vHat) + _eps) + _weightDecay * dataSpan[i]);
                    }
                }
            }
        }

        /// <summary>
        /// Clip gradients by global norm to prevent exploding gradients
        /// Note: For value-based clipping (_gradClipValue > 0), clipping is fused into the Step() method for performance.
        /// </summary>
        /// <param name="maxNorm">Maximum L2 norm of gradients</param>
        public void ClipGradientsByNorm(float maxNorm)
        {
            // Compute global gradient norm
            float totalNorm = 0.0f;
            for (int p = 0; p < _parameters.Count; p++)
            {
                var param = _parameters[p];
                if (param.Grad == null) continue;

                for (int i = 0; i < param.Size; i++)
                {
                    totalNorm += param.Grad[i] * param.Grad[i];
                }
            }
            totalNorm = MathF.Sqrt(totalNorm);

            // Scale gradients if norm exceeds threshold
            if (totalNorm > maxNorm)
            {
                float scale = maxNorm / (totalNorm + 1e-6f);
                for (int p = 0; p < _parameters.Count; p++)
                {
                    var param = _parameters[p];
                    if (param.Grad == null) continue;

                    for (int i = 0; i < param.Size; i++)
                    {
                        param.Grad[i] *= scale;
                    }
                }
            }
        }

        public void ZeroGrad()
        {
            for (int p = 0; p < _parameters.Count; p++)
            {
                _parameters[p].ZeroGrad();
            }
        }

        /// <summary>
        /// Set the learning rate (useful for learning rate scheduling)
        /// </summary>
        public void SetLearningRate(float lr)
        {
            _lr = lr;
        }

        /// <summary>
        /// Get the current learning rate
        /// </summary>
        public float GetLearningRate()
        {
            return _lr;
        }
    }
}
