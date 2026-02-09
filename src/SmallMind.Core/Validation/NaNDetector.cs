using System;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace SmallMind.Core.Validation
{
    /// <summary>
    /// Efficient NaN and Infinity detection for tensor operations.
    /// Uses SIMD intrinsics for high-performance validation.
    /// </summary>
    internal static class NaNDetector
    {
        /// <summary>
        /// Checks if any element in the span is NaN or Infinity.
        /// Returns index of first invalid value, or -1 if all valid.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int DetectInvalid(ReadOnlySpan<float> values)
        {
            if (values.Length == 0) return -1;
            
            // Use SIMD if available (8 floats at a time)
            if (Avx.IsSupported && values.Length >= 8)
            {
                return DetectInvalidSimd(values);
            }
            
            // Fallback to scalar
            for (int i = 0; i < values.Length; i++)
            {
                if (!float.IsFinite(values[i]))
                    return i;
            }
            return -1;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private static unsafe int DetectInvalidSimd(ReadOnlySpan<float> values)
        {
            fixed (float* ptr = values)
            {
                int i = 0;
                int vecLen = values.Length - (values.Length % 8);
                
                for (; i < vecLen; i += 8)
                {
                    Vector256<float> vec = Avx.LoadVector256(ptr + i);
                    
                    // Check for NaN using self-comparison
                    var nanCheck = Avx.Compare(vec, vec, FloatComparisonMode.UnorderedNonSignaling);
                    
                    // Check for Inf by comparing absolute value against max float
                    var absVec = Avx.And(vec, Vector256.Create(0x7FFFFFFF).AsSingle());
                    var infCheck = Avx.Compare(absVec, Vector256.Create(float.MaxValue), FloatComparisonMode.OrderedGreaterThanNonSignaling);
                    
                    // Combine checks
                    var invalidCheck = Avx.Or(nanCheck.AsSingle(), infCheck.AsSingle());
                    
                    if (!Avx.TestZ(invalidCheck.AsInt32(), invalidCheck.AsInt32()))
                    {
                        // Found invalid value - locate which lane
                        for (int j = 0; j < 8; j++)
                        {
                            if (!float.IsFinite(values[i + j]))
                                return i + j;
                        }
                    }
                }
                
                // Check remainder
                for (; i < values.Length; i++)
                {
                    if (!float.IsFinite(values[i]))
                        return i;
                }
            }
            return -1;
        }
        
        /// <summary>
        /// Replaces NaN/Inf values with specified replacement.
        /// Returns count of values replaced.
        /// </summary>
        public static int SanitizeInPlace(Span<float> values, float replacement = 0f)
        {
            int count = 0;
            for (int i = 0; i < values.Length; i++)
            {
                if (!float.IsFinite(values[i]))
                {
                    values[i] = replacement;
                    count++;
                }
            }
            return count;
        }
    }
}
