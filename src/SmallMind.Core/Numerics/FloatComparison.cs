using System.Runtime.CompilerServices;

namespace SmallMind.Core.Numerics
{
    /// <summary>
    /// Utilities for safe floating-point comparisons.
    /// Provides methods to avoid exact equality checks on computed floating-point values.
    /// </summary>
    internal static class FloatComparison
    {
        /// <summary>
        /// Machine epsilon for float (single precision).
        /// This is the smallest value e such that 1.0f + e != 1.0f.
        /// </summary>
        private const float Epsilon = 1.1920929e-7f; // float.Epsilon is too small (denormalized)

        /// <summary>
        /// Checks if a floating-point value is effectively zero.
        /// Use this instead of `value == 0f` for computed values.
        /// For exact zero checks (e.g., after explicit assignment), use IsExactZero.
        /// </summary>
        /// <param name="value">The value to check.</param>
        /// <returns>True if the absolute value is smaller than epsilon.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNearZero(float value)
        {
            // For values that should be exactly zero (e.g., set to 0f or cleared),
            // they will pass this check. For computed values that are very close to zero,
            // this provides numerical safety.
            return MathF.Abs(value) < Epsilon;
        }

        /// <summary>
        /// Checks if a floating-point value is exactly zero (bit-wise).
        /// Use this ONLY for sparsity optimizations where the value was explicitly set to 0f.
        /// Do NOT use for computed values.
        /// </summary>
        /// <param name="value">The value to check.</param>
        /// <returns>True if the value is exactly 0.0f (bit-wise).</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsExactZero(float value)
        {
            // This check is safe for sparsity optimizations where:
            // 1. Values are explicitly initialized to 0f
            // 2. Values are set to 0f (e.g., after ReLU)
            // 3. Performance is critical (avoiding branch mispredictions)
            //
            // The comparison compiles to a single float comparison instruction,
            // so there's no performance overhead vs `value == 0f`.
            return value == 0f;
        }

        /// <summary>
        /// Checks if two floating-point values are approximately equal.
        /// </summary>
        /// <param name="a">First value.</param>
        /// <param name="b">Second value.</param>
        /// <param name="tolerance">Optional tolerance (defaults to machine epsilon).</param>
        /// <returns>True if values are within tolerance.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool AreEqual(float a, float b, float tolerance = Epsilon)
        {
            return MathF.Abs(a - b) < tolerance;
        }
    }
}
