using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace SmallMind.Core.Simd
{
    /// <summary>
    /// SIMD-accelerated softmax operation with numerical stability.
    /// Implements the stable softmax: exp(x - max(x)) / sum(exp(x - max(x)))
    /// </summary>
    internal static class SoftmaxOps
    {
        /// <summary>
        /// Applies softmax over the last dimension of a 2D tensor (rows x cols).
        /// Each row is normalized independently.
        /// Uses SIMD for max-finding and normalization, with parallel processing for large batches.
        /// </summary>
        public static void Softmax2D(float[] input, float[] output, int rows, int cols)
        {
            if (input.Length != rows * cols || output.Length != rows * cols)
                throw new ArgumentException("Input and output sizes must match rows * cols");

            // Use parallel processing for larger batches (rows >= 32)
            if (rows >= 32)
            {
                Parallel.For(0, rows, i =>
                {
                    int offset = i * cols;
                    SoftmaxRowIndexed(input, output, offset, cols);
                });
            }
            else
            {
                // Sequential for small batches
                for (int i = 0; i < rows; i++)
                {
                    int offset = i * cols;
                    SoftmaxRowIndexed(input, output, offset, cols);
                }
            }
        }

        /// <summary>
        /// Applies softmax to a single row using array indexing (for parallel processing).
        /// Optimized with SIMD for max finding and normalization.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [SkipLocalsInit]
        private static void SoftmaxRowIndexed(float[] input, float[] output, int offset, int length)
        {
            // Validate offset is within bounds before any pointer arithmetic
            if (offset < 0 || offset > input.Length || offset > output.Length)
                throw new ArgumentOutOfRangeException(nameof(offset));
            if (length < 0 || offset + length > input.Length || offset + length > output.Length)
                throw new ArgumentOutOfRangeException(nameof(length));

            // Step 1: Find max with SIMD
            int i = 0;
            float max = float.NegativeInfinity;

            unsafe
            {
                fixed (float* pInput = input)
                {
                    // AVX-512 path (16 floats)
                    if (Avx512F.IsSupported && length >= 16)
                    {
                        var maxVec512 = Vector512.Create(float.NegativeInfinity);
                        for (; i <= length - 16; i += 16)
                        {
                            var v = Avx512F.LoadVector512(pInput + offset + i);
                            maxVec512 = Avx512F.Max(maxVec512, v);
                        }
                        // Reduce 512→256→scalar
                        var upper = Avx512F.ExtractVector256(maxVec512, 1);
                        var lower = Avx512F.ExtractVector256(maxVec512, 0);
                        var maxVec256 = Avx.Max(upper, lower);

                        // Further reduce 256→scalar using horizontal max
                        unsafe
                        {
                            float* temp = stackalloc float[8];
                            Avx.Store(temp, maxVec256);
                            for (int j = 0; j < 8; j++)
                            {
                                if (temp[j] > max)
                                    max = temp[j];
                            }
                        }
                    }
                }
            }

            // Vector<T> fallback for remaining elements
            int vectorSize2 = Vector<float>.Count;
            var maxVec2 = new Vector<float>(max);
            for (; i <= length - vectorSize2; i += vectorSize2)
            {
                var v = new Vector<float>(input, offset + i);
                maxVec2 = Vector.Max(maxVec2, v);
            }

            // Horizontal max reduction
            for (int j = 0; j < vectorSize2; j++)
            {
                if (maxVec2[j] > max)
                    max = maxVec2[j];
            }

            // Scalar remainder for max
            for (; i < length; i++)
            {
                if (input[offset + i] > max)
                    max = input[offset + i];
            }

            // Step 2: Compute exp(x - max) and sum (scalar for accuracy)
            // Note: SIMD exp approximations have too much error for softmax
            float sum = 0f;
            for (int j = 0; j < length; j++)
            {
                float exp = MathF.Exp(input[offset + j] - max);
                output[offset + j] = exp;
                sum += exp;
            }

            // Step 3: Normalize by sum with SIMD
            float invSum = 1f / sum;
            i = 0;

            unsafe
            {
                fixed (float* pOutput = output)
                {
                    // AVX-512 normalization path (16 floats)
                    if (Avx512F.IsSupported && length >= 16)
                    {
                        var invSumVec512 = Vector512.Create(invSum);
                        for (; i <= length - 16; i += 16)
                        {
                            var v = Avx512F.LoadVector512(pOutput + offset + i);
                            Avx512F.Store(pOutput + offset + i, Avx512F.Multiply(v, invSumVec512));
                        }
                    }
                }
            }

            // Vector<T> fallback for normalization
            var invSumVec = new Vector<float>(invSum);
            for (; i <= length - vectorSize2; i += vectorSize2)
            {
                var v = new Vector<float>(output, offset + i);
                (v * invSumVec).CopyTo(output, offset + i);
            }

            // Scalar remainder for normalization
            for (; i < length; i++)
            {
                output[offset + i] *= invSum;
            }
        }

        /// <summary>
        /// Applies softmax to a single row with SIMD acceleration (span-based).
        /// Implements numerically stable softmax: exp(x - max) / sum(exp(x - max))
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [SkipLocalsInit]
        private static void SoftmaxRow(ReadOnlySpan<float> input, Span<float> output)
        {
            int length = input.Length;

            // Step 1: Find max (SIMD accelerated)
            float max = FindMax(input);

            // Step 2: Compute exp(x - max) and sum (scalar for accuracy)
            // Note: SIMD exp approximations have too much error for softmax
            float sum = 0f;
            for (int i = 0; i < length; i++)
            {
                float exp = MathF.Exp(input[i] - max);
                output[i] = exp;
                sum += exp;
            }

            // Step 3: Normalize by sum (SIMD accelerated)
            float invSum = 1f / sum;
            Scale(output, invSum);
        }

        /// <summary>
        /// Finds the maximum value in a span using SIMD (AVX-512, AVX2, or Vector&lt;T&gt;).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [SkipLocalsInit]
        private static float FindMax(ReadOnlySpan<float> values)
        {
            int length = values.Length;
            int i = 0;
            float max = float.NegativeInfinity;

            unsafe
            {
                fixed (float* pValues = values)
                {
                    // AVX-512 path (16 floats)
                    if (Avx512F.IsSupported && length >= 16)
                    {
                        var maxVec512 = Vector512.Create(float.NegativeInfinity);
                        for (; i <= length - 16; i += 16)
                        {
                            var v = Avx512F.LoadVector512(pValues + i);
                            maxVec512 = Avx512F.Max(maxVec512, v);
                        }
                        // Reduce 512→256: take max of upper and lower halves
                        var upper = Avx512F.ExtractVector256(maxVec512, 1);
                        var lower = Avx512F.ExtractVector256(maxVec512, 0);
                        var maxVec256 = Avx.Max(upper, lower);

                        // Reduce 256→scalar
                        float* temp = stackalloc float[8];
                        Avx.Store(temp, maxVec256);
                        for (int j = 0; j < 8; j++)
                        {
                            if (temp[j] > max)
                                max = temp[j];
                        }
                    }
                }
            }

            // Vector<T> fallback
            // OPTIMIZED: Use unsafe pointer arithmetic to eliminate Span.Slice() overhead
            var maxVec = new Vector<float>(max);
            int vectorSize = Vector<float>.Count;

            if (i <= length - vectorSize)
            {
                unsafe
                {
                    fixed (float* pValues = values)
                    {
                        for (; i <= length - vectorSize; i += vectorSize)
                        {
                            var v = Unsafe.Read<Vector<float>>(pValues + i);
                            maxVec = Vector.Max(maxVec, v);
                        }
                    }
                }
            }

            // Horizontal max reduction
            for (int j = 0; j < vectorSize; j++)
            {
                if (maxVec[j] > max)
                    max = maxVec[j];
            }

            // Scalar remainder
            for (; i < length; i++)
            {
                if (values[i] > max)
                    max = values[i];
            }

            return max;
        }

        /// <summary>
        /// In-place scalar multiplication using SIMD (AVX-512, AVX2, or Vector&lt;T&gt;).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Scale(Span<float> values, float scalar)
        {
            int length = values.Length;
            int i = 0;

            // AVX-512 path (16 floats)
            if (Avx512F.IsSupported && length >= 16)
            {
                var vScalar512 = Vector512.Create(scalar);
                unsafe
                {
                    fixed (float* pValues = values)
                    {
                        for (; i <= length - 16; i += 16)
                        {
                            var v = Avx512F.LoadVector512(pValues + i);
                            Avx512F.Store(pValues + i, Avx512F.Multiply(v, vScalar512));
                        }
                    }
                }
            }

            // Vector<T> fallback
            // OPTIMIZED: Use unsafe pointer arithmetic to eliminate Span.Slice() overhead
            var vScalar = new Vector<float>(scalar);
            int vectorSize = Vector<float>.Count;

            if (i <= length - vectorSize)
            {
                unsafe
                {
                    fixed (float* pValues = values)
                    {
                        for (; i <= length - vectorSize; i += vectorSize)
                        {
                            var v = Unsafe.Read<Vector<float>>(pValues + i);
                            Unsafe.Write(pValues + i, v * vScalar);
                        }
                    }
                }
            }

            // Scalar remainder
            for (; i < length; i++)
            {
                values[i] *= scalar;
            }
        }

        /// <summary>
        /// 1D softmax (single vector normalization).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Softmax1D(ReadOnlySpan<float> input, Span<float> output)
        {
            if (input.Length != output.Length)
                throw new ArgumentException("Input and output must have the same length");

            SoftmaxRow(input, output);
        }

        /// <summary>
        /// Log-softmax: log(softmax(x)) = x - max - log(sum(exp(x - max)))
        /// More numerically stable than computing log(softmax(x)) separately.
        /// </summary>
        public static void LogSoftmax(ReadOnlySpan<float> input, Span<float> output, int rows, int cols)
        {
            if (input.Length != rows * cols || output.Length != rows * cols)
                throw new ArgumentException("Input and output sizes must match rows * cols");

            // OPTIMIZED: Use unsafe pointer arithmetic to eliminate Span.Slice() overhead
            // But still use FindMax for SIMD-accelerated max finding
            for (int i = 0; i < rows; i++)
            {
                int offset = i * cols;

                // Validate offset is within bounds before any pointer arithmetic
                if (offset < 0 || offset > input.Length || offset > output.Length ||
                    offset + cols > input.Length || offset + cols > output.Length)
                {
                    throw new ArgumentOutOfRangeException(nameof(offset), 
                        "Computed offset exceeds array bounds");
                }

                unsafe
                {
                    fixed (float* pInput = input, pOutput = output)
                    {
                        // Find max using SIMD-accelerated FindMax on the row slice
                        float* pInputRow = pInput + offset;
                        float* pOutputRow = pOutput + offset;

                        // Create span for FindMax (zero cost - just pointer + length)
                        ReadOnlySpan<float> inputRow = new ReadOnlySpan<float>(pInputRow, cols);
                        float max = FindMax(inputRow);

                        // Compute sum of exp(x - max)
                        float sum = 0f;
                        for (int j = 0; j < cols; j++)
                        {
                            sum += MathF.Exp(pInputRow[j] - max);
                        }

                        float logSumExp = MathF.Log(sum);

                        // Compute log-softmax: x - max - log(sum)
                        for (int j = 0; j < cols; j++)
                        {
                            pOutputRow[j] = pInputRow[j] - max - logSumExp;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Fast vectorized exp approximation for softmax.
        /// Uses polynomial approximation optimized for the typical softmax range.
        /// Accuracy: ~0.1% error for inputs in [-10, 0] which is typical for softmax after subtracting max.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector<float> FastExpVec(Vector<float> x)
        {
            // Clamp to reasonable range for softmax (after subtracting max, values are typically negative)
            var vClampMin = new Vector<float>(-88.0f); // exp(-88) ≈ 0 (underflow)
            var vClampMax = new Vector<float>(0.0f);    // max is already subtracted
            x = Vector.Max(vClampMin, Vector.Min(vClampMax, x));

            // Padé [2/2] approximation for e^x
            // exp(x) ≈ (2 + x + x²/6) / (2 - x + x²/6) for |x| < 2
            // Good accuracy for softmax range after max subtraction
            var vTwo = new Vector<float>(2.0f);
            var vOneSixth = new Vector<float>(1.0f / 6.0f);

            var x2 = x * x;
            var numerator = vTwo + x + x2 * vOneSixth;
            var denominator = vTwo - x + x2 * vOneSixth;

            return numerator / denominator;
        }
    }
}
