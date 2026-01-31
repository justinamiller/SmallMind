using System;
using System.Collections.Generic;

namespace TinyLLM.Core
{
    /// <summary>
    /// AdamW optimizer for training neural networks
    /// </summary>
    public class AdamW
    {
        private List<Tensor> _parameters;
        private float _lr;
        private float _beta1;
        private float _beta2;
        private float _eps;
        private float _weightDecay;
        
        private List<float[]> _m; // First moment estimates
        private List<float[]> _v; // Second moment estimates
        private int _t; // Time step

        public AdamW(List<Tensor> parameters, float lr = 0.001f, float beta1 = 0.9f, 
                     float beta2 = 0.999f, float eps = 1e-8f, float weightDecay = 0.01f)
        {
            _parameters = parameters;
            _lr = lr;
            _beta1 = beta1;
            _beta2 = beta2;
            _eps = eps;
            _weightDecay = weightDecay;
            _t = 0;

            // Initialize moment estimates
            _m = new List<float[]>();
            _v = new List<float[]>();
            
            foreach (var param in _parameters)
            {
                _m.Add(new float[param.Size]);
                _v.Add(new float[param.Size]);
            }
        }

        public void Step()
        {
            _t++;
            
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

        public void ZeroGrad()
        {
            foreach (var param in _parameters)
            {
                param.ZeroGrad();
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
