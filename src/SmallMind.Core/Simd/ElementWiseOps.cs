using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace SmallMind.Core.Simd
{
    /// <summary>
    /// SIMD-accelerated element-wise operations for tensors.
    /// Supports add, subtract, multiply, multiply-add (FMA), and scale operations.
    /// All operations are allocation-free and use the best available SIMD instruction set.
    /// </summary>
    public static class ElementWiseOps
    {
        /// <summary>
        /// Element-wise addition: result[i] = a[i] + b[i]
        /// Uses SIMD acceleration with Vector&lt;float&gt; for portable performance.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Add(ReadOnlySpan<float> a, ReadOnlySpan<float> b, Span<float> result)
        {
            if (a.Length != b.Length || a.Length != result.Length)
                throw new ArgumentException("All spans must have the same length");

            int length = a.Length;
            int vectorSize = Vector<float>.Count;
            int i = 0;

            // SIMD loop - process multiple elements per iteration
            for (; i <= length - vectorSize; i += vectorSize)
            {
                var va = new Vector<float>(a.Slice(i));
                var vb = new Vector<float>(b.Slice(i));
                (va + vb).CopyTo(result.Slice(i));
            }

            // Scalar remainder
            for (; i < length; i++)
            {
                result[i] = a[i] + b[i];
            }
        }

        /// <summary>
        /// Element-wise subtraction: result[i] = a[i] - b[i]
        /// Uses SIMD acceleration with Vector&lt;float&gt; for portable performance.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Subtract(ReadOnlySpan<float> a, ReadOnlySpan<float> b, Span<float> result)
        {
            if (a.Length != b.Length || a.Length != result.Length)
                throw new ArgumentException("All spans must have the same length");

            int length = a.Length;
            int vectorSize = Vector<float>.Count;
            int i = 0;

            // SIMD loop
            for (; i <= length - vectorSize; i += vectorSize)
            {
                var va = new Vector<float>(a.Slice(i));
                var vb = new Vector<float>(b.Slice(i));
                (va - vb).CopyTo(result.Slice(i));
            }

            // Scalar remainder
            for (; i < length; i++)
            {
                result[i] = a[i] - b[i];
            }
        }

        /// <summary>
        /// Element-wise multiplication: result[i] = a[i] * b[i]
        /// Uses SIMD acceleration with Vector&lt;float&gt; for portable performance.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Multiply(ReadOnlySpan<float> a, ReadOnlySpan<float> b, Span<float> result)
        {
            if (a.Length != b.Length || a.Length != result.Length)
                throw new ArgumentException("All spans must have the same length");

            int length = a.Length;
            int vectorSize = Vector<float>.Count;
            int i = 0;

            // SIMD loop
            for (; i <= length - vectorSize; i += vectorSize)
            {
                var va = new Vector<float>(a.Slice(i));
                var vb = new Vector<float>(b.Slice(i));
                (va * vb).CopyTo(result.Slice(i));
            }

            // Scalar remainder
            for (; i < length; i++)
            {
                result[i] = a[i] * b[i];
            }
        }

        /// <summary>
        /// Element-wise multiply-add: result[i] = a[i] * b[i] + c[i]
        /// Uses FMA intrinsics when available for better performance and accuracy.
        /// Falls back to Vector&lt;float&gt; multiply + add otherwise.
        /// </summary>
        public static void MultiplyAdd(ReadOnlySpan<float> a, ReadOnlySpan<float> b, ReadOnlySpan<float> c, Span<float> result)
        {
            if (a.Length != b.Length || a.Length != c.Length || a.Length != result.Length)
                throw new ArgumentException("All spans must have the same length");

            int length = a.Length;

            // Use FMA intrinsics if available (AVX2 or FMA)
            if (Fma.IsSupported && length >= Vector256<float>.Count)
            {
                MultiplyAddFma(a, b, c, result);
            }
            else
            {
                MultiplyAddVector(a, b, c, result);
            }
        }

        /// <summary>
        /// FMA implementation using hardware intrinsics (256-bit AVX2+FMA).
        /// </summary>
        private static void MultiplyAddFma(ReadOnlySpan<float> a, ReadOnlySpan<float> b, ReadOnlySpan<float> c, Span<float> result)
        {
            int length = a.Length;
            int vectorSize = Vector256<float>.Count; // 8 floats
            int i = 0;

            unsafe
            {
                fixed (float* pA = a, pB = b, pC = c, pResult = result)
                {
                    // SIMD loop with FMA
                    for (; i <= length - vectorSize; i += vectorSize)
                    {
                        Vector256<float> va = Avx.LoadVector256(pA + i);
                        Vector256<float> vb = Avx.LoadVector256(pB + i);
                        Vector256<float> vc = Avx.LoadVector256(pC + i);
                        
                        // FMA: va * vb + vc
                        Vector256<float> vResult = Fma.MultiplyAdd(va, vb, vc);
                        Avx.Store(pResult + i, vResult);
                    }
                }
            }

            // Scalar remainder
            for (; i < length; i++)
            {
                result[i] = a[i] * b[i] + c[i];
            }
        }

        /// <summary>
        /// Fallback implementation using Vector&lt;float&gt;.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void MultiplyAddVector(ReadOnlySpan<float> a, ReadOnlySpan<float> b, ReadOnlySpan<float> c, Span<float> result)
        {
            int length = a.Length;
            int vectorSize = Vector<float>.Count;
            int i = 0;

            // SIMD loop
            for (; i <= length - vectorSize; i += vectorSize)
            {
                var va = new Vector<float>(a.Slice(i));
                var vb = new Vector<float>(b.Slice(i));
                var vc = new Vector<float>(c.Slice(i));
                (va * vb + vc).CopyTo(result.Slice(i));
            }

            // Scalar remainder
            for (; i < length; i++)
            {
                result[i] = a[i] * b[i] + c[i];
            }
        }

        /// <summary>
        /// Scalar multiplication: result[i] = a[i] * scalar
        /// Uses SIMD acceleration with broadcasted scalar value.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Scale(ReadOnlySpan<float> a, float scalar, Span<float> result)
        {
            if (a.Length != result.Length)
                throw new ArgumentException("Input and output spans must have the same length");

            int length = a.Length;
            int vectorSize = Vector<float>.Count;
            int i = 0;

            // Broadcast scalar to vector
            var vScalar = new Vector<float>(scalar);

            // SIMD loop
            for (; i <= length - vectorSize; i += vectorSize)
            {
                var va = new Vector<float>(a.Slice(i));
                (va * vScalar).CopyTo(result.Slice(i));
            }

            // Scalar remainder
            for (; i < length; i++)
            {
                result[i] = a[i] * scalar;
            }
        }

        /// <summary>
        /// In-place scalar addition: a[i] += scalar
        /// Uses SIMD acceleration with broadcasted scalar value.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddScalarInPlace(Span<float> a, float scalar)
        {
            int length = a.Length;
            int vectorSize = Vector<float>.Count;
            int i = 0;

            // Broadcast scalar to vector
            var vScalar = new Vector<float>(scalar);

            // SIMD loop
            for (; i <= length - vectorSize; i += vectorSize)
            {
                var va = new Vector<float>(a.Slice(i));
                (va + vScalar).CopyTo(a.Slice(i));
            }

            // Scalar remainder
            for (; i < length; i++)
            {
                a[i] += scalar;
            }
        }

        /// <summary>
        /// In-place element-wise addition: a[i] += b[i]
        /// Uses SIMD acceleration for better performance.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddInPlace(Span<float> a, ReadOnlySpan<float> b)
        {
            if (a.Length != b.Length)
                throw new ArgumentException("Spans must have the same length");

            int length = a.Length;
            int vectorSize = Vector<float>.Count;
            int i = 0;

            // SIMD loop
            for (; i <= length - vectorSize; i += vectorSize)
            {
                var va = new Vector<float>(a.Slice(i));
                var vb = new Vector<float>(b.Slice(i));
                (va + vb).CopyTo(a.Slice(i));
            }

            // Scalar remainder
            for (; i < length; i++)
            {
                a[i] += b[i];
            }
        }
    }
}
