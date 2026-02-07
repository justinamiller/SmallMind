using SmallMind.Core.Exceptions;
using SmallMind.Core.Core;
using SmallMind.Core.Simd;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SmallMind.Transformers
{
    /// <summary>
    /// Base class for neural network layers/modules
    /// </summary>
    public abstract class Module
    {
        public List<Tensor> Parameters { get; protected set; } = new List<Tensor>();
        
        /// <summary>
        /// Whether the module is in training mode (affects gradient computation and dropout).
        /// </summary>
        protected bool IsTraining { get; private set; } = true;
        
        public abstract Tensor Forward(Tensor input);

        public void ZeroGrad()
        {
            for (int i = 0; i < Parameters.Count; i++)
            {
                Parameters[i].ZeroGrad();
            }
        }

        public virtual void Train() 
        { 
            IsTraining = true;
        }
        
        public virtual void Eval() 
        { 
            IsTraining = false;
        }
    }

    /// <summary>
    /// Linear (fully connected) layer
    /// </summary>
    public sealed class Linear : Module
    {
        public Tensor Weight { get; private set; }
        public Tensor? Bias { get; private set; }
        private Random _random;
        
        // Cached transposed weight for inference (Tier-0 optimization: eliminate per-call transpose allocation)
        private Tensor? _weightTransposeCache;

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
            return Forward(input, dest: null);
        }
        
        /// <summary>
        /// Forward pass with optional destination tensor to avoid allocation.
        /// If dest is null, allocates a new tensor. If dest is provided, writes result there.
        /// Note: For 3D inputs, dest must have the final reshaped shape (batch, seq, outFeatures)
        /// </summary>
        public Tensor Forward(Tensor input, Tensor? dest)
        {
            // input: (batch, inFeatures) or (batch, seq, inFeatures)
            // weight: (outFeatures, inFeatures)
            // output: (batch, outFeatures) or (batch, seq, outFeatures)

            // TIER-3 OPTIMIZATION: Use precomputed transpose cache in inference
            // Cache is precomputed in Eval(), no lazy allocation during forward pass
            Tensor weightT;
            if (IsTraining)
            {
                weightT = Weight.Transpose();
            }
            else
            {
                // Use precomputed cache (should never be null after Eval())
                weightT = _weightTransposeCache!;
            }

            if (input.Shape.Length == 2)
            {
                // (batch, in) @ (in, out)^T = (batch, out)
                var output = Tensor.MatMul(input, weightT, dest, requiresGrad: IsTraining);
                if (Bias != null)
                {
                    // Add bias in-place to output
                    output = Tensor.Add(output, Bias, output, requiresGrad: IsTraining);
                }
                return output;
            }
            else if (input.Shape.Length == 3)
            {
                // (batch, seq, in) - reshape to (batch*seq, in), apply linear, reshape back
                int batch = input.Shape[0];
                int seq = input.Shape[1];
                int inFeatures = input.Shape[2];
                int outFeatures = Weight.Shape[0];
                
                // Tier-0 optimization: Use ReshapeView instead of Reshape to avoid cloning
                var reshaped = input.ReshapeView(new int[] { batch * seq, inFeatures });
                
                // Tier-0 optimization: Allow dest usage for 3D case
                // Create intermediate buffer if dest is null, otherwise use dest reshaped as view
                Tensor? intermediateBuffer = null;
                if (dest != null)
                {
                    // Use dest as backing storage via view
                    intermediateBuffer = dest.ReshapeView(new int[] { batch * seq, outFeatures });
                }
                
                var output = Tensor.MatMul(reshaped, weightT, intermediateBuffer, requiresGrad: IsTraining);
                
                if (Bias != null)
                {
                    output = Tensor.Add(output, Bias, output, requiresGrad: IsTraining);
                }
                
                // Tier-0 optimization: Use ReshapeView to return final shape without cloning
                return output.ReshapeView(new int[] { batch, seq, outFeatures });
            }
            
            throw new ArgumentException($"Unsupported input shape: {string.Join(",", input.Shape)}");
        }
        
        public override void Train()
        {
            base.Train();
            // Invalidate transpose cache when switching to training mode
            _weightTransposeCache = null;
        }
        
        /// <summary>
        /// TIER-3 OPTIMIZATION: Precompute transpose cache at Eval() time
        /// Ensures transpose is computed once when entering inference mode,
        /// not lazily on first forward pass
        /// </summary>
        public override void Eval()
        {
            base.Eval();
            // Precompute transpose cache for inference
            if (_weightTransposeCache == null)
            {
                _weightTransposeCache = Weight.Transpose();
            }
        }
    }

    /// <summary>
    /// Embedding layer
    /// </summary>
    public sealed class Embedding : Module
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
            
            // Check if we need chunked storage
            long totalElements = (long)numEmbeddings * embeddingDim;
            
            if (totalElements > int.MaxValue)
            {
                // Use chunked storage for large embedding tables
                Weight = Tensor.CreateChunked(new int[] { numEmbeddings, embeddingDim }, requiresGrad: true);
                Weight.InitializeRandom(_random, 0.02f);
                Console.WriteLine($"Embedding using chunked storage: {numEmbeddings:N0} x {embeddingDim:N0} = {totalElements:N0} elements ({Weight.GetChunkedBuffer().ChunkCount} chunks)");
            }
            else
            {
                // Use traditional dense storage
                Weight = new Tensor(new int[] { numEmbeddings, embeddingDim }, requiresGrad: true);
                Weight.InitializeRandom(_random, 0.02f);
            }
            
            Parameters.Add(Weight);
        }

        public override Tensor Forward(Tensor input)
        {
            return Forward(input, dest: null);
        }
        
        /// <summary>
        /// Forward pass with optional destination tensor to avoid allocation.
        /// If dest is null, allocates a new tensor. If dest is provided, writes result there.
        /// </summary>
        public Tensor Forward(Tensor input, Tensor? dest)
        {
            // input: indices (batch,) or (batch, seq)
            // output: (batch, embDim) or (batch, seq, embDim)
            
            if (input.Shape.Length == 1)
            {
                return ForwardBatch(input, dest);
            }
            else if (input.Shape.Length == 2)
            {
                return ForwardBatchSeq(input, dest);
            }
            
            throw new ArgumentException("Embedding input must be 1D or 2D");
        }

        private Tensor ForwardBatch(Tensor input, Tensor? dest = null)
        {
            int batch = input.Shape[0];
            var output = dest ?? new Tensor(new int[] { batch, _embeddingDim }, requiresGrad: IsTraining);
            
            if (Weight.IsChunked)
            {
                // Chunked embedding lookup
                var weightBuffer = Weight.GetChunkedBuffer();
                
                for (int i = 0; i < batch; i++)
                {
                    int idx = (int)input.Data[i];
                    if (idx >= 0 && idx < _numEmbeddings)
                    {
                        long srcIndex = (long)idx * _embeddingDim;
                        int dstOffset = i * _embeddingDim;
                        
                        // Copy embedding row from chunked storage
                        Weight.CopyTo(srcIndex, output.Data.AsSpan(dstOffset, _embeddingDim), _embeddingDim);
                    }
                }
                
                // Backward: scatter gradient to embedding weights
                if (Weight.RequiresGrad)
                {
                    output.SetBackward(() =>
                    {
                        var gradBuffer = Weight.GetChunkedGradBuffer();
                        for (int i = 0; i < batch; i++)
                        {
                            int idx = (int)input.Data[i];
                            if (idx >= 0 && idx < _numEmbeddings)
                            {
                                long dstIndex = (long)idx * _embeddingDim;
                                int srcOffset = i * _embeddingDim;
                                
                                // Accumulate gradients to chunked storage
                                AccumulateGradients(output.Grad.AsSpan(srcOffset, _embeddingDim), dstIndex, gradBuffer);
                            }
                        }
                    });
                }
            }
            else
            {
                // Dense embedding lookup (original implementation)
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
            }
            
            return output;
        }

        private Tensor ForwardBatchSeq(Tensor input, Tensor? dest = null)
        {
            int batch = input.Shape[0];
            int seq = input.Shape[1];
            var output = dest ?? new Tensor(new int[] { batch, seq, _embeddingDim }, requiresGrad: IsTraining);
            
            if (Weight.IsChunked)
            {
                // Chunked embedding lookup
                var weightBuffer = Weight.GetChunkedBuffer();
                
                // Parallelize over batch when beneficial
                if (batch >= 4)
                {
                    Parallel.For(0, batch, b =>
                    {
                        for (int s = 0; s < seq; s++)
                        {
                            int idx = (int)input.Data[b * seq + s];
                            if (idx >= 0 && idx < _numEmbeddings)
                            {
                                long srcIndex = (long)idx * _embeddingDim;
                                int dstOffset = (b * seq + s) * _embeddingDim;
                                
                                // Copy embedding row from chunked storage
                                Weight.CopyTo(srcIndex, output.Data.AsSpan(dstOffset, _embeddingDim), _embeddingDim);
                            }
                        }
                    });
                }
                else
                {
                    for (int b = 0; b < batch; b++)
                    {
                        for (int s = 0; s < seq; s++)
                        {
                            int idx = (int)input.Data[b * seq + s];
                            if (idx >= 0 && idx < _numEmbeddings)
                            {
                                long srcIndex = (long)idx * _embeddingDim;
                                int dstOffset = (b * seq + s) * _embeddingDim;
                                
                                // Copy embedding row from chunked storage
                                Weight.CopyTo(srcIndex, output.Data.AsSpan(dstOffset, _embeddingDim), _embeddingDim);
                            }
                        }
                    }
                }
                
                // Backward: scatter gradient to embedding weights
                if (Weight.RequiresGrad)
                {
                    output.SetBackward(() =>
                    {
                        var gradBuffer = Weight.GetChunkedGradBuffer();
                        for (int b = 0; b < batch; b++)
                        {
                            for (int s = 0; s < seq; s++)
                            {
                                int idx = (int)input.Data[b * seq + s];
                                if (idx >= 0 && idx < _numEmbeddings)
                                {
                                    long dstIndex = (long)idx * _embeddingDim;
                                    int srcOffset = (b * seq + s) * _embeddingDim;
                                    
                                    // Accumulate gradients to chunked storage
                                    AccumulateGradients(output.Grad.AsSpan(srcOffset, _embeddingDim), dstIndex, gradBuffer);
                                }
                            }
                        }
                    });
                }
            }
            else
            {
                // Dense embedding lookup (original implementation)
                // Parallelize over batch when beneficial
                if (batch >= 4)
                {
                    Parallel.For(0, batch, b =>
                    {
                        for (int s = 0; s < seq; s++)
                        {
                            int idx = (int)input.Data[b * seq + s];
                            if (idx >= 0 && idx < _numEmbeddings)
                            {
                                int srcOffset = idx * _embeddingDim;
                                int dstOffset = (b * seq + s) * _embeddingDim;
                                
                                // Use Array.Copy for bulk memory transfer (much faster)
                                Array.Copy(
                                    Weight.Data, srcOffset,
                                    output.Data, dstOffset,
                                    _embeddingDim
                                );
                            }
                        }
                    });
                }
                else
                {
                    for (int b = 0; b < batch; b++)
                    {
                        for (int s = 0; s < seq; s++)
                        {
                            int idx = (int)input.Data[b * seq + s];
                            if (idx >= 0 && idx < _numEmbeddings)
                            {
                                int srcOffset = idx * _embeddingDim;
                                int dstOffset = (b * seq + s) * _embeddingDim;
                                
                                // Use Array.Copy for bulk memory transfer (much faster)
                                Array.Copy(
                                    Weight.Data, srcOffset,
                                    output.Data, dstOffset,
                                    _embeddingDim
                                );
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
                                    int srcOffset = (b * seq + s) * _embeddingDim;
                                    int dstOffset = idx * _embeddingDim;
                                    
                                    // Use Array.Copy for backward pass as well
                                    for (int j = 0; j < _embeddingDim; j++)
                                    {
                                        Weight.Grad[dstOffset + j] += output.Grad[srcOffset + j];
                                    }
                                }
                            }
                        }
                    });
                }
            }
            
            return output;
        }

        /// <summary>
        /// Accumulate gradients to chunked buffer. Handles chunk boundaries.
        /// </summary>
        private void AccumulateGradients(ReadOnlySpan<float> gradients, long baseIndex, ChunkedBuffer gradBuffer)
        {
            var (startChunkIdx, startOffset) = gradBuffer.GetChunkOffset(baseIndex);
            
            int remaining = gradients.Length;
            int srcOffset = 0;
            int chunkIdx = startChunkIdx;
            int chunkOffset = startOffset;
            
            while (remaining > 0)
            {
                var chunkSpan = gradBuffer.GetChunkSpan(chunkIdx);
                int chunkRemaining = chunkSpan.Length - chunkOffset;
                int toCopy = Math.Min(remaining, chunkRemaining);
                
                // Accumulate (not copy) gradients
                for (int i = 0; i < toCopy; i++)
                {
                    chunkSpan[chunkOffset + i] += gradients[srcOffset + i];
                }
                
                srcOffset += toCopy;
                remaining -= toCopy;
                chunkIdx++;
                chunkOffset = 0; // After first chunk, start from beginning
            }
        }
    }

    /// <summary>
    /// Layer Normalization
    /// </summary>
    public sealed class LayerNorm : Module
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
            return Forward(input, dest: null);
        }
        
        /// <summary>
        /// Forward pass with optional destination tensor to avoid allocation.
        /// If dest is null, allocates a new tensor. If dest is provided, writes result there.
        /// </summary>
        public Tensor Forward(Tensor input, Tensor? dest)
        {
            // Use fused LayerNorm operations - no intermediate allocations
            Tensor output = dest ?? new Tensor(input.Shape, requiresGrad: IsTraining);
            
            if (input.Shape.Length == 2)
            {
                int batch = input.Shape[0];
                int features = input.Shape[1];
                int expectedSize = batch * features;
                
                // Fused two-pass LayerNorm
                // Use AsSpan to handle pooled tensors with oversized backing arrays
                SmallMind.Core.Core.LayerNormOps.LayerNorm(
                    input.Data.AsSpan(0, expectedSize),
                    Gamma.Data,
                    Beta.Data,
                    output.Data.AsSpan(0, expectedSize),
                    batch,
                    features,
                    _eps);
            }
            else if (input.Shape.Length == 3)
            {
                int batch = input.Shape[0];
                int seq = input.Shape[1];
                int features = input.Shape[2];
                int expectedSize = batch * seq * features;
                
                // Fused LayerNorm for 3D tensors
                // Use AsSpan to handle pooled tensors with oversized backing arrays
                SmallMind.Core.Core.LayerNormOps.LayerNorm3D(
                    input.Data.AsSpan(0, expectedSize),
                    Gamma.Data,
                    Beta.Data,
                    output.Data.AsSpan(0, expectedSize),
                    batch,
                    seq,
                    features,
                    _eps);
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
    }

    /// <summary>
    /// RMSNorm (Root Mean Square Normalization) layer.
    /// Used in modern architectures like Llama, Mistral, Gemma.
    /// Simpler than LayerNorm: no mean subtraction, no beta parameter.
    /// Formula: y = (x / rms(x)) * gamma, where rms(x) = sqrt(mean(x^2) + eps)
    /// </summary>
    public sealed class RMSNorm : Module
    {
        private int _normalizedShape;
        private float _eps;
        public Tensor Gamma { get; private set; }

        public RMSNorm(int normalizedShape, float eps = 1e-5f)
        {
            _normalizedShape = normalizedShape;
            _eps = eps;
            
            Gamma = Tensor.Ones(new int[] { normalizedShape }, requiresGrad: true);
            
            Parameters.Add(Gamma);
        }

        public override Tensor Forward(Tensor input)
        {
            return Forward(input, dest: null);
        }
        
        /// <summary>
        /// Forward pass with optional destination tensor to avoid allocation.
        /// If dest is null, allocates a new tensor. If dest is provided, writes result there.
        /// </summary>
        public Tensor Forward(Tensor input, Tensor? dest)
        {
            // Use fused RMSNorm operations - no intermediate allocations
            Tensor output = dest ?? new Tensor(input.Shape, requiresGrad: IsTraining);
            
            if (input.Shape.Length == 2)
            {
                int batch = input.Shape[0];
                int features = input.Shape[1];
                int expectedSize = batch * features;
                
                // Fused two-pass RMSNorm
                // Use AsSpan to handle pooled tensors with oversized backing arrays
                SmallMind.Core.Core.RMSNormOps.RMSNorm(
                    input.Data.AsSpan(0, expectedSize),
                    Gamma.Data,
                    output.Data.AsSpan(0, expectedSize),
                    batch,
                    features,
                    _eps);
            }
            else if (input.Shape.Length == 3)
            {
                int batch = input.Shape[0];
                int seq = input.Shape[1];
                int features = input.Shape[2];
                int expectedSize = batch * seq * features;
                
                // Fused RMSNorm for 3D tensors
                // Use AsSpan to handle pooled tensors with oversized backing arrays
                SmallMind.Core.Core.RMSNormOps.RMSNorm3D(
                    input.Data.AsSpan(0, expectedSize),
                    Gamma.Data,
                    output.Data.AsSpan(0, expectedSize),
                    batch,
                    seq,
                    features,
                    _eps);
            }
            
            // Backward: simplified gradient pass
            // NOTE: This is a simplified implementation for educational purposes and initial deployment.
            // A complete RMSNorm backward would compute gradients with respect to the RMS.
            // For inference-only use cases (which is the primary target), this is acceptable.
            // For training modern architectures, consider implementing the full backward pass:
            // dL/dx_i = (1/rms) * (dL/dy_i * gamma_i) - (x_i / (rms^3 * n)) * sum_j(dL/dy_j * gamma_j * x_j)
            if (input.RequiresGrad || Gamma.RequiresGrad)
            {
                output.SetBackward(() =>
                {
                    // Simplified: pass through gradients (not fully correct but works for basic training)
                    if (input.RequiresGrad)
                    {
                        int expectedSize = input.Size;
                        var inputGradSpan = input.Grad.AsSpan(0, expectedSize);
                        var outputGradSpan = output.Grad.AsSpan(0, expectedSize);
                        
                        for (int i = 0; i < expectedSize; i++)
                        {
                            inputGradSpan[i] += outputGradSpan[i];
                        }
                    }
                    
                    // Accumulate gamma gradients
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
                                    Gamma.Grad[f] += output.Grad[offset + f]; // Simplified
                                }
                            }
                        }
                    }
                });
            }
            
            return output;
        }
    }

    /// <summary>
    /// Dropout layer (for regularization during training)
    /// </summary>
    public sealed class Dropout : Module
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
                // During inference (eval), dropout is a no-op, so we return the input tensor directly.
                // This eliminates a major allocation hotspot (Clone() created new float[] every call).
                // 
                // OWNERSHIP NOTE: Callers must not mutate the returned tensor when in eval mode.
                // This is safe because inference flows are read-only; training flows create new tensors.
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
                output.Data[i] = MathF.Max(0, input.Data[i]);
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

        /// <summary>
        /// SiLU (Sigmoid Linear Unit) activation, also known as Swish.
        /// Formula: SiLU(x) = x * sigmoid(x) = x / (1 + exp(-x))
        /// Used in modern architectures like Llama for gated MLPs (SwiGLU).
        /// </summary>
        public static Tensor SiLU(Tensor input)
        {
            return SiLU(input, dest: null);
        }

        /// <summary>
        /// SiLU activation with optional destination tensor to avoid allocation.
        /// If dest is null, allocates a new tensor. If dest is provided, writes result there.
        /// </summary>
        public static Tensor SiLU(Tensor input, Tensor? dest)
        {
            var output = dest ?? new Tensor(input.Shape, requiresGrad: input.RequiresGrad);
            
            // SiLU(x) = x * sigmoid(x) = x / (1 + exp(-x))
            for (int i = 0; i < input.Size; i++)
            {
                float x = input.Data[i];
                float sigmoid = 1.0f / (1.0f + MathF.Exp(-x));
                output.Data[i] = x * sigmoid;
            }
            
            if (input.RequiresGrad)
            {
                output.SetBackward(() =>
                {
                    // Derivative: SiLU'(x) = sigmoid(x) + x * sigmoid(x) * (1 - sigmoid(x))
                    //                      = sigmoid(x) * (1 + x * (1 - sigmoid(x)))
                    for (int i = 0; i < input.Size; i++)
                    {
                        float x = input.Data[i];
                        float sigmoid = 1.0f / (1.0f + MathF.Exp(-x));
                        float derivative = sigmoid * (1.0f + x * (1.0f - sigmoid));
                        input.Grad[i] += output.Grad[i] * derivative;
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
            
            // Use optimized fast GELU approximation from ActivationOps
            // Based on the sigmoid approximation: GELU(x) ≈ x * σ(1.702 * x)
            ActivationOps.GELU(input.Data, output.Data);
            
            if (input.RequiresGrad)
            {
                output.SetBackward(() =>
                {
                    // Use optimized GELU backward pass from ActivationOps
                    ActivationOps.GELUBackward(input.Data, output.Grad, input.Grad);
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
