using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using SmallMind.Validation;

namespace SmallMind.Core
{
    /// <summary>
    /// A simple tensor class with automatic differentiation support.
    /// Supports basic operations needed for neural networks.
    /// </summary>
    public class Tensor
    {
        public float[] Data { get; private set; }
        public int[] Shape { get; private set; }
        public float[]? Grad { get; set; }
        public bool RequiresGrad { get; set; }
        
        private Action? _backward;

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
            
            int size = 1;
            for (int i = 0; i < shape.Length; i++)
            {
                Guard.GreaterThan(shape[i], 0);
                size *= shape[i];
            }
            return size;
        }

        public int Size => Data.Length;

        /// <summary>
        /// Initialize with random normal distribution (Xavier initialization)
        /// </summary>
        public void InitializeXavier(Random random, int fanIn, int fanOut)
        {
            float std = MathF.Sqrt(2.0f / (fanIn + fanOut));
            for (int i = 0; i < Data.Length; i++)
            {
                Data[i] = (float)(random.NextDouble() * 2 - 1) * std * MathF.Sqrt(3);
            }
        }

        /// <summary>
        /// Initialize with small random values
        /// </summary>
        public void InitializeRandom(Random random, float scale = 0.02f)
        {
            for (int i = 0; i < Data.Length; i++)
            {
                Data[i] = (float)(random.NextDouble() * 2 - 1) * scale;
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
        /// Optimized with parallel processing for better performance
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

            // Use optimized cache-friendly SIMD matmul
            MatMulOptimized(a.Data, b.Data, result.Data, M, K, N);

            // Setup backward pass
            if (requiresGrad && (a.RequiresGrad || b.RequiresGrad))
            {
                result.SetBackward(() =>
                {
                    if (a.RequiresGrad)
                    {
                        // grad_a = grad_output @ b^T
                        // For parallel processing, each row is independent, so no locking needed
                        if (M >= 32)
                        {
                            Parallel.For(0, M, i =>
                            {
                                for (int k = 0; k < K; k++)
                                {
                                    float sum = 0;
                                    for (int j = 0; j < N; j++)
                                    {
                                        sum += result.Grad[i * N + j] * b.Data[k * N + j];
                                    }
                                    // Each thread writes to its own row (i), so no race condition
                                    a.Grad[i * K + k] += sum;
                                }
                            });
                        }
                        else
                        {
                            for (int i = 0; i < M; i++)
                            {
                                for (int k = 0; k < K; k++)
                                {
                                    float sum = 0;
                                    for (int j = 0; j < N; j++)
                                    {
                                        sum += result.Grad[i * N + j] * b.Data[k * N + j];
                                    }
                                    a.Grad[i * K + k] += sum;
                                }
                            }
                        }
                    }
                    if (b.RequiresGrad)
                    {
                        // grad_b = a^T @ grad_output
                        // Note: b.Grad is shared across threads, but we parallelize over k dimension
                        // Each thread accumulates to different elements, so no race condition
                        for (int k = 0; k < K; k++)
                        {
                            for (int j = 0; j < N; j++)
                            {
                                float sum = 0;
                                for (int i = 0; i < M; i++)
                                {
                                    sum += a.Data[i * K + k] * result.Grad[i * N + j];
                                }
                                b.Grad[k * N + j] += sum;
                            }
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

        /// <summary>
        /// Cache-friendly SIMD-optimized matrix multiplication using ikj loop order.
        /// This provides better cache locality than the naive ijk order.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void MatMulOptimized(float[] A, float[] B, float[] C, int M, int K, int N)
        {
            int vectorSize = Vector<float>.Count;
            
            // Parallelize when M >= 32 to amortize overhead
            if (M >= 32)
            {
                Parallel.For(0, M, i =>
                {
                    int aRowStart = i * K;
                    int cRowStart = i * N;
                    
                    // ikj loop order for better cache locality
                    for (int k = 0; k < K; k++)
                    {
                        float aik = A[aRowStart + k];
                        var vAik = new Vector<float>(aik);
                        int bRowStart = k * N;
                        
                        // SIMD vectorized inner loop
                        int j = 0;
                        for (; j <= N - vectorSize; j += vectorSize)
                        {
                            var vb = new Vector<float>(B, bRowStart + j);
                            var vc = new Vector<float>(C, cRowStart + j);
                            (vc + vAik * vb).CopyTo(C, cRowStart + j);
                        }
                        
                        // Scalar remainder
                        for (; j < N; j++)
                        {
                            C[cRowStart + j] += aik * B[bRowStart + j];
                        }
                    }
                });
            }
            else
            {
                // Sequential for small matrices using same optimized pattern
                for (int i = 0; i < M; i++)
                {
                    int aRowStart = i * K;
                    int cRowStart = i * N;
                    
                    for (int k = 0; k < K; k++)
                    {
                        float aik = A[aRowStart + k];
                        var vAik = new Vector<float>(aik);
                        int bRowStart = k * N;
                        
                        int j = 0;
                        for (; j <= N - vectorSize; j += vectorSize)
                        {
                            var vb = new Vector<float>(B, bRowStart + j);
                            var vc = new Vector<float>(C, cRowStart + j);
                            (vc + vAik * vb).CopyTo(C, cRowStart + j);
                        }
                        
                        for (; j < N; j++)
                        {
                            C[cRowStart + j] += aik * B[bRowStart + j];
                        }
                    }
                }
            }
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

        public override string ToString()
        {
            return $"Tensor(shape=[{string.Join(", ", Shape)}], size={Size})";
        }
    }
}
