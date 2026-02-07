using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace SmallMind.Core
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

            // Initialize moment estimates - pre-size to parameter count
            _m = new List<float[]>(_parameters.Count);
            _v = new List<float[]>(_parameters.Count);
            
            foreach (var param in _parameters)
            {
                _m.Add(new float[param.Size]);
                _v.Add(new float[param.Size]);
            }
        }

        public void Step()
        {
            _t++;
            
            // Pre-compute bias correction factors (major optimization - avoids redundant Pow calls)
            float biasCorrection1 = 1.0f - MathF.Pow(_beta1, _t);
            float biasCorrection2 = 1.0f - MathF.Pow(_beta2, _t);
            float mScale = 1.0f / biasCorrection1;
            float vScale = 1.0f / biasCorrection2;
            
            // Pre-compute constants for vectorization
            float beta1Complement = 1.0f - _beta1;
            float beta2Complement = 1.0f - _beta2;
            
            for (int p = 0; p < _parameters.Count; p++)
            {
                var param = _parameters[p];
                if (param.Grad == null) continue;

                var m = _m[p];
                var v = _v[p];
                
                // Use SIMD vectorization for parameter updates
                StepSIMD(param.Data, param.Grad, m, v, param.Size, 
                        mScale, vScale, beta1Complement, beta2Complement);
            }
        }
        
        /// <summary>
        /// SIMD-optimized parameter update inner loop
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void StepSIMD(float[] paramData, float[] grad, float[] m, float[] v, 
                             int size, float mScale, float vScale, 
                             float beta1Complement, float beta2Complement)
        {
            int vectorSize = Vector<float>.Count;
            int i = 0;
            
            // SIMD vectorized loop
            var vBeta1 = new Vector<float>(_beta1);
            var vBeta2 = new Vector<float>(_beta2);
            var vBeta1Comp = new Vector<float>(beta1Complement);
            var vBeta2Comp = new Vector<float>(beta2Complement);
            var vMScale = new Vector<float>(mScale);
            var vVScale = new Vector<float>(vScale);
            var vEps = new Vector<float>(_eps);
            var vLr = new Vector<float>(_lr);
            var vWeightDecay = new Vector<float>(_weightDecay);
            
            for (; i <= size - vectorSize; i += vectorSize)
            {
                // Load vectors
                var vGrad = new Vector<float>(grad, i);
                var vM = new Vector<float>(m, i);
                var vV = new Vector<float>(v, i);
                var vParam = new Vector<float>(paramData, i);
                
                // Update first moment: m = beta1 * m + (1 - beta1) * grad
                vM = vBeta1 * vM + vBeta1Comp * vGrad;
                vM.CopyTo(m, i);
                
                // Update second moment: v = beta2 * v + (1 - beta2) * grad^2
                vV = vBeta2 * vV + vBeta2Comp * vGrad * vGrad;
                vV.CopyTo(v, i);
                
                // Bias-corrected moments
                var vMHat = vM * vMScale;
                var vVHat = vV * vVScale;
                
                // Parameter update: param -= lr * (mHat / (sqrt(vHat) + eps) + weightDecay * param)
                var vUpdate = vMHat / (Vector.SquareRoot(vVHat) + vEps) + vWeightDecay * vParam;
                vParam -= vLr * vUpdate;
                vParam.CopyTo(paramData, i);
            }
            
            // Scalar remainder loop
            for (; i < size; i++)
            {
                float gradVal = grad[i];
                
                // Update biased first moment estimate
                m[i] = _beta1 * m[i] + beta1Complement * gradVal;
                
                // Update biased second moment estimate
                v[i] = _beta2 * v[i] + beta2Complement * gradVal * gradVal;
                
                // Compute bias-corrected moment estimates
                float mHat = m[i] * mScale;
                float vHat = v[i] * vScale;
                
                // Update parameters with weight decay (AdamW)
                paramData[i] -= _lr * (mHat / (MathF.Sqrt(vHat) + _eps) + _weightDecay * paramData[i]);
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
