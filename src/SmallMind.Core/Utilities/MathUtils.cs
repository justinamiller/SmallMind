using System.Runtime.CompilerServices;

namespace SmallMind.Core.Utilities
{
    /// <summary>
    /// Fast mathematical approximations optimized for neural network operations.
    /// Prioritizes performance over exact accuracy where acceptable for ML workloads.
    /// </summary>
    internal static class MathUtils
    {
        /// <summary>
        /// Exponential function. Delegates to MathF.Exp for correctness.
        /// The Padé [2/2] approximation previously used here had unacceptable error
        /// (>1000% relative error) for inputs below -3, which is common in softmax
        /// attention after max subtraction and causes completely wrong attention weights.
        /// </summary>
        /// <param name="x">Input value</param>
        /// <returns>exp(x)</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float FastExp(float x)
        {
            return MathF.Exp(x);
        }
    }
}
