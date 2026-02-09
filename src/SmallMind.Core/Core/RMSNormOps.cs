using System;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace SmallMind.Core.Core
{
    /// <summary>
    /// Fused RMSNorm operations with no intermediate allocations.
    /// RMSNorm normalizes using root mean square instead of mean/variance,
    /// and does not use a shift parameter (beta).
    /// Used in modern architectures like Llama, Mistral, Gemma.
    /// </summary>
    internal static class RMSNormOps
    {
        /// <summary>
        /// Fused RMSNorm: normalizes over last dimension using RMS.
        /// Formula: y = (x / rms(x)) * gamma, where rms(x) = sqrt(mean(x^2) + eps)
        /// </summary>
        /// <param name="input">Input tensor data (flattened)</param>
        /// <param name="gamma">Scale parameters (size = features)</param>
        /// <param name="output">Output buffer (can be same as input for in-place)</param>
        /// <param name="batch">Batch size (or number of sequences)</param>
        /// <param name="features">Feature dimension (normalized dimension)</param>
        /// <param name="eps">Small constant for numerical stability</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static void RMSNorm(
            ReadOnlySpan<float> input,
            ReadOnlySpan<float> gamma,
            Span<float> output,
            int batch,
            int features,
            float eps = 1e-5f)
        {
            Validation.Guard.GreaterThan(batch, 0);
            Validation.Guard.GreaterThan(features, 0);
            
            int expectedSize = batch * features;
            if (input.Length != expectedSize)
                throw new Exceptions.ValidationException($"Input length {input.Length} must equal batch * features ({expectedSize})", nameof(input));
            if (output.Length != expectedSize)
                throw new Exceptions.ValidationException($"Output length {output.Length} must equal batch * features ({expectedSize})", nameof(output));
            if (gamma.Length != features)
                throw new Exceptions.ValidationException($"Gamma length {gamma.Length} must equal features ({features})", nameof(gamma));
            
            for (int b = 0; b < batch; b++)
            {
                int offset = b * features;
                
                // Pass 1: Compute sum of squares
                float sumSq;
                
                if (System.Numerics.Vector.IsHardwareAccelerated && features >= 128)
                {
                    // SIMD path for large feature dimensions
                    int vecSize = System.Numerics.Vector<float>.Count;
                    
                    var vSqSum = System.Numerics.Vector<float>.Zero;
                    int f = 0;
                    for (; f <= features - vecSize; f += vecSize)
                    {
                        var v = new System.Numerics.Vector<float>(input.Slice(offset + f, vecSize));
                        vSqSum += v * v;
                    }
                    
                    // Horizontal sum reduction
                    sumSq = 0f;
                    for (int vi = 0; vi < vecSize; vi++)
                        sumSq += vSqSum[vi];
                    
                    // Add scalar remainder
                    for (; f < features; f++)
                    {
                        float val = input[offset + f];
                        sumSq += val * val;
                    }
                }
                else
                {
                    // Scalar path for small dimensions
                    sumSq = 0f;
                    for (int i = 0; i < features; i++)
                    {
                        float val = input[offset + i];
                        sumSq += val * val;
                    }
                }
                
                // Compute inverse RMS
                float invRms = 1f / MathF.Sqrt(sumSq / features + eps);
                
                // Pass 2: Normalize and apply scale (gamma)
                int f2 = 0;
                
                // AVX-512 path (16 floats)
                if (Avx512F.IsSupported && features >= 16)
                {
                    var vInvRms512 = Vector512.Create(invRms);
                    
                    unsafe
                    {
                        fixed (float* pInput = input, pGamma = gamma, pOutput = output)
                        {
                            for (; f2 <= features - 16; f2 += 16)
                            {
                                var vInput = Avx512F.LoadVector512(pInput + offset + f2);
                                var vGamma = Avx512F.LoadVector512(pGamma + f2);
                                
                                // gamma * (input * invRms)
                                var vNormalized = Avx512F.Multiply(vInput, vInvRms512);
                                var vResult = Avx512F.Multiply(vGamma, vNormalized);
                                Avx512F.Store(pOutput + offset + f2, vResult);
                            }
                        }
                    }
                }
                
                // AVX2 with FMA path (8 floats)
                if (Avx2.IsSupported && Fma.IsSupported && f2 <= features - 8)
                {
                    var vInvRms256 = Vector256.Create(invRms);
                    
                    unsafe
                    {
                        fixed (float* pInput = input, pGamma = gamma, pOutput = output)
                        {
                            for (; f2 <= features - 8; f2 += 8)
                            {
                                var vInput = Avx.LoadVector256(pInput + offset + f2);
                                var vGamma = Avx.LoadVector256(pGamma + f2);
                                
                                // gamma * (input * invRms)
                                var vNormalized = Avx.Multiply(vInput, vInvRms256);
                                var vResult = Avx.Multiply(vGamma, vNormalized);
                                Avx.Store(pOutput + offset + f2, vResult);
                            }
                        }
                    }
                }
                
                // Vector<T> fallback
                int vectorSize = System.Numerics.Vector<float>.Count;
                if (System.Numerics.Vector.IsHardwareAccelerated && f2 <= features - vectorSize)
                {
                    var vInvRms = new System.Numerics.Vector<float>(invRms);
                    
                    for (; f2 <= features - vectorSize; f2 += vectorSize)
                    {
                        var vInput = new System.Numerics.Vector<float>(input.Slice(offset + f2, vectorSize));
                        var vGamma = new System.Numerics.Vector<float>(gamma.Slice(f2, vectorSize));
                        
                        // gamma * (input * invRms)
                        var vNormalized = vInput * vInvRms;
                        var vResult = vGamma * vNormalized;
                        
                        vResult.CopyTo(output.Slice(offset + f2, vectorSize));
                    }
                }
                
                // Scalar remainder
                for (; f2 < features; f2++)
                {
                    float normalized = input[offset + f2] * invRms;
                    output[offset + f2] = gamma[f2] * normalized;
                }
            }
        }
        
        /// <summary>
        /// Fused RMSNorm for 3D tensors (batch, sequence, features).
        /// </summary>
        public static void RMSNorm3D(
            ReadOnlySpan<float> input,
            ReadOnlySpan<float> gamma,
            Span<float> output,
            int batch,
            int sequence,
            int features,
            float eps = 1e-5f)
        {
            Validation.Guard.GreaterThan(batch, 0);
            Validation.Guard.GreaterThan(sequence, 0);
            Validation.Guard.GreaterThan(features, 0);
            
            int totalBatch = batch * sequence;
            RMSNorm(input, gamma, output, totalBatch, features, eps);
        }
        
        /// <summary>
        /// In-place RMSNorm: normalizes tensor in-place.
        /// </summary>
        public static void RMSNormInPlace(
            Span<float> data,
            ReadOnlySpan<float> gamma,
            int batch,
            int features,
            float eps = 1e-5f)
        {
            RMSNorm(data, gamma, data, batch, features, eps);
        }
        
        /// <summary>
        /// Fused RMSNorm with residual connection: output = RMSNorm(input + residual).
        /// High-ROI fusion that combines residual add + normalization.
        /// </summary>
        public static void RMSNormResidual(
            ReadOnlySpan<float> input,
            ReadOnlySpan<float> residual,
            ReadOnlySpan<float> gamma,
            Span<float> output,
            int batch,
            int features,
            float eps = 1e-5f)
        {
            Validation.Guard.GreaterThan(batch, 0);
            Validation.Guard.GreaterThan(features, 0);
            
            int expectedSize = batch * features;
            if (input.Length != expectedSize)
                throw new Exceptions.ValidationException($"Input length {input.Length} must equal batch * features ({expectedSize})", nameof(input));
            if (residual.Length != expectedSize)
                throw new Exceptions.ValidationException($"Residual length {residual.Length} must equal batch * features ({expectedSize})", nameof(residual));
            if (output.Length != expectedSize)
                throw new Exceptions.ValidationException($"Output length {output.Length} must equal batch * features ({expectedSize})", nameof(output));
            if (gamma.Length != features)
                throw new Exceptions.ValidationException($"Gamma length {gamma.Length} must equal features ({features})", nameof(gamma));
            
            for (int b = 0; b < batch; b++)
            {
                int offset = b * features;
                
                // Pass 1: Compute sum of squares of (input + residual)
                float sumSq;
                
                if (System.Numerics.Vector.IsHardwareAccelerated && features >= System.Numerics.Vector<float>.Count * 2)
                {
                    int vectorSize = System.Numerics.Vector<float>.Count;
                    
                    var vSqSum = System.Numerics.Vector<float>.Zero;
                    int f = 0;
                    for (; f <= features - vectorSize; f += vectorSize)
                    {
                        var vIn = new System.Numerics.Vector<float>(input.Slice(offset + f, vectorSize));
                        var vRes = new System.Numerics.Vector<float>(residual.Slice(offset + f, vectorSize));
                        var vCombined = vIn + vRes;
                        vSqSum += vCombined * vCombined;
                    }
                    
                    sumSq = 0f;
                    for (int vi = 0; vi < vectorSize; vi++)
                        sumSq += vSqSum[vi];
                    
                    for (; f < features; f++)
                    {
                        float val = input[offset + f] + residual[offset + f];
                        sumSq += val * val;
                    }
                }
                else
                {
                    // Scalar path
                    sumSq = 0f;
                    for (int i = 0; i < features; i++)
                    {
                        float val = input[offset + i] + residual[offset + i];
                        sumSq += val * val;
                    }
                }
                
                float invRms = 1f / MathF.Sqrt(sumSq / features + eps);
                
                // Pass 2: Normalize (input + residual) and apply scale
                int f2 = 0;
                
                // AVX2 with FMA path (8 floats)
                if (Avx2.IsSupported && Fma.IsSupported && features >= 8)
                {
                    var vInvRms256 = Vector256.Create(invRms);
                    
                    unsafe
                    {
                        fixed (float* pInput = input, pResidual = residual, pGamma = gamma, pOutput = output)
                        {
                            for (; f2 <= features - 8; f2 += 8)
                            {
                                var vInput = Avx.LoadVector256(pInput + offset + f2);
                                var vResidual = Avx.LoadVector256(pResidual + offset + f2);
                                var vGamma = Avx.LoadVector256(pGamma + f2);
                                
                                // gamma * ((input + residual) * invRms)
                                var vCombined = Avx.Add(vInput, vResidual);
                                var vNormalized = Avx.Multiply(vCombined, vInvRms256);
                                var vResult = Avx.Multiply(vGamma, vNormalized);
                                Avx.Store(pOutput + offset + f2, vResult);
                            }
                        }
                    }
                }
                
                if (System.Numerics.Vector.IsHardwareAccelerated && f2 <= features - System.Numerics.Vector<float>.Count)
                {
                    int vectorSize = System.Numerics.Vector<float>.Count;
                    var vInvRms = new System.Numerics.Vector<float>(invRms);
                    
                    for (; f2 <= features - vectorSize; f2 += vectorSize)
                    {
                        var vInput = new System.Numerics.Vector<float>(input.Slice(offset + f2, vectorSize));
                        var vResidual = new System.Numerics.Vector<float>(residual.Slice(offset + f2, vectorSize));
                        var vGamma = new System.Numerics.Vector<float>(gamma.Slice(f2, vectorSize));
                        
                        // gamma * ((input + residual) * invRms)
                        var vCombined = vInput + vResidual;
                        var vNormalized = vCombined * vInvRms;
                        var vResult = vGamma * vNormalized;
                        
                        vResult.CopyTo(output.Slice(offset + f2, vectorSize));
                    }
                }
                
                // Scalar remainder
                for (; f2 < features; f2++)
                {
                    float combined = input[offset + f2] + residual[offset + f2];
                    float normalized = combined * invRms;
                    output[offset + f2] = gamma[f2] * normalized;
                }
            }
        }
    }
}
