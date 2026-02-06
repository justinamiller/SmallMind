using System;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace SmallMind.Core.Core
{
    /// <summary>
    /// Fused LayerNorm operations with no intermediate allocations.
    /// Implements efficient two-pass normalization over last dimension.
    /// </summary>
    public static class LayerNormOps
    {
        /// <summary>
        /// Fused LayerNorm: normalizes over last dimension with optional in-place operation.
        /// Two-pass algorithm: 1) compute mean/variance, 2) normalize + affine transform.
        /// </summary>
        /// <param name="input">Input tensor data (flattened)</param>
        /// <param name="gamma">Scale parameters (size = features)</param>
        /// <param name="beta">Shift parameters (size = features)</param>
        /// <param name="output">Output buffer (can be same as input for in-place)</param>
        /// <param name="batch">Batch size (or number of sequences)</param>
        /// <param name="features">Feature dimension (normalized dimension)</param>
        /// <param name="eps">Small constant for numerical stability</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LayerNorm(
            ReadOnlySpan<float> input,
            ReadOnlySpan<float> gamma,
            ReadOnlySpan<float> beta,
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
            if (beta.Length != features)
                throw new Exceptions.ValidationException($"Beta length {beta.Length} must equal features ({features})", nameof(beta));
            
            int vectorSize = System.Numerics.Vector<float>.Count;
            
            for (int b = 0; b < batch; b++)
            {
                int offset = b * features;
                
                // Pass 1: Compute mean and variance (Welford's online algorithm for better numerical stability)
                float mean = 0f;
                float m2 = 0f;
                
                for (int i = 0; i < features; i++)
                {
                    float val = input[offset + i];
                    float delta = val - mean;
                    mean += delta / (i + 1);
                    float delta2 = val - mean;
                    m2 += delta * delta2;
                }
                
                float variance = m2 / features;
                float invStd = 1f / MathF.Sqrt(variance + eps);
                
                // Pass 2: Normalize and apply affine transformation (SIMD optimized)
                int f = 0;
                
                // AVX-512 path (16 floats)
                if (Avx512F.IsSupported && features >= 16)
                {
                    var vMean512 = Vector512.Create(mean);
                    var vInvStd512 = Vector512.Create(invStd);
                    
                    unsafe
                    {
                        fixed (float* pInput = input, pGamma = gamma, pBeta = beta, pOutput = output)
                        {
                            for (; f <= features - 16; f += 16)
                            {
                                var vInput = Avx512F.LoadVector512(pInput + offset + f);
                                var vGamma = Avx512F.LoadVector512(pGamma + f);
                                var vBeta = Avx512F.LoadVector512(pBeta + f);
                                
                                var vNormalized = Avx512F.Multiply(Avx512F.Subtract(vInput, vMean512), vInvStd512);
                                var vResult = Avx512F.FusedMultiplyAdd(vGamma, vNormalized, vBeta);
                                Avx512F.Store(pOutput + offset + f, vResult);
                            }
                        }
                    }
                }
                
                // Vector<T> fallback
                int vectorSize = System.Numerics.Vector<float>.Count;
                if (System.Numerics.Vector.IsHardwareAccelerated && f <= features - vectorSize)
                {
                    var vMean = new System.Numerics.Vector<float>(mean);
                    var vInvStd = new System.Numerics.Vector<float>(invStd);
                    
                    // SIMD loop for normalization and affine transform
                    for (; f <= features - vectorSize; f += vectorSize)
                    {
                        // Load input, gamma, beta
                        var vInput = new System.Numerics.Vector<float>(input.Slice(offset + f, vectorSize));
                        var vGamma = new System.Numerics.Vector<float>(gamma.Slice(f, vectorSize));
                        var vBeta = new System.Numerics.Vector<float>(beta.Slice(f, vectorSize));
                        
                        // Normalize: (input - mean) * invStd
                        var vNormalized = (vInput - vMean) * vInvStd;
                        
                        // Affine: gamma * normalized + beta
                        var vResult = vGamma * vNormalized + vBeta;
                        
                        vResult.CopyTo(output.Slice(offset + f, vectorSize));
                    }
                }
                
                // Scalar remainder
                for (; f < features; f++)
                {
                    float normalized = (input[offset + f] - mean) * invStd;
                    output[offset + f] = gamma[f] * normalized + beta[f];
                }
            }
        }
        
        /// <summary>
        /// Fused LayerNorm for 3D tensors (batch, sequence, features).
        /// </summary>
        public static void LayerNorm3D(
            ReadOnlySpan<float> input,
            ReadOnlySpan<float> gamma,
            ReadOnlySpan<float> beta,
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
            LayerNorm(input, gamma, beta, output, totalBatch, features, eps);
        }
        
        /// <summary>
        /// Fused LayerNorm with residual connection: output = LayerNorm(input + residual).
        /// High-ROI fusion that combines residual add + normalization.
        /// </summary>
        public static void LayerNormResidual(
            ReadOnlySpan<float> input,
            ReadOnlySpan<float> residual,
            ReadOnlySpan<float> gamma,
            ReadOnlySpan<float> beta,
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
            if (beta.Length != features)
                throw new Exceptions.ValidationException($"Beta length {beta.Length} must equal features ({features})", nameof(beta));
            
            for (int b = 0; b < batch; b++)
            {
                int offset = b * features;
                
                // Pass 1: Compute mean and variance of (input + residual)
                float mean = 0f;
                float m2 = 0f;
                
                for (int f = 0; f < features; f++)
                {
                    float val = input[offset + f] + residual[offset + f];
                    float delta = val - mean;
                    mean += delta / (f + 1);
                    float delta2 = val - mean;
                    m2 += delta * delta2;
                }
                
                float variance = m2 / features;
                float invStd = 1f / MathF.Sqrt(variance + eps);
                
                // Pass 2: Normalize (input + residual) and apply affine transformation
                for (int f = 0; f < features; f++)
                {
                    float combined = input[offset + f] + residual[offset + f];
                    float normalized = (combined - mean) * invStd;
                    output[offset + f] = gamma[f] * normalized + beta[f];
                }
            }
        }
        
        /// <summary>
        /// In-place LayerNorm: normalizes tensor in-place.
        /// </summary>
        public static void LayerNormInPlace(
            Span<float> data,
            ReadOnlySpan<float> gamma,
            ReadOnlySpan<float> beta,
            int batch,
            int features,
            float eps = 1e-5f)
        {
            LayerNorm(data, gamma, beta, data, batch, features, eps);
        }
    }
}
