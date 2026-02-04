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
            
            // Apply gradient clipping if enabled
            if (_gradClipValue > 0)
            {
                ClipGradients(_gradClipValue);
            }
            
            for (int p = 0; p < _parameters.Count; p++)
            {
                var param = _parameters[p];
                if (param.Grad == null) continue;

                var m = _m[p];
                var v = _v[p];

                for (int i = 0; i < param.Size; i++)
                {
                    float grad = param.Grad[i];
                    
                    // Update biased first moment estimate
                    m[i] = _beta1 * m[i] + (1 - _beta1) * grad;
                    
                    // Update biased second moment estimate
                    v[i] = _beta2 * v[i] + (1 - _beta2) * grad * grad;
                    
                    // Compute bias-corrected moment estimates
                    float mHat = m[i] / (1 - MathF.Pow(_beta1, _t));
                    float vHat = v[i] / (1 - MathF.Pow(_beta2, _t));
                    
                    // Update parameters with weight decay (AdamW)
                    param.Data[i] -= _lr * (mHat / (MathF.Sqrt(vHat) + _eps) + _weightDecay * param.Data[i]);
                }
            }
        }

        /// <summary>
        /// Clip gradients by value to prevent exploding gradients
        /// </summary>
        /// <param name="maxValue">Maximum absolute value for gradients</param>
        private void ClipGradients(float maxValue)
        {
            for (int p = 0; p < _parameters.Count; p++)
            {
                var param = _parameters[p];
                if (param.Grad == null) continue;

                for (int i = 0; i < param.Size; i++)
                {
                    if (param.Grad[i] > maxValue)
                        param.Grad[i] = maxValue;
                    else if (param.Grad[i] < -maxValue)
                        param.Grad[i] = -maxValue;
                }
            }
        }

        /// <summary>
        /// Clip gradients by global norm to prevent exploding gradients
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
