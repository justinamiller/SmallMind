using System.Runtime.CompilerServices;
using SmallMind.Core.Core;

namespace SmallMind.Transformers
{
    /// <summary>
    /// Internal helper class for common Transformer operations.
    /// Consolidates duplicated code patterns across Transformer components.
    /// </summary>
    internal static class TransformerHelpers
    {
        /// <summary>
        /// Executes forward pass through a normalization layer with in-place destination.
        /// Handles LayerNorm, RMSNorm, and fallback cases.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void ForwardNorm(Module norm, Tensor input, Tensor dest)
        {
            if (norm is LayerNorm layerNorm)
            {
                layerNorm.Forward(input, dest);
            }
            else if (norm is RMSNorm rmsNorm)
            {
                rmsNorm.Forward(input, dest);
            }
            else
            {
                // Fallback: use base Forward and copy result
                var result = norm.Forward(input);
                result.Data.CopyTo(dest.Data, 0);
            }
        }

        /// <summary>
        /// Executes work in parallel or sequentially based on workload size.
        /// Uses parallel execution for workloads >= 4 to amortize threading overhead.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void ParallelOrSequential(int workSize, Action<int> body)
        {
            if (workSize >= 4)
            {
                Parallel.For(0, workSize, body);
            }
            else
            {
                for (int i = 0; i < workSize; i++)
                {
                    body(i);
                }
            }
        }

        /// <summary>
        /// Checks if two shape arrays are equal.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool ShapesMatch(int[] shape1, ReadOnlySpan<int> shape2)
        {
            if (shape1.Length != shape2.Length)
                return false;

            for (int i = 0; i < shape1.Length; i++)
            {
                if (shape1[i] != shape2[i])
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Updates a 4D shape cache array.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void UpdateShapeCache4D(int[] cache, int dim0, int dim1, int dim2, int dim3)
        {
            cache[0] = dim0;
            cache[1] = dim1;
            cache[2] = dim2;
            cache[3] = dim3;
        }

        /// <summary>
        /// Updates a 3D shape cache array.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void UpdateShapeCache3D(int[] cache, int dim0, int dim1, int dim2)
        {
            cache[0] = dim0;
            cache[1] = dim1;
            cache[2] = dim2;
        }
    }
}
