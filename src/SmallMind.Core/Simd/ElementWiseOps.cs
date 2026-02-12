using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Runtime.Intrinsics.Arm;

namespace SmallMind.Core.Simd
{
    /// <summary>
    /// SIMD-accelerated element-wise operations for tensors.
    /// Supports add, subtract, multiply, multiply-add (FMA), and scale operations.
    /// All operations are allocation-free and use the best available SIMD instruction set.
    /// TIER-5 OPTIMIZATION: [SkipLocalsInit] on class to avoid zero-initialization overhead in hot methods.
    /// </summary>
    [SkipLocalsInit]
    internal static class ElementWiseOps
    {
        /// <summary>
        /// Element-wise addition: result[i] = a[i] + b[i]
        /// Uses SIMD acceleration with AVX-512, AVX2, or Vector&lt;float&gt; fallback.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Add(ReadOnlySpan<float> a, ReadOnlySpan<float> b, Span<float> result)
        {
            if (a.Length != b.Length || a.Length != result.Length)
                throw new ArgumentException("All spans must have the same length");

            int length = a.Length;
            int i = 0;

            // AVX-512 path (16 floats)
            if (Avx512F.IsSupported && length >= 16)
            {
                unsafe
                {
                    fixed (float* pA = a, pB = b, pR = result)
                    {
                        for (; i <= length - 16; i += 16)
                        {
                            var va = Avx512F.LoadVector512(pA + i);
                            var vb = Avx512F.LoadVector512(pB + i);
                            Avx512F.Store(pR + i, Avx512F.Add(va, vb));
                        }
                    }
                }
            }
            // ARM NEON path (4 floats)
            else if (AdvSimd.Arm64.IsSupported && length >= 4)
            {
                unsafe
                {
                    fixed (float* pA = a, pB = b, pR = result)
                    {
                        for (; i <= length - 4; i += 4)
                        {
                            var va = AdvSimd.LoadVector128(pA + i);
                            var vb = AdvSimd.LoadVector128(pB + i);
                            AdvSimd.Store(pR + i, AdvSimd.Add(va, vb));
                        }
                    }
                }
            }

            // Vector<T> fallback - process remaining elements
            // OPTIMIZED: Use unsafe pointer arithmetic to eliminate Span.Slice() overhead
            int vectorSize = Vector<float>.Count;
            if (i <= length - vectorSize)
            {
                unsafe
                {
                    fixed (float* pA = a, pB = b, pR = result)
                    {
                        for (; i <= length - vectorSize; i += vectorSize)
                        {
                            var va = Unsafe.Read<Vector<float>>(pA + i);
                            var vb = Unsafe.Read<Vector<float>>(pB + i);
                            Unsafe.Write(pR + i, va + vb);
                        }
                    }
                }
            }

            // Scalar remainder
            for (; i < length; i++)
            {
                result[i] = a[i] + b[i];
            }
        }

        /// <summary>
        /// Element-wise subtraction: result[i] = a[i] - b[i]
        /// Uses SIMD acceleration with AVX-512, AVX2, or Vector&lt;float&gt; fallback.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Subtract(ReadOnlySpan<float> a, ReadOnlySpan<float> b, Span<float> result)
        {
            if (a.Length != b.Length || a.Length != result.Length)
                throw new ArgumentException("All spans must have the same length");

            int length = a.Length;
            int i = 0;

            // AVX-512 path (16 floats)
            if (Avx512F.IsSupported && length >= 16)
            {
                unsafe
                {
                    fixed (float* pA = a, pB = b, pR = result)
                    {
                        for (; i <= length - 16; i += 16)
                        {
                            var va = Avx512F.LoadVector512(pA + i);
                            var vb = Avx512F.LoadVector512(pB + i);
                            Avx512F.Store(pR + i, Avx512F.Subtract(va, vb));
                        }
                    }
                }
            }

            // Vector<T> fallback
            // OPTIMIZED: Use unsafe pointer arithmetic to eliminate Span.Slice() overhead
            int vectorSize = Vector<float>.Count;
            if (i <= length - vectorSize)
            {
                unsafe
                {
                    fixed (float* pA = a, pB = b, pR = result)
                    {
                        for (; i <= length - vectorSize; i += vectorSize)
                        {
                            var va = Unsafe.Read<Vector<float>>(pA + i);
                            var vb = Unsafe.Read<Vector<float>>(pB + i);
                            Unsafe.Write(pR + i, va - vb);
                        }
                    }
                }
            }

            // Scalar remainder
            for (; i < length; i++)
            {
                result[i] = a[i] - b[i];
            }
        }

        /// <summary>
        /// Element-wise multiplication: result[i] = a[i] * b[i]
        /// Uses SIMD acceleration with AVX-512, AVX2, or Vector&lt;float&gt; fallback.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Multiply(ReadOnlySpan<float> a, ReadOnlySpan<float> b, Span<float> result)
        {
            if (a.Length != b.Length || a.Length != result.Length)
                throw new ArgumentException("All spans must have the same length");

            int length = a.Length;
            int i = 0;

            // AVX-512 path (16 floats)
            if (Avx512F.IsSupported && length >= 16)
            {
                unsafe
                {
                    fixed (float* pA = a, pB = b, pR = result)
                    {
                        for (; i <= length - 16; i += 16)
                        {
                            var va = Avx512F.LoadVector512(pA + i);
                            var vb = Avx512F.LoadVector512(pB + i);
                            Avx512F.Store(pR + i, Avx512F.Multiply(va, vb));
                        }
                    }
                }
            }
            // ARM NEON path (4 floats)
            else if (AdvSimd.Arm64.IsSupported && length >= 4)
            {
                unsafe
                {
                    fixed (float* pA = a, pB = b, pR = result)
                    {
                        for (; i <= length - 4; i += 4)
                        {
                            var va = AdvSimd.LoadVector128(pA + i);
                            var vb = AdvSimd.LoadVector128(pB + i);
                            AdvSimd.Store(pR + i, AdvSimd.Multiply(va, vb));
                        }
                    }
                }
            }

            // Vector<T> fallback
            // OPTIMIZED: Use unsafe pointer arithmetic to eliminate Span.Slice() overhead
            int vectorSize = Vector<float>.Count;
            if (i <= length - vectorSize)
            {
                unsafe
                {
                    fixed (float* pA = a, pB = b, pR = result)
                    {
                        for (; i <= length - vectorSize; i += vectorSize)
                        {
                            var va = Unsafe.Read<Vector<float>>(pA + i);
                            var vb = Unsafe.Read<Vector<float>>(pB + i);
                            Unsafe.Write(pR + i, va * vb);
                        }
                    }
                }
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
        /// FMA implementation using hardware intrinsics (AVX-512 or 256-bit AVX2+FMA).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [SkipLocalsInit]
        private static void MultiplyAddFma(ReadOnlySpan<float> a, ReadOnlySpan<float> b, ReadOnlySpan<float> c, Span<float> result)
        {
            int length = a.Length;
            int i = 0;

            unsafe
            {
                fixed (float* pA = a, pB = b, pC = c, pResult = result)
                {
                    // AVX-512 path (16 floats)
                    if (Avx512F.IsSupported && length >= 16)
                    {
                        for (; i <= length - 16; i += 16)
                        {
                            Vector512<float> va = Avx512F.LoadVector512(pA + i);
                            Vector512<float> vb = Avx512F.LoadVector512(pB + i);
                            Vector512<float> vc = Avx512F.LoadVector512(pC + i);
                            Avx512F.Store(pResult + i, Avx512F.FusedMultiplyAdd(va, vb, vc));
                        }
                    }

                    // AVX2+FMA path (8 floats)
                    int vectorSize = Vector256<float>.Count; // 8 floats
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
        /// Uses SIMD acceleration with AVX-512, AVX2, or Vector&lt;T&gt; fallback.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Scale(ReadOnlySpan<float> a, float scalar, Span<float> result)
        {
            if (a.Length != result.Length)
                throw new ArgumentException("Input and output spans must have the same length");

            int length = a.Length;
            int i = 0;

            // AVX-512 path (16 floats)
            if (Avx512F.IsSupported && length >= 16)
            {
                var vScalar512 = Vector512.Create(scalar);
                unsafe
                {
                    fixed (float* pA = a, pR = result)
                    {
                        for (; i <= length - 16; i += 16)
                        {
                            var va = Avx512F.LoadVector512(pA + i);
                            Avx512F.Store(pR + i, Avx512F.Multiply(va, vScalar512));
                        }
                    }
                }
            }

            // Vector<T> fallback
            int vectorSize = Vector<float>.Count;
            var vScalar = new Vector<float>(scalar);
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
        /// Uses SIMD acceleration with AVX-512, AVX2, or Vector&lt;T&gt; fallback.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddScalarInPlace(Span<float> a, float scalar)
        {
            int length = a.Length;
            int i = 0;

            // AVX-512 path (16 floats)
            if (Avx512F.IsSupported && length >= 16)
            {
                var vScalar512 = Vector512.Create(scalar);
                unsafe
                {
                    fixed (float* pA = a)
                    {
                        for (; i <= length - 16; i += 16)
                        {
                            var va = Avx512F.LoadVector512(pA + i);
                            Avx512F.Store(pA + i, Avx512F.Add(va, vScalar512));
                        }
                    }
                }
            }

            // Vector<T> fallback
            // OPTIMIZED: Use unsafe pointer arithmetic to eliminate Span.Slice() overhead
            int vectorSize = Vector<float>.Count;
            var vScalar = new Vector<float>(scalar);
            if (i <= length - vectorSize)
            {
                unsafe
                {
                    fixed (float* pA = a)
                    {
                        for (; i <= length - vectorSize; i += vectorSize)
                        {
                            var va = Unsafe.Read<Vector<float>>(pA + i);
                            Unsafe.Write(pA + i, va + vScalar);
                        }
                    }
                }
            }

            // Scalar remainder
            for (; i < length; i++)
            {
                a[i] += scalar;
            }
        }

        /// <summary>
        /// In-place element-wise addition: a[i] += b[i]
        /// Uses SIMD acceleration with AVX-512, AVX2, or Vector&lt;T&gt; fallback.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddInPlace(Span<float> a, ReadOnlySpan<float> b)
        {
            if (a.Length != b.Length)
                throw new ArgumentException("Spans must have the same length");

            int length = a.Length;
            int i = 0;

            // AVX-512 path (16 floats)
            if (Avx512F.IsSupported && length >= 16)
            {
                unsafe
                {
                    fixed (float* pA = a, pB = b)
                    {
                        for (; i <= length - 16; i += 16)
                        {
                            var va = Avx512F.LoadVector512(pA + i);
                            var vb = Avx512F.LoadVector512(pB + i);
                            Avx512F.Store(pA + i, Avx512F.Add(va, vb));
                        }
                    }
                }
            }

            // Vector<T> fallback
            // OPTIMIZED: Use unsafe pointer arithmetic to eliminate Span.Slice() overhead
            int vectorSize = Vector<float>.Count;
            if (i <= length - vectorSize)
            {
                unsafe
                {
                    fixed (float* pA = a, pB = b)
                    {
                        for (; i <= length - vectorSize; i += vectorSize)
                        {
                            var va = Unsafe.Read<Vector<float>>(pA + i);
                            var vb = Unsafe.Read<Vector<float>>(pB + i);
                            Unsafe.Write(pA + i, va + vb);
                        }
                    }
                }
            }

            // Scalar remainder
            for (; i < length; i++)
            {
                a[i] += b[i];
            }
        }
    }
}
