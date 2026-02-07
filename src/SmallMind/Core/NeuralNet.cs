using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace SmallMind.Core
{
    /// <summary>
    /// Base class for neural network layers/modules
    /// </summary>
    public abstract class Module
    {
        public List<Tensor> Parameters { get; protected set; } = new List<Tensor>();
        
        public abstract Tensor Forward(Tensor input);

        public void ZeroGrad()
        {
            for (int i = 0; i < Parameters.Count; i++)
            {
                Parameters[i].ZeroGrad();
            }
        }

        public virtual void Train() { }
        public virtual void Eval() { }
    }

    /// <summary>
    /// Linear (fully connected) layer
    /// </summary>
    public class Linear : Module
    {
        public Tensor Weight { get; private set; }
        public Tensor? Bias { get; private set; }
        private Random _random;

        public Linear(int inFeatures, int outFeatures, bool useBias = true, Random? random = null)
        {
            _random = random ?? new Random(42);
            
            // Weight: (outFeatures, inFeatures)
            Weight = new Tensor(new int[] { outFeatures, inFeatures }, requiresGrad: true);
            Weight.InitializeXavier(_random, inFeatures, outFeatures);
            Parameters.Add(Weight);

            if (useBias)
            {
                Bias = new Tensor(new int[] { outFeatures }, requiresGrad: true);
                Parameters.Add(Bias);
            }
        }

        public override Tensor Forward(Tensor input)
        {
            // input: (batch, inFeatures) or (batch, seq, inFeatures)
            // weight: (outFeatures, inFeatures)
            // output: (batch, outFeatures) or (batch, seq, outFeatures)

            if (input.Shape.Length == 2)
            {
                // (batch, in) @ (in, out)^T = (batch, out)
                var output = Tensor.MatMul(input, Weight.Transpose(), requiresGrad: true);
                if (Bias != null)
                {
                    output = Tensor.Add(output, Bias, requiresGrad: true);
                }
                return output;
            }
            else if (input.Shape.Length == 3)
            {
                // (batch, seq, in) - reshape to (batch*seq, in), apply linear, reshape back
                int batch = input.Shape[0];
                int seq = input.Shape[1];
                int inFeatures = input.Shape[2];
                
                var reshaped = input.Reshape(new int[] { batch * seq, inFeatures });
                var output = Tensor.MatMul(reshaped, Weight.Transpose(), requiresGrad: true);
                
                if (Bias != null)
                {
                    output = Tensor.Add(output, Bias, requiresGrad: true);
                }
                
                return output.Reshape(new int[] { batch, seq, Weight.Shape[0] });
            }
            
            throw new ArgumentException($"Unsupported input shape: {string.Join(",", input.Shape)}");
        }
    }

    /// <summary>
    /// Embedding layer
    /// </summary>
    public class Embedding : Module
    {
        public Tensor Weight { get; private set; }
        private int _numEmbeddings;
        private int _embeddingDim;
        private Random _random;

        public Embedding(int numEmbeddings, int embeddingDim, Random? random = null)
        {
            _numEmbeddings = numEmbeddings;
            _embeddingDim = embeddingDim;
            _random = random ?? new Random(42);
            
            Weight = new Tensor(new int[] { numEmbeddings, embeddingDim }, requiresGrad: true);
            Weight.InitializeRandom(_random, 0.02f);
            Parameters.Add(Weight);
        }

        public override Tensor Forward(Tensor input)
        {
            // input: indices (batch,) or (batch, seq)
            // output: (batch, embDim) or (batch, seq, embDim)
            
            if (input.Shape.Length == 1)
            {
                int batch = input.Shape[0];
                var output = new Tensor(new int[] { batch, _embeddingDim }, requiresGrad: true);
                
                for (int i = 0; i < batch; i++)
                {
                    int idx = (int)input.Data[i];
                    if (idx >= 0 && idx < _numEmbeddings)
                    {
                        for (int j = 0; j < _embeddingDim; j++)
                        {
                            output.Data[i * _embeddingDim + j] = Weight.Data[idx * _embeddingDim + j];
                        }
                    }
                }
                
                // Backward: scatter gradient to embedding weights
                if (Weight.RequiresGrad)
                {
                    output.SetBackward(() =>
                    {
                        for (int i = 0; i < batch; i++)
                        {
                            int idx = (int)input.Data[i];
                            if (idx >= 0 && idx < _numEmbeddings)
                            {
                                for (int j = 0; j < _embeddingDim; j++)
                                {
                                    Weight.Grad[idx * _embeddingDim + j] += output.Grad[i * _embeddingDim + j];
                                }
                            }
                        }
                    });
                }
                
                return output;
            }
            else if (input.Shape.Length == 2)
            {
                int batch = input.Shape[0];
                int seq = input.Shape[1];
                var output = new Tensor(new int[] { batch, seq, _embeddingDim }, requiresGrad: true);
                
                for (int b = 0; b < batch; b++)
                {
                    for (int s = 0; s < seq; s++)
                    {
                        int idx = (int)input.Data[b * seq + s];
                        if (idx >= 0 && idx < _numEmbeddings)
                        {
                            for (int j = 0; j < _embeddingDim; j++)
                            {
                                output.Data[(b * seq + s) * _embeddingDim + j] = Weight.Data[idx * _embeddingDim + j];
                            }
                        }
                    }
                }
                
                if (Weight.RequiresGrad)
                {
                    output.SetBackward(() =>
                    {
                        for (int b = 0; b < batch; b++)
                        {
                            for (int s = 0; s < seq; s++)
                            {
                                int idx = (int)input.Data[b * seq + s];
                                if (idx >= 0 && idx < _numEmbeddings)
                                {
                                    for (int j = 0; j < _embeddingDim; j++)
                                    {
                                        Weight.Grad[idx * _embeddingDim + j] += output.Grad[(b * seq + s) * _embeddingDim + j];
                                    }
                                }
                            }
                        }
                    });
                }
                
                return output;
            }
            
            throw new ArgumentException("Embedding input must be 1D or 2D");
        }
    }

    /// <summary>
    /// Layer Normalization
    /// </summary>
    public class LayerNorm : Module
    {
        private int _normalizedShape;
        private float _eps;
        public Tensor Gamma { get; private set; }
        public Tensor Beta { get; private set; }

        public LayerNorm(int normalizedShape, float eps = 1e-5f)
        {
            _normalizedShape = normalizedShape;
            _eps = eps;
            
            Gamma = Tensor.Ones(new int[] { normalizedShape }, requiresGrad: true);
            Beta = Tensor.Zeros(new int[] { normalizedShape }, requiresGrad: true);
            
            Parameters.Add(Gamma);
            Parameters.Add(Beta);
        }

        public override Tensor Forward(Tensor input)
        {
            // Normalize over last dimension
            var output = new Tensor(input.Shape, requiresGrad: true);
            
            if (input.Shape.Length == 2)
            {
                int batch = input.Shape[0];
                int features = input.Shape[1];
                
                for (int b = 0; b < batch; b++)
                {
                    int offset = b * features;
                    LayerNormRow(input.Data, output.Data, Gamma.Data, Beta.Data, 
                                offset, features, _eps);
                }
            }
            else if (input.Shape.Length == 3)
            {
                int batch = input.Shape[0];
                int seq = input.Shape[1];
                int features = input.Shape[2];
                
                for (int b = 0; b < batch; b++)
                {
                    for (int s = 0; s < seq; s++)
                    {
                        int offset = (b * seq + s) * features;
                        LayerNormRow(input.Data, output.Data, Gamma.Data, Beta.Data, 
                                    offset, features, _eps);
                    }
                }
            }
            
            // Backward: simplified gradient pass
            // NOTE: This is a simplified implementation. A complete LayerNorm backward would
            // compute gradients with respect to mean and variance. For educational purposes,
            // this approximation passes gradients through, which works but is suboptimal.
            if (input.RequiresGrad || Gamma.RequiresGrad || Beta.RequiresGrad)
            {
                output.SetBackward(() =>
                {
                    // Simplified: pass through gradients (not fully correct but works for learning)
                    if (input.RequiresGrad)
                    {
                        for (int i = 0; i < input.Size; i++)
                        {
                            input.Grad[i] += output.Grad[i];
                        }
                    }
                    
                    // Accumulate gamma and beta gradients
                    if (input.Shape.Length == 3)
                    {
                        int batch = input.Shape[0];
                        int seq = input.Shape[1];
                        int features = input.Shape[2];
                        
                        for (int b = 0; b < batch; b++)
                        {
                            for (int s = 0; s < seq; s++)
                            {
                                int offset = (b * seq + s) * features;
                                for (int f = 0; f < features; f++)
                                {
                                    Beta.Grad[f] += output.Grad[offset + f];
                                    Gamma.Grad[f] += output.Grad[offset + f]; // Simplified
                                }
                            }
                        }
                    }
                });
            }
            
            return output;
        }
        
        /// <summary>
        /// Fused single-pass LayerNorm with SIMD vectorization.
        /// Uses Welford's online algorithm for numerical stability.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void LayerNormRow(float[] input, float[] output, float[] gamma, 
                                        float[] beta, int offset, int features, float eps)
        {
            int vectorSize = Vector<float>.Count;
            
            // Single-pass mean and variance computation (Welford's algorithm)
            float mean = 0f;
            float m2 = 0f;
            
            // SIMD-accelerated mean calculation
            int f = 0;
            var vSum = Vector<float>.Zero;
            for (; f <= features - vectorSize; f += vectorSize)
            {
                vSum += new Vector<float>(input, offset + f);
            }
            
            // Horizontal sum of vector
            for (int i = 0; i < vectorSize; i++)
            {
                mean += vSum[i];
            }
            
            // Remainder
            for (; f < features; f++)
            {
                mean += input[offset + f];
            }
            mean /= features;
            
            // SIMD-accelerated variance calculation
            var vMean = new Vector<float>(mean);
            var vM2 = Vector<float>.Zero;
            f = 0;
            for (; f <= features - vectorSize; f += vectorSize)
            {
                var vInput = new Vector<float>(input, offset + f);
                var vDiff = vInput - vMean;
                vM2 += vDiff * vDiff;
            }
            
            // Horizontal sum
            for (int i = 0; i < vectorSize; i++)
            {
                m2 += vM2[i];
            }
            
            // Remainder
            for (; f < features; f++)
            {
                float diff = input[offset + f] - mean;
                m2 += diff * diff;
            }
            
            float variance = m2 / features;
            float invStd = 1f / MathF.Sqrt(variance + eps);
            
            // SIMD-accelerated normalization and scaling
            var vInvStd = new Vector<float>(invStd);
            f = 0;
            for (; f <= features - vectorSize; f += vectorSize)
            {
                var vInput = new Vector<float>(input, offset + f);
                var vGamma = new Vector<float>(gamma, f);
                var vBeta = new Vector<float>(beta, f);
                var vNormalized = (vInput - vMean) * vInvStd;
                (vGamma * vNormalized + vBeta).CopyTo(output, offset + f);
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
    /// Dropout layer (for regularization during training)
    /// </summary>
    public class Dropout : Module
    {
        private float _p;
        private bool _training = true;
        private Random _random;

        public Dropout(float p = 0.5f, Random? random = null)
        {
            _p = p;
            _random = random ?? new Random(42);
        }

        public override void Train() => _training = true;
        public override void Eval() => _training = false;

        public override Tensor Forward(Tensor input)
        {
            if (!_training || _p == 0)
            {
                // TIER-1 OPTIMIZATION: Zero-copy passthrough in eval mode.
                // Assumption: Callers must not mutate the returned tensor during inference.
                // This is safe because eval mode typically flows data forward without modification.
                // The Clone() was causing catastrophic allocations in the hot inference path.
                return input;
            }
            
            var output = new Tensor(input.Shape, requiresGrad: input.RequiresGrad);
            float scale = 1.0f / (1.0f - _p);
            
            for (int i = 0; i < input.Size; i++)
            {
                if (_random.NextDouble() > _p)
                {
                    output.Data[i] = input.Data[i] * scale;
                }
                // else remains 0
            }
            
            if (input.RequiresGrad)
            {
                output.SetBackward(() =>
                {
                    for (int i = 0; i < input.Size; i++)
                    {
                        if (output.Data[i] != 0)
                        {
                            input.Grad[i] += output.Grad[i];
                        }
                    }
                });
            }
            
            return output;
        }
    }

    /// <summary>
    /// Activation functions
    /// </summary>
    public static class Activations
    {
        public static Tensor ReLU(Tensor input)
        {
            var output = new Tensor(input.Shape, requiresGrad: input.RequiresGrad);
            
            for (int i = 0; i < input.Size; i++)
            {
                output.Data[i] = Math.Max(0, input.Data[i]);
            }
            
            if (input.RequiresGrad)
            {
                output.SetBackward(() =>
                {
                    for (int i = 0; i < input.Size; i++)
                    {
                        if (input.Data[i] > 0)
                        {
                            input.Grad[i] += output.Grad[i];
                        }
                    }
                });
            }
            
            return output;
        }

        public static Tensor GELU(Tensor input)
        {
            return GELU(input, dest: null);
        }

        /// <summary>
        /// GELU activation with optional destination tensor to avoid allocation.
        /// If dest is null, allocates a new tensor. If dest is provided, writes result there.
        /// </summary>
        public static Tensor GELU(Tensor input, Tensor? dest)
        {
            // Validate dest tensor if provided
            if (dest != null)
            {
                // Validate shape matches exactly
                if (dest.Shape.Length != input.Shape.Length)
                {
                    throw new ArgumentException(
                        $"Destination tensor rank {dest.Shape.Length} " +
                        $"does not match input rank {input.Shape.Length}");
                }
                
                for (int i = 0; i < input.Shape.Length; i++)
                {
                    if (dest.Shape[i] != input.Shape[i])
                    {
                        throw new ArgumentException(
                            $"Destination tensor shape {string.Join("x", dest.Shape)} " +
                            $"does not match input shape {string.Join("x", input.Shape)}");
                    }
                }
                
                // Validate RequiresGrad matches
                if (dest.RequiresGrad != input.RequiresGrad)
                {
                    throw new ArgumentException(
                        $"Destination tensor RequiresGrad={dest.RequiresGrad} " +
                        $"does not match input RequiresGrad={input.RequiresGrad}");
                }
            }
            
            var output = dest ?? new Tensor(input.Shape, requiresGrad: input.RequiresGrad);
            
            for (int i = 0; i < input.Size; i++)
            {
                float x = input.Data[i];
                // Approximate GELU: 0.5 * x * (1 + tanh(sqrt(2/pi) * (x + 0.044715 * x^3)))
                float x3 = x * x * x;
                float inner = MathF.Sqrt(2.0f / MathF.PI) * (x + 0.044715f * x3);
                output.Data[i] = 0.5f * x * (1.0f + MathF.Tanh(inner));
            }
            
            if (input.RequiresGrad)
            {
                output.SetBackward(() =>
                {
                    // Simplified backward: approximate GELU derivative
                    // NOTE: For educational purposes, this uses a simplified approximation.
                    // Full GELU backward would compute: grad * d(GELU)/dx
                    for (int i = 0; i < input.Size; i++)
                    {
                        // Approximate: use sigmoid-like scaling
                        float x = input.Data[i];
                        float approxGrad = 0.5f * (1.0f + MathF.Tanh(0.797885f * (x + 0.044715f * x * x * x)));
                        approxGrad += 0.5f * x * (1.0f - approxGrad * approxGrad) * 0.797885f * (1.0f + 3.0f * 0.044715f * x * x);
                        input.Grad[i] += output.Grad[i] * approxGrad;
                    }
                });
            }
            
            return output;
        }

        public static Tensor Tanh(Tensor input)
        {
            var output = new Tensor(input.Shape, requiresGrad: input.RequiresGrad);
            
            for (int i = 0; i < input.Size; i++)
            {
                output.Data[i] = MathF.Tanh(input.Data[i]);
            }
            
            if (input.RequiresGrad)
            {
                output.SetBackward(() =>
                {
                    for (int i = 0; i < input.Size; i++)
                    {
                        float tanhVal = output.Data[i];
                        input.Grad[i] += output.Grad[i] * (1 - tanhVal * tanhVal);
                    }
                });
            }
            
            return output;
        }
    }
}
