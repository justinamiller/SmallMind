using System;
using System.Threading.Tasks;
using SmallMind.Core.Validation;
using SmallMind.Core.Simd;

namespace SmallMind.Core.Core
{
    /// <summary>
    /// A simple tensor class with automatic differentiation support.
    /// Supports basic operations needed for neural networks.
    /// Now supports chunked storage for tensors exceeding int.MaxValue elements.
    /// </summary>
    public class Tensor : IDisposable
    {
        public float[] Data { get; private set; }
        public int[] Shape { get; private set; }
        public float[]? Grad { get; set; }
        public bool RequiresGrad { get; set; }
        
        protected int? _logicalSize; // For pooled tensors with oversized backing arrays
        private Action? _backward;
        
        // Chunked storage support
        internal ITensorStorage? _storage; // When using chunked storage
        internal ITensorStorage? _gradStorage; // Chunked gradient storage
        private bool _disposed;

        public Tensor(float[] data, int[] shape, bool requiresGrad = false)
        {
            Guard.NotNull(data);
            Guard.NotNull(shape);
            Guard.NotNullOrEmpty(shape);
            
            int expectedSize = ShapeToSize(shape);
            if (data.Length != expectedSize)
            {
                throw new Exceptions.ValidationException(
                    $"Data length {data.Length} does not match shape size {expectedSize}",
                    nameof(data));
            }
            
            Data = data;
            Shape = shape;
            RequiresGrad = requiresGrad;
            if (requiresGrad)
            {
                Grad = new float[data.Length];
            }
        }
        
        /// <summary>
        /// Protected constructor for pooled tensors that may have larger backing arrays.
        /// Only the first 'size' elements of data will be used.
        /// </summary>
        protected Tensor(float[] data, int[] shape, int size, bool requiresGrad = false)
        {
            Guard.NotNull(data);
            Guard.NotNull(shape);
            Guard.NotNullOrEmpty(shape);
            
            int expectedSize = ShapeToSize(shape);
            Guard.GreaterThanOrEqualTo(size, expectedSize);
            Guard.GreaterThanOrEqualTo(data.Length, size);
            
            Data = data;
            Shape = shape;
            _logicalSize = expectedSize; // Track logical size separately
            RequiresGrad = requiresGrad;
            if (requiresGrad)
            {
                Grad = new float[expectedSize];
            }
        }

        public Tensor(int[] shape, bool requiresGrad = false)
        {
            Guard.NotNull(shape);
            Guard.NotNullOrEmpty(shape);
            
            Data = new float[ShapeToSize(shape)];
            Shape = shape;
            RequiresGrad = requiresGrad;
            if (requiresGrad)
            {
                Grad = new float[Data.Length];
            }
        }

        public static int ShapeToSize(int[] shape)
        {
            Guard.NotNull(shape);
            
            long size = ShapeToSizeLong(shape);
            
            // Check for overflow - critical for billion-parameter models
            if (size > int.MaxValue)
            {
                throw new Exceptions.ValidationException(
                    $"Tensor size overflow: shape {string.Join("x", shape)} = {size:N0} exceeds int.MaxValue ({int.MaxValue:N0}). " +
                    $"Use Tensor.CreateChunked() for tensors larger than {int.MaxValue:N0} elements.",
                    nameof(shape));
            }
            return (int)size;
        }

        public int Size => _logicalSize ?? Data.Length;

        /// <summary>
        /// Gets whether this tensor uses chunked storage (for >int.MaxValue elements).
        /// </summary>
        public bool IsChunked => _storage != null && _storage.IsChunked;

        /// <summary>
        /// Gets the total number of elements, supporting long for chunked tensors.
        /// </summary>
        public long TotalElements => _storage?.Length ?? Data.Length;

        /// <summary>
        /// Creates a new chunked tensor for large tensors exceeding int.MaxValue.
        /// Automatically uses chunked storage when total elements exceed int.MaxValue.
        /// </summary>
        /// <param name="shape">Shape of the tensor.</param>
        /// <param name="requiresGrad">Whether to track gradients.</param>
        /// <param name="chunkSize">Size of each chunk in elements (default: 64M).</param>
        /// <returns>A new tensor with chunked storage.</returns>
        public static Tensor CreateChunked(int[] shape, bool requiresGrad = false, int chunkSize = ChunkedBuffer.DEFAULT_CHUNK_SIZE)
        {
            Guard.NotNull(shape);
            Guard.NotNullOrEmpty(shape);

            long totalElements = ShapeToSizeLong(shape);
            
            var tensor = new Tensor();
            tensor.Shape = (int[])shape.Clone();
            tensor.RequiresGrad = requiresGrad;
            tensor._storage = new ChunkedStorage(totalElements, chunkSize);
            
            // For compatibility, Data points to an empty array (most code won't use it for chunked tensors)
            // This prevents NullReferenceException in legacy code paths
            tensor.Data = Array.Empty<float>();
            
            if (requiresGrad)
            {
                tensor._gradStorage = new ChunkedStorage(totalElements, chunkSize);
                tensor.Grad = Array.Empty<float>(); // For compatibility
            }
            
            return tensor;
        }

        /// <summary>
        /// Creates a memory-mapped tensor that streams data from disk.
        /// Ideal for very large models that don't fit in RAM (inference only).
        /// </summary>
        /// <param name="filePath">Path to the file containing tensor data.</param>
        /// <param name="shape">Shape of the tensor.</param>
        /// <param name="writable">Whether to allow write access (default: false).</param>
        /// <returns>A new tensor with memory-mapped storage.</returns>
        public static Tensor CreateMemoryMapped(string filePath, int[] shape, bool writable = false)
        {
            Guard.NotNullOrWhiteSpace(filePath);
            Guard.NotNull(shape);
            Guard.NotNullOrEmpty(shape);

            long totalElements = ShapeToSizeLong(shape);
            
            var tensor = new Tensor();
            tensor.Shape = (int[])shape.Clone();
            tensor.RequiresGrad = false; // Memory-mapped tensors don't support gradients
            tensor._storage = new MemoryMappedTensorStorage(filePath, totalElements, writable);
            tensor.Data = Array.Empty<float>();
            
            return tensor;
        }

        /// <summary>
        /// Creates a new memory-mapped tensor file initialized with zeros.
        /// </summary>
        /// <param name="filePath">Path where the file will be created.</param>
        /// <param name="shape">Shape of the tensor.</param>
        /// <returns>A new tensor with memory-mapped storage.</returns>
        public static Tensor CreateMemoryMappedFile(string filePath, int[] shape)
        {
            Guard.NotNullOrWhiteSpace(filePath);
            Guard.NotNull(shape);
            Guard.NotNullOrEmpty(shape);

            long totalElements = ShapeToSizeLong(shape);
            
            var tensor = new Tensor();
            tensor.Shape = (int[])shape.Clone();
            tensor.RequiresGrad = false;
            tensor._storage = MemoryMappedTensorStorage.Create(filePath, totalElements);
            tensor.Data = Array.Empty<float>();
            
            return tensor;
        }

        /// <summary>
        /// Gets whether this tensor uses memory-mapped storage.
        /// </summary>
        public bool IsMemoryMapped => _storage is MemoryMappedTensorStorage;

        /// <summary>
        /// Protected parameterless constructor for CreateChunked factory method.
        /// </summary>
        protected Tensor()
        {
            Data = Array.Empty<float>();
            Shape = Array.Empty<int>();
        }

        /// <summary>
        /// Calculate tensor size as long to support >int.MaxValue.
        /// Returns long to detect overflow without throwing.
        /// </summary>
        public static long ShapeToSizeLong(int[] shape)
        {
            Guard.NotNull(shape);
            
            long size = 1;
            for (int i = 0; i < shape.Length; i++)
            {
                Guard.GreaterThan(shape[i], 0);
                size *= shape[i];
            }
            return size;
        }

        /// <summary>
        /// Initialize with random normal distribution (Xavier initialization)
        /// </summary>
        public void InitializeXavier(Random random, int fanIn, int fanOut)
        {
            float std = MathF.Sqrt(2.0f / (fanIn + fanOut));
            
            if (_storage != null)
            {
                // Chunked storage - initialize per chunk
                var buffer = _storage.GetChunkedBuffer();
                for (int chunkIdx = 0; chunkIdx < buffer.ChunkCount; chunkIdx++)
                {
                    var chunk = buffer.GetChunkSpan(chunkIdx);
                    for (int i = 0; i < chunk.Length; i++)
                    {
                        chunk[i] = (float)(random.NextDouble() * 2 - 1) * std * MathF.Sqrt(3);
                    }
                }
            }
            else
            {
                // Dense storage
                for (int i = 0; i < Data.Length; i++)
                {
                    Data[i] = (float)(random.NextDouble() * 2 - 1) * std * MathF.Sqrt(3);
                }
            }
        }

        /// <summary>
        /// Initialize with small random values
        /// </summary>
        public void InitializeRandom(Random random, float scale = 0.02f)
        {
            if (_storage != null)
            {
                // Chunked storage - initialize per chunk
                var buffer = _storage.GetChunkedBuffer();
                for (int chunkIdx = 0; chunkIdx < buffer.ChunkCount; chunkIdx++)
                {
                    var chunk = buffer.GetChunkSpan(chunkIdx);
                    for (int i = 0; i < chunk.Length; i++)
                    {
                        chunk[i] = (float)(random.NextDouble() * 2 - 1) * scale;
                    }
                }
            }
            else
            {
                // Dense storage
                for (int i = 0; i < Data.Length; i++)
                {
                    Data[i] = (float)(random.NextDouble() * 2 - 1) * scale;
                }
            }
        }

        /// <summary>
        /// Zero gradients
        /// </summary>
        public void ZeroGrad()
        {
            if (Grad != null)
            {
                Array.Clear(Grad, 0, Grad.Length);
            }
        }

        /// <summary>
        /// Backward pass - accumulate gradients
        /// </summary>
        public void Backward(float[]? grad = null)
        {
            if (!RequiresGrad) return;

            if (grad == null)
            {
                // Root of computation graph
                if (Grad == null)
                {
                    Grad = new float[Data.Length];
                }
                Span<float> gradSpan = Grad;
                for (int i = 0; i < Grad.Length; i++)
                {
                    gradSpan[i] = 1.0f;
                }
            }
            else
            {
                // Accumulate gradient (use Span for better performance)
                if (Grad == null) Grad = new float[Data.Length];
                Span<float> gradSpan = Grad;
                ReadOnlySpan<float> inputGradSpan = grad;
                int length = Math.Min(grad.Length, Grad.Length);
                for (int i = 0; i < length; i++)
                {
                    gradSpan[i] += inputGradSpan[i];
                }
            }

            _backward?.Invoke();
        }

        public void SetBackward(Action backward)
        {
            _backward = backward;
        }

        /// <summary>
        /// Matrix multiplication: (M, K) @ (K, N) = (M, N)
        /// Optimized with SIMD operations and parallel processing
        /// </summary>
        public static Tensor MatMul(Tensor a, Tensor b, bool requiresGrad = false)
        {
            if (a.Shape.Length != 2 || b.Shape.Length != 2)
                throw new ArgumentException("MatMul requires 2D tensors");
            if (a.Shape[1] != b.Shape[0])
                throw new ArgumentException($"Incompatible shapes for matmul: ({a.Shape[0]}, {a.Shape[1]}) and ({b.Shape[0]}, {b.Shape[1]})");

            int M = a.Shape[0];
            int K = a.Shape[1];
            int N = b.Shape[1];

            var result = new Tensor(new int[] { M, N }, requiresGrad);

            // Use SIMD-optimized matrix multiplication from SmallMind.Core.Simd
            // This provides 5-10x speedup over naive implementation
            MatMulOps.MatMul(a.Data, b.Data, result.Data, M, K, N);

            // Setup backward pass
            if (requiresGrad && (a.RequiresGrad || b.RequiresGrad))
            {
                result.SetBackward(() =>
                {
                    if (a.RequiresGrad)
                    {
                        // grad_a = grad_output @ b^T
                        // Use optimized MatMulTransposeB from MatrixOps
                        float[] tempGradA = new float[M * K];
                        MatrixOps.MatMulTransposeB(result.Grad, b.Data, tempGradA, M, N, K);
                        
                        // Accumulate gradients
                        for (int i = 0; i < M * K; i++)
                        {
                            a.Grad[i] += tempGradA[i];
                        }
                    }
                    if (b.RequiresGrad)
                    {
                        // grad_b = a^T @ grad_output
                        // Use optimized MatMulTransposeA from MatrixOps
                        float[] tempGradB = new float[K * N];
                        MatrixOps.MatMulTransposeA(a.Data, result.Grad, tempGradB, K, M, N);
                        
                        // Accumulate gradients
                        for (int i = 0; i < K * N; i++)
                        {
                            b.Grad[i] += tempGradB[i];
                        }
                    }
                });
            }

            return result;
        }

        /// <summary>
        /// Element-wise addition
        /// </summary>
        public static Tensor Add(Tensor a, Tensor b, bool requiresGrad = false)
        {
            if (!ShapesEqual(a.Shape, b.Shape) && !IsBroadcastable(a.Shape, b.Shape))
                throw new ArgumentException("Shapes must be equal or broadcastable for addition");

            var result = new Tensor(a.Shape, requiresGrad);

            // Simple case: same shape - use Span<T> for better performance
            if (ShapesEqual(a.Shape, b.Shape))
            {
                Span<float> resultSpan = result.Data;
                ReadOnlySpan<float> aSpan = a.Data;
                ReadOnlySpan<float> bSpan = b.Data;
                
                for (int i = 0; i < a.Size; i++)
                {
                    resultSpan[i] = aSpan[i] + bSpan[i];
                }

                if (requiresGrad && (a.RequiresGrad || b.RequiresGrad))
                {
                    result.SetBackward(() =>
                    {
                        if (a.RequiresGrad)
                        {
                            Span<float> aGradSpan = a.Grad;
                            ReadOnlySpan<float> resultGradSpan = result.Grad;
                            for (int i = 0; i < a.Size; i++)
                                aGradSpan[i] += resultGradSpan[i];
                        }
                        if (b.RequiresGrad)
                        {
                            Span<float> bGradSpan = b.Grad;
                            ReadOnlySpan<float> resultGradSpan = result.Grad;
                            for (int i = 0; i < b.Size; i++)
                                bGradSpan[i] += resultGradSpan[i];
                        }
                    });
                }
            }
            else
            {
                // Broadcasting case (simplified for common patterns)
                BroadcastAdd(a, b, result);
                
                if (requiresGrad && (a.RequiresGrad || b.RequiresGrad))
                {
                    result.SetBackward(() =>
                    {
                        BroadcastAddBackward(a, b, result);
                    });
                }
            }

            return result;
        }

        private static void BroadcastAdd(Tensor a, Tensor b, Tensor result)
        {
            // Handle common broadcasting patterns
            if (b.Shape.Length == 1 && a.Shape.Length == 2 && b.Shape[0] == a.Shape[1])
            {
                // (M, N) + (N,) broadcasting
                int M = a.Shape[0];
                int N = a.Shape[1];
                for (int i = 0; i < M; i++)
                {
                    for (int j = 0; j < N; j++)
                    {
                        result.Data[i * N + j] = a.Data[i * N + j] + b.Data[j];
                    }
                }
            }
            else
            {
                // Fallback: element-wise if same size
                for (int i = 0; i < Math.Min(a.Size, b.Size); i++)
                {
                    result.Data[i] = a.Data[i] + b.Data[i % b.Size];
                }
            }
        }

        private static void BroadcastAddBackward(Tensor a, Tensor b, Tensor result)
        {
            if (a.RequiresGrad)
            {
                for (int i = 0; i < a.Size; i++)
                    a.Grad[i] += result.Grad[i];
            }
            if (b.RequiresGrad)
            {
                if (b.Shape.Length == 1 && result.Shape.Length == 2)
                {
                    int M = result.Shape[0];
                    int N = result.Shape[1];
                    for (int i = 0; i < M; i++)
                    {
                        for (int j = 0; j < N; j++)
                        {
                            b.Grad[j] += result.Grad[i * N + j];
                        }
                    }
                }
                else
                {
                    for (int i = 0; i < b.Size; i++)
                        b.Grad[i] += result.Grad[i];
                }
            }
        }

        private static bool ShapesEqual(int[] a, int[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i]) return false;
            return true;
        }

        private static bool IsBroadcastable(int[] a, int[] b)
        {
            // Simplified broadcasting check
            return true; // We'll handle specific cases
        }

        /// <summary>
        /// Softmax over last dimension
        /// </summary>
        public Tensor Softmax(int dim = -1)
        {
            var result = new Tensor(Shape, RequiresGrad);
            
            if (Shape.Length == 2 && dim == -1)
            {
                int rows = Shape[0];
                int cols = Shape[1];
                
                ReadOnlySpan<float> dataSpan = Data;
                Span<float> resultSpan = result.Data;
                
                for (int i = 0; i < rows; i++)
                {
                    int offset = i * cols;
                    
                    // Find max for numerical stability
                    float max = float.NegativeInfinity;
                    for (int j = 0; j < cols; j++)
                    {
                        max = Math.Max(max, dataSpan[offset + j]);
                    }
                    
                    // Exp and sum
                    float sum = 0;
                    for (int j = 0; j < cols; j++)
                    {
                        float exp = MathF.Exp(dataSpan[offset + j] - max);
                        resultSpan[offset + j] = exp;
                        sum += exp;
                    }
                    
                    // Normalize
                    for (int j = 0; j < cols; j++)
                    {
                        resultSpan[offset + j] /= sum;
                    }
                }
            }
            
            // Softmax backward is complex, simplified here
            if (RequiresGrad)
            {
                result.SetBackward(() =>
                {
                    // Simplified: just pass gradient through
                    Span<float> gradSpan = Grad;
                    ReadOnlySpan<float> resultGradSpan = result.Grad;
                    for (int i = 0; i < Size; i++)
                    {
                        gradSpan[i] += resultGradSpan[i];
                    }
                });
            }
            
            return result;
        }

        /// <summary>
        /// Create a copy of this tensor
        /// </summary>
        public Tensor Clone()
        {
            var result = new Tensor((float[])Data.Clone(), (int[])Shape.Clone(), RequiresGrad);
            return result;
        }

        /// <summary>
        /// Reshape tensor
        /// </summary>
        public Tensor Reshape(int[] newShape)
        {
            if (ShapeToSize(newShape) != Size)
                throw new ArgumentException("New shape must have same total size");
            
            var result = new Tensor((float[])Data.Clone(), newShape, RequiresGrad);
            if (RequiresGrad)
            {
                result.SetBackward(() =>
                {
                    Span<float> gradSpan = Grad;
                    ReadOnlySpan<float> resultGradSpan = result.Grad;
                    for (int i = 0; i < Size; i++)
                        gradSpan[i] += resultGradSpan[i];
                });
            }
            return result;
        }

        /// <summary>
        /// Transpose a 2D tensor
        /// </summary>
        public Tensor Transpose()
        {
            if (Shape.Length != 2)
                throw new ArgumentException("Transpose only works on 2D tensors");
            
            int rows = Shape[0];
            int cols = Shape[1];
            var result = new Tensor(new int[] { cols, rows }, RequiresGrad);
            
            ReadOnlySpan<float> dataSpan = Data;
            Span<float> resultSpan = result.Data;
            
            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    resultSpan[j * rows + i] = dataSpan[i * cols + j];
                }
            }
            
            if (RequiresGrad)
            {
                result.SetBackward(() =>
                {
                    for (int i = 0; i < rows; i++)
                    {
                        for (int j = 0; j < cols; j++)
                        {
                            Grad[i * cols + j] += result.Grad[j * rows + i];
                        }
                    }
                });
            }
            
            return result;
        }

        public static Tensor Zeros(int[] shape, bool requiresGrad = false)
        {
            return new Tensor(shape, requiresGrad);
        }

        public static Tensor Ones(int[] shape, bool requiresGrad = false)
        {
            var t = new Tensor(shape, requiresGrad);
            for (int i = 0; i < t.Size; i++)
                t.Data[i] = 1.0f;
            return t;
        }

        /// <summary>
        /// Create a pooled tensor for temporary/scratch use. Must be returned via Dispose or TensorScope.
        /// DO NOT use for model weights or long-lived tensors.
        /// </summary>
        public static PooledTensor CreatePooled(int[] shape, bool requiresGrad = false)
        {
            Guard.NotNull(shape);
            Guard.NotNullOrEmpty(shape);
            
            int size = ShapeToSize(shape);
            var data = TensorPool.Shared.Rent(size, out int capacity);
            
            float[]? grad = null;
            bool pooledGrad = false;
            if (requiresGrad)
            {
                grad = TensorPool.Shared.Rent(size, out _);
                Array.Clear(grad, 0, size); // Zero out gradient
                pooledGrad = true;
            }
            
            var tensor = new PooledTensor(data, shape, capacity, requiresGrad, pooledGrad);
            if (grad != null)
            {
                tensor.Grad = grad;
            }
            
            return tensor;
        }

        // In-place operations
        
        /// <summary>
        /// Add another tensor to this tensor in-place. Shapes must match.
        /// </summary>
        public void AddInPlace(Tensor other)
        {
            Guard.NotNull(other);
            if (!ShapesEqual(Shape, other.Shape))
                throw new ArgumentException("Shapes must match for AddInPlace");
            
            Span<float> dataSpan = Data;
            ReadOnlySpan<float> otherSpan = other.Data;
            for (int i = 0; i < Size; i++)
            {
                dataSpan[i] += otherSpan[i];
            }
        }
        
        /// <summary>
        /// Multiply this tensor by another tensor in-place. Shapes must match.
        /// </summary>
        public void MulInPlace(Tensor other)
        {
            Guard.NotNull(other);
            if (!ShapesEqual(Shape, other.Shape))
                throw new ArgumentException("Shapes must match for MulInPlace");
            
            Span<float> dataSpan = Data;
            ReadOnlySpan<float> otherSpan = other.Data;
            for (int i = 0; i < Size; i++)
            {
                dataSpan[i] *= otherSpan[i];
            }
        }
        
        /// <summary>
        /// Scale this tensor by a scalar in-place.
        /// </summary>
        public void ScaleInPlace(float scalar)
        {
            Span<float> dataSpan = Data;
            for (int i = 0; i < Size; i++)
            {
                dataSpan[i] *= scalar;
            }
        }
        
        /// <summary>
        /// Add a scaled tensor to this tensor in-place: this += scale * other
        /// </summary>
        public void AddScaledInPlace(Tensor other, float scale)
        {
            Guard.NotNull(other);
            if (!ShapesEqual(Shape, other.Shape))
                throw new ArgumentException("Shapes must match for AddScaledInPlace");
            
            Span<float> dataSpan = Data;
            ReadOnlySpan<float> otherSpan = other.Data;
            for (int i = 0; i < Size; i++)
            {
                dataSpan[i] += scale * otherSpan[i];
            }
        }
        
        /// <summary>
        /// Copy data from another tensor to this tensor.
        /// </summary>
        public void CopyFrom(Tensor source)
        {
            Guard.NotNull(source);
            if (!ShapesEqual(Shape, source.Shape))
                throw new ArgumentException("Shapes must match for CopyFrom");
            
            source.Data.CopyTo(Data, 0);
        }
        
        // Functional overloads with optional destination parameter
        
        /// <summary>
        /// Element-wise addition with optional destination tensor.
        /// If dest is provided, result is written there (must have matching shape).
        /// Otherwise, allocates a new tensor.
        /// </summary>
        public static Tensor Add(Tensor a, Tensor b, Tensor? dest = null, bool requiresGrad = false)
        {
            if (dest != null)
            {
                // Validate dest shape matches
                if (!ShapesEqual(a.Shape, dest.Shape))
                    throw new ArgumentException("Destination shape must match source shape");
                
                // Write result to dest
                if (ShapesEqual(a.Shape, b.Shape))
                {
                    Span<float> destSpan = dest.Data;
                    ReadOnlySpan<float> aSpan = a.Data;
                    ReadOnlySpan<float> bSpan = b.Data;
                    
                    for (int i = 0; i < a.Size; i++)
                    {
                        destSpan[i] = aSpan[i] + bSpan[i];
                    }
                }
                else
                {
                    // Broadcasting case
                    BroadcastAdd(a, b, dest);
                }
                
                // Setup backward if needed
                if (requiresGrad && (a.RequiresGrad || b.RequiresGrad))
                {
                    dest.SetBackward(() => BroadcastAddBackward(a, b, dest));
                }
                
                return dest;
            }
            else
            {
                // Original allocation path
                return Add(a, b, requiresGrad);
            }
        }

        /// <summary>
        /// Gets chunked buffer for direct access (for chunked tensors only).
        /// </summary>
        /// <returns>The underlying chunked buffer.</returns>
        /// <exception cref="InvalidOperationException">If tensor is not chunked.</exception>
        public ChunkedBuffer GetChunkedBuffer()
        {
            if (_storage == null || !_storage.IsChunked)
                throw new InvalidOperationException("Tensor does not use chunked storage.");
            return _storage.GetChunkedBuffer();
        }

        /// <summary>
        /// Gets chunked gradient buffer for direct access (for chunked tensors only).
        /// </summary>
        /// <returns>The underlying chunked gradient buffer.</returns>
        /// <exception cref="InvalidOperationException">If tensor is not chunked or doesn't have gradients.</exception>
        public ChunkedBuffer GetChunkedGradBuffer()
        {
            if (_gradStorage == null || !_gradStorage.IsChunked)
                throw new InvalidOperationException("Tensor does not use chunked gradient storage.");
            return _gradStorage.GetChunkedBuffer();
        }

        /// <summary>
        /// Copies data from chunked storage to a destination span.
        /// Works for both dense and chunked tensors.
        /// </summary>
        /// <param name="sourceIndex">Starting index in this tensor.</param>
        /// <param name="destination">Destination span.</param>
        /// <param name="length">Number of elements to copy.</param>
        public void CopyTo(long sourceIndex, Span<float> destination, int length)
        {
            if (_storage != null)
            {
                _storage.CopyTo(sourceIndex, destination, length);
            }
            else
            {
                Data.AsSpan((int)sourceIndex, length).CopyTo(destination);
            }
        }

        /// <summary>
        /// Copies data from a source span to this tensor.
        /// Works for both dense and chunked tensors.
        /// </summary>
        /// <param name="source">Source span.</param>
        /// <param name="destinationIndex">Starting index in this tensor.</param>
        public void CopyFrom(ReadOnlySpan<float> source, long destinationIndex)
        {
            if (_storage != null)
            {
                _storage.CopyFrom(source, destinationIndex);
            }
            else
            {
                source.CopyTo(Data.AsSpan((int)destinationIndex));
            }
        }

        public override string ToString()
        {
            if (IsMemoryMapped)
            {
                var mmStorage = (MemoryMappedTensorStorage)_storage!;
                return $"Tensor(shape=[{string.Join(", ", Shape)}], memory-mapped, file={Path.GetFileName(mmStorage.FilePath)}, totalElements={TotalElements:N0})";
            }
            if (IsChunked)
                return $"Tensor(shape=[{string.Join(", ", Shape)}], chunked, totalElements={TotalElements:N0})";
            return $"Tensor(shape=[{string.Join(", ", Shape)}], size={Size})";
        }

        /// <summary>
        /// Disposes resources used by this tensor (only for memory-mapped tensors).
        /// Regular dense and chunked tensors don't need disposal (GC handles them).
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            
            if (_storage is IDisposable disposableStorage)
            {
                disposableStorage.Dispose();
            }
            
            if (_gradStorage is IDisposable disposableGradStorage)
            {
                disposableGradStorage.Dispose();
            }
            
            _disposed = true;
        }
    }
    
    /// <summary>
    /// A pooled tensor that returns its backing array to TensorPool when disposed.
    /// Use for temporary/scratch tensors only. DO NOT use for model weights.
    /// The backing array may be larger than the logical size to leverage pooling.
    /// </summary>
    public sealed class PooledTensor : Tensor, IDisposable
    {
        private readonly int _capacity;
        private bool _disposed;
        private readonly bool _pooledGrad; // Track if gradient is also pooled
        
        internal PooledTensor(float[] data, int[] shape, int capacity, bool requiresGrad = false, bool pooledGrad = false)
            : base(data, shape, capacity, requiresGrad)
        {
            _capacity = capacity;
            _pooledGrad = pooledGrad;
        }
        
        public int Capacity => _capacity;
        
        public new void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            
            TensorPool.Shared.Return(Data, clearArray: true);
            
            // Return gradient to pool if it was also pooled
            if (_pooledGrad && Grad != null)
            {
                TensorPool.Shared.Return(Grad, clearArray: true);
            }
        }
    }
    
    /// <summary>
    /// Scope helper for automatically returning pooled tensors.
    /// Usage: using var scope = new TensorScope();
    ///        var temp = scope.Rent(shape);
    /// </summary>
    public sealed class TensorScope : IDisposable
    {
        private readonly System.Collections.Generic.List<PooledTensor> _tensors = new();
        
        public PooledTensor Rent(int[] shape, bool requiresGrad = false)
        {
            var tensor = Tensor.CreatePooled(shape, requiresGrad);
            _tensors.Add(tensor);
            return tensor;
        }
        
        public void Dispose()
        {
            foreach (var tensor in _tensors)
            {
                tensor.Dispose();
            }
            _tensors.Clear();
        }
    }
}
