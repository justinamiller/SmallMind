using System;
using SmallMind.Core.Core;

namespace SmallMind.Runtime.Metrics
{
    /// <summary>
    /// Utilities for computing model quality metrics.
    /// </summary>
    internal static class MetricsComputer
    {
        /// <summary>
        /// Compute token-level prediction accuracy.
        /// Returns the fraction of tokens where the model's top-1 prediction matches the target.
        /// </summary>
        /// <param name="logits">Model output logits (B, T, V)</param>
        /// <param name="targets">Target token indices (B, T)</param>
        /// <returns>Accuracy in range [0, 1]</returns>
        public static float ComputeTokenAccuracy(Tensor logits, Tensor targets)
        {
            if (logits.Shape.Length != 3)
                throw new ArgumentException("Logits must have shape (B, T, V)");
            if (targets.Shape.Length != 2)
                throw new ArgumentException("Targets must have shape (B, T)");

            int B = logits.Shape[0];
            int T = logits.Shape[1];
            int V = logits.Shape[2];

            int correctCount = 0;
            int totalCount = B * T;

            for (int b = 0; b < B; b++)
            {
                for (int t = 0; t < T; t++)
                {
                    int targetClass = (int)targets.Data[b * T + t];
                    if (targetClass < 0 || targetClass >= V) continue;

                    int offset = (b * T + t) * V;

                    // Find the token with highest logit (argmax)
                    int predictedClass = 0;
                    float maxLogit = logits.Data[offset];
                    for (int v = 1; v < V; v++)
                    {
                        if (logits.Data[offset + v] > maxLogit)
                        {
                            maxLogit = logits.Data[offset + v];
                            predictedClass = v;
                        }
                    }

                    if (predictedClass == targetClass)
                    {
                        correctCount++;
                    }
                }
            }

            return totalCount > 0 ? (float)correctCount / totalCount : 0f;
        }

        /// <summary>
        /// Compute gradient statistics for a list of parameters.
        /// Useful for detecting gradient issues (vanishing, exploding, NaN, Inf).
        /// </summary>
        /// <param name="parameters">List of model parameters</param>
        /// <returns>Gradient statistics</returns>
        public static (float meanNorm, float maxNorm, float minNorm, int nanCount, int infCount) ComputeGradientStats(
            System.Collections.Generic.List<Tensor> parameters)
        {
            float sumNorms = 0f;
            float maxNorm = float.NegativeInfinity;
            float minNorm = float.PositiveInfinity;
            int nanCount = 0;
            int infCount = 0;
            int paramCount = 0;

            foreach (var param in parameters)
            {
                if (param.Grad == null || !param.RequiresGrad) continue;

                float norm = 0f;
                for (int i = 0; i < param.Grad.Length; i++)
                {
                    float g = param.Grad[i];
                    
                    if (float.IsNaN(g))
                    {
                        nanCount++;
                        continue;
                    }
                    if (float.IsInfinity(g))
                    {
                        infCount++;
                        continue;
                    }

                    norm += g * g;
                }

                norm = MathF.Sqrt(norm);
                
                if (!float.IsNaN(norm) && !float.IsInfinity(norm))
                {
                    sumNorms += norm;
                    maxNorm = Math.Max(maxNorm, norm);
                    minNorm = Math.Min(minNorm, norm);
                    paramCount++;
                }
            }

            float meanNorm = paramCount > 0 ? sumNorms / paramCount : 0f;
            
            // Handle case where no valid gradients found
            if (paramCount == 0)
            {
                maxNorm = 0f;
                minNorm = 0f;
            }

            return (meanNorm, maxNorm, minNorm, nanCount, infCount);
        }

        /// <summary>
        /// Check if gradients are healthy (no NaN/Inf, reasonable magnitude).
        /// </summary>
        /// <param name="meanNorm">Average gradient norm</param>
        /// <param name="maxNorm">Maximum gradient norm</param>
        /// <param name="nanCount">Number of NaN gradients</param>
        /// <param name="infCount">Number of Inf gradients</param>
        /// <param name="maxAllowedNorm">Maximum allowed gradient norm (default: 100)</param>
        /// <returns>True if gradients are healthy</returns>
        public static bool AreGradientsHealthy(float meanNorm, float maxNorm, int nanCount, int infCount, 
            float maxAllowedNorm = 100f)
        {
            // Check for NaN or Inf
            if (nanCount > 0 || infCount > 0)
                return false;

            // Check for exploding gradients
            if (maxNorm > maxAllowedNorm)
                return false;

            // Check for vanishing gradients (too small)
            if (meanNorm < 1e-8f)
                return false;

            return true;
        }

        /// <summary>
        /// Compute perplexity from loss.
        /// Perplexity = exp(loss), clamped to prevent overflow.
        /// </summary>
        public static float ComputePerplexity(float loss)
        {
            // Clamp to prevent overflow (exp(11.5) ≈ 99,000)
            float clampedLoss = Math.Min(loss, 11.5f);
            return MathF.Exp(clampedLoss);
        }
    }
}
