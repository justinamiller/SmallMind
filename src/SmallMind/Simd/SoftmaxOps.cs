using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace SmallMind.Simd
{
    /// <summary>
    /// SIMD-accelerated softmax operation with numerical stability.
    /// Implements the stable softmax: exp(x - max(x)) / sum(exp(x - max(x)))
    /// </summary>
    public static class SoftmaxOps
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
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SoftmaxRowIndexed(float[] input, float[] output, int offset, int length)
        {
            // Step 1: Find max
            float max = float.NegativeInfinity;
            for (int i = 0; i < length; i++)
            {
                if (input[offset + i] > max)
                    max = input[offset + i];
            }

            // Step 2: Compute exp(x - max) and sum
            float sum = 0f;
            for (int i = 0; i < length; i++)
            {
                float exp = MathF.Exp(input[offset + i] - max);
                output[offset + i] = exp;
                sum += exp;
            }

            // Step 3: Normalize by sum
            float invSum = 1f / sum;
            for (int i = 0; i < length; i++)
            {
                output[offset + i] *= invSum;
            }
        }

        /// <summary>
        /// Applies softmax to a single row with SIMD acceleration (span-based).
        /// Implements numerically stable softmax: exp(x - max) / sum(exp(x - max))
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SoftmaxRow(ReadOnlySpan<float> input, Span<float> output)
        {
            int length = input.Length;
            
            // Step 1: Find max (SIMD accelerated)
            float max = FindMax(input);

            // Step 2: Compute exp(x - max) and sum (fused loop for cache efficiency)
            // Note: exp() has no SIMD intrinsic, so this is scalar
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
        /// Finds the maximum value in a span using SIMD.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float FindMax(ReadOnlySpan<float> values)
        {
            int length = values.Length;
            int vectorSize = Vector<float>.Count;
            int i = 0;

            // Initialize with negative infinity
            var maxVec = new Vector<float>(float.NegativeInfinity);

            // SIMD loop
            for (; i <= length - vectorSize; i += vectorSize)
            {
                var v = new Vector<float>(values.Slice(i));
                maxVec = Vector.Max(maxVec, v);
            }

            // Horizontal max reduction
            float max = float.NegativeInfinity;
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
        /// In-place scalar multiplication using SIMD.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Scale(Span<float> values, float scalar)
        {
            int length = values.Length;
            int vectorSize = Vector<float>.Count;
            int i = 0;

            var vScalar = new Vector<float>(scalar);

            // SIMD loop
            for (; i <= length - vectorSize; i += vectorSize)
            {
                var v = new Vector<float>(values.Slice(i));
                (v * vScalar).CopyTo(values.Slice(i));
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

            for (int i = 0; i < rows; i++)
            {
                int offset = i * cols;
                var inputRow = input.Slice(offset, cols);
                var outputRow = output.Slice(offset, cols);

                // Find max
                float max = FindMax(inputRow);

                // Compute sum of exp(x - max)
                float sum = 0f;
                for (int j = 0; j < cols; j++)
                {
                    sum += MathF.Exp(inputRow[j] - max);
                }

                float logSumExp = MathF.Log(sum);

                // Compute log-softmax: x - max - log(sum)
                for (int j = 0; j < cols; j++)
                {
                    outputRow[j] = inputRow[j] - max - logSumExp;
                }
            }
        }
    }
}
