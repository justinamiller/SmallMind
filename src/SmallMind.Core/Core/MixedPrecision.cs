using System;
using System.Runtime.CompilerServices;

namespace SmallMind.Core.Core
{
    /// <summary>
    /// Mixed precision training utilities for FP16/FP32 conversion and training.
    /// Uses Half precision (float16) for forward/backward, float32 for master weights.
    /// </summary>
    public static class MixedPrecision
    {
        /// <summary>
        /// Convert float32 buffer to float16 (Half)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FloatToHalf(ReadOnlySpan<float> source, Span<Half> dest)
        {
            if (source.Length != dest.Length)
                throw new ArgumentException("Source and destination lengths must match");
            
            for (int i = 0; i < source.Length; i++)
            {
                dest[i] = (Half)source[i];
            }
        }
        
        /// <summary>
        /// Convert float16 (Half) buffer to float32
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void HalfToFloat(ReadOnlySpan<Half> source, Span<float> dest)
        {
            if (source.Length != dest.Length)
                throw new ArgumentException("Source and destination lengths must match");
            
            for (int i = 0; i < source.Length; i++)
            {
                dest[i] = (float)source[i];
            }
        }
        
        /// <summary>
        /// Check if gradients have overflow (infinity or NaN)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool HasGradientOverflow(ReadOnlySpan<float> gradients)
        {
            for (int i = 0; i < gradients.Length; i++)
            {
                if (float.IsInfinity(gradients[i]) || float.IsNaN(gradients[i]))
                {
                    return true;
                }
            }
            return false;
        }
    }
    
    /// <summary>
    /// Mixed precision trainer that maintains FP32 master weights
    /// and uses FP16 for forward/backward passes.
    /// </summary>
    internal class MixedPrecisionTrainer
    {
        private readonly AdamW _optimizer;
        private readonly float[][] _masterWeights;
        private readonly Half[][] _fp16Weights;
        
        // Dynamic loss scaling
        private float _lossScale;
        private const float SCALE_FACTOR = 2f;
        private const int SCALE_WINDOW = 1000;
        private int _stepsSinceScale;
        private int _overflowCount;
        
        public float LossScale => _lossScale;
        public int OverflowCount => _overflowCount;
        
        public MixedPrecisionTrainer(AdamW optimizer, System.Collections.Generic.List<Tensor> parameters, float initialLossScale = 65536f)
        {
            _optimizer = optimizer;
            _lossScale = initialLossScale;
            _stepsSinceScale = 0;
            _overflowCount = 0;
            
            // Allocate master weights (FP32) and FP16 copies
            _masterWeights = new float[parameters.Count][];
            _fp16Weights = new Half[parameters.Count][];
            
            for (int i = 0; i < parameters.Count; i++)
            {
                _masterWeights[i] = (float[])parameters[i].Data.Clone();
                _fp16Weights[i] = new Half[parameters[i].Size];
            }
        }
        
        /// <summary>
        /// Convert master weights to FP16 for forward pass
        /// </summary>
        public void SyncToFP16(System.Collections.Generic.List<Tensor> parameters)
        {
            for (int i = 0; i < parameters.Count; i++)
            {
                MixedPrecision.FloatToHalf(_masterWeights[i], _fp16Weights[i]);
                MixedPrecision.HalfToFloat(_fp16Weights[i], parameters[i].Data);
            }
        }
        
        /// <summary>
        /// Check gradients for overflow and update loss scale
        /// Returns true if gradients are valid, false if overflow detected
        /// </summary>
        public bool CheckAndUnscaleGradients(System.Collections.Generic.List<Tensor> parameters)
        {
            bool hasOverflow = false;
            
            // Check for overflow and unscale
            for (int i = 0; i < parameters.Count; i++)
            {
                if (parameters[i].Grad == null) continue;
                
                // Unscale gradients
                for (int j = 0; j < parameters[i].Grad.Length; j++)
                {
                    parameters[i].Grad[j] /= _lossScale;
                    
                    if (float.IsInfinity(parameters[i].Grad[j]) || float.IsNaN(parameters[i].Grad[j]))
                    {
                        hasOverflow = true;
                        break;
                    }
                }
                
                if (hasOverflow) break;
            }
            
            if (hasOverflow)
            {
                _overflowCount++;
                _lossScale /= SCALE_FACTOR;
                _stepsSinceScale = 0;
                
                // Zero out gradients
                foreach (var param in parameters)
                {
                    param.ZeroGrad();
                }
                
                return false;
            }
            
            // No overflow - try to increase scale after window
            _stepsSinceScale++;
            if (_stepsSinceScale >= SCALE_WINDOW)
            {
                _lossScale = Math.Min(_lossScale * SCALE_FACTOR, 65536f); // Cap at initial scale
                _stepsSinceScale = 0;
            }
            
            return true;
        }
        
        /// <summary>
        /// Update master weights from FP32 gradients
        /// </summary>
        public void UpdateMasterWeights(System.Collections.Generic.List<Tensor> parameters)
        {
            // Optimizer has already updated parameters[i].Data
            // Copy back to master weights
            for (int i = 0; i < parameters.Count; i++)
            {
                Array.Copy(parameters[i].Data, _masterWeights[i], parameters[i].Size);
            }
        }
    }
}
