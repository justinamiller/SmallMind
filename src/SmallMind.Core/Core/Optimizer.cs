using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

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
                    // SIMD-optimized path without clipping
                    // Use vectorization for large parameter tensors (> 512 elements)
                    if (Vector.IsHardwareAccelerated && param.Size >= 512)
                    {
                        StepSIMD(gradSpan, dataSpan, m, v, param.Size,
                                oneMinusBeta1, oneMinusBeta2, beta1Correction, beta2Correction);
                    }
                    else
                    {
                        // Scalar fallback for small tensors
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

        /// <summary>
        /// SIMD-vectorized AdamW update step (no gradient clipping).
        /// Processes vectors of 4-16 floats at a time for 2-4x throughput improvement.
        /// Uses AVX2 intrinsics when available for maximum performance.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [SkipLocalsInit]
        private void StepSIMD(
            ReadOnlySpan<float> grad, 
            Span<float> data,
            float[] m, 
            float[] v, 
            int size,
            float oneMinusBeta1,
            float oneMinusBeta2,
            float beta1Correction,
            float beta2Correction)
        {
            int i = 0;
            
            // AVX2 path (8 floats) - optimal for most CPUs
            if (Avx2.IsSupported && Fma.IsSupported && size >= 8)
            {
                var vBeta1_256 = Vector256.Create(_beta1);
                var vBeta2_256 = Vector256.Create(_beta2);
                var vOneMinusBeta1_256 = Vector256.Create(oneMinusBeta1);
                var vOneMinusBeta2_256 = Vector256.Create(oneMinusBeta2);
                var vBeta1Correction_256 = Vector256.Create(beta1Correction);
                var vBeta2Correction_256 = Vector256.Create(beta2Correction);
                var vLr_256 = Vector256.Create(_lr);
                var vEps_256 = Vector256.Create(_eps);
                var vWeightDecay_256 = Vector256.Create(_weightDecay);
                
                unsafe
                {
                    fixed (float* pGrad = grad, pData = data, pM = m, pV = v)
                    {
                        for (; i <= size - 8; i += 8)
                        {
                            // Load current state
                            var vGrad = Avx.LoadVector256(pGrad + i);
                            var vM = Avx.LoadVector256(pM + i);
                            var vV = Avx.LoadVector256(pV + i);
                            var vData = Avx.LoadVector256(pData + i);
                            
                            // Update biased first moment: m = beta1 * m + (1 - beta1) * grad
                            vM = Fma.MultiplyAdd(vBeta1_256, vM, Avx.Multiply(vOneMinusBeta1_256, vGrad));
                            
                            // Update biased second moment: v = beta2 * v + (1 - beta2) * grad^2
                            vV = Fma.MultiplyAdd(vBeta2_256, vV, Avx.Multiply(vOneMinusBeta2_256, Avx.Multiply(vGrad, vGrad)));
                            
                            // Store updated moments
                            Avx.Store(pM + i, vM);
                            Avx.Store(pV + i, vV);
                            
                            // Bias-corrected moments
                            var vMHat = Avx.Multiply(vM, vBeta1Correction_256);
                            var vVHat = Avx.Multiply(vV, vBeta2Correction_256);
                            
                            // AdamW update: data -= lr * (mHat / sqrt(vHat + eps) + weightDecay * data)
                            var vDenom = Avx.Sqrt(Avx.Add(vVHat, vEps_256));
                            var vUpdate = Avx.Divide(vMHat, vDenom);
                            vUpdate = Fma.MultiplyAdd(vWeightDecay_256, vData, vUpdate);
                            vData = Avx.Subtract(vData, Avx.Multiply(vLr_256, vUpdate));
                            
                            Avx.Store(pData + i, vData);
                        }
                    }
                }
            }
            
            // Vector<T> fallback for platforms without AVX2
            if (!Avx2.IsSupported && Vector.IsHardwareAccelerated)
            {
                int vectorSize = Vector<float>.Count;
                
                // Pre-broadcast constants to vectors
                var vBeta1 = new Vector<float>(_beta1);
                var vBeta2 = new Vector<float>(_beta2);
                var vOneMinusBeta1 = new Vector<float>(oneMinusBeta1);
                var vOneMinusBeta2 = new Vector<float>(oneMinusBeta2);
                var vBeta1Correction = new Vector<float>(beta1Correction);
                var vBeta2Correction = new Vector<float>(beta2Correction);
                var vLr = new Vector<float>(_lr);
                var vEps = new Vector<float>(_eps);
                var vWeightDecay = new Vector<float>(_weightDecay);
                
                // SIMD loop: Process vectorSize floats per iteration
                for (; i <= size - vectorSize; i += vectorSize)
                {
                    // Load current state
                    var vGrad = new Vector<float>(grad.Slice(i, vectorSize));
                    var vM = new Vector<float>(m, i);
                    var vV = new Vector<float>(v, i);
                    var vData = new Vector<float>(data.Slice(i, vectorSize));
                    
                    // Update biased first moment: m = beta1 * m + (1 - beta1) * grad
                    vM = vBeta1 * vM + vOneMinusBeta1 * vGrad;
                    
                    // Update biased second moment: v = beta2 * v + (1 - beta2) * grad^2
                    vV = vBeta2 * vV + vOneMinusBeta2 * vGrad * vGrad;
                    
                    // Store updated moments
                    vM.CopyTo(m, i);
                    vV.CopyTo(v, i);
                    
                    // Bias-corrected moments
                    var vMHat = vM * vBeta1Correction;
                    var vVHat = vV * vBeta2Correction;
                    
                    // AdamW update: data -= lr * (mHat / sqrt(vHat + eps) + weightDecay * data)
                    // Note: Vector<T> doesn't have Sqrt, so we need to do this in scalar
                    // This is still faster overall because moment updates are vectorized
                    for (int j = 0; j < vectorSize; j++)
                    {
                        int idx = i + j;
                        float mHat = vMHat[j];
                        float vHat = vVHat[j];
                        data[idx] -= _lr * (mHat / (MathF.Sqrt(vHat) + _eps) + _weightDecay * data[idx]);
                    }
                }
            }
            
            // Scalar remainder
            for (; i < size; i++)
            {
                float g = grad[i];
                
                // Update biased first moment estimate
                m[i] = _beta1 * m[i] + oneMinusBeta1 * g;
                
                // Update biased second moment estimate
                v[i] = _beta2 * v[i] + oneMinusBeta2 * g * g;
                
                // Compute bias-corrected moment estimates
                float mHat = m[i] * beta1Correction;
                float vHat = v[i] * beta2Correction;
                
                // Update parameters with weight decay (AdamW)
                data[i] -= _lr * (mHat / (MathF.Sqrt(vHat) + _eps) + _weightDecay * data[i]);
            }
        }
    }
}
