using System;
using System.Collections.Generic;
using System.Linq;

namespace SmallMind.Runtime.Metrics
{
    /// <summary>
    /// Tracks model quality metrics during training and validation.
    /// Provides insights into prediction quality, training progress, and model health.
    /// </summary>
    internal sealed class TrainingMetrics
    {
        private readonly List<float> _trainingLosses = new List<float>();
        private readonly List<float> _validationLosses = new List<float>();
        private readonly List<float> _perplexities = new List<float>();
        private readonly List<float> _tokenAccuracies = new List<float>();
        private readonly List<GradientStats> _gradientStats = new List<GradientStats>();
        
        /// <summary>
        /// Record a training loss value for a step.
        /// </summary>
        public void RecordTrainingLoss(float loss)
        {
            if (!float.IsNaN(loss) && !float.IsInfinity(loss))
            {
                _trainingLosses.Add(loss);
            }
        }

        /// <summary>
        /// Record a validation loss and compute perplexity.
        /// Perplexity = exp(loss) measures how well the model predicts the next token.
        /// Lower perplexity indicates better prediction quality.
        /// </summary>
        public void RecordValidationLoss(float loss)
        {
            if (!float.IsNaN(loss) && !float.IsInfinity(loss))
            {
                _validationLosses.Add(loss);
                
                // Compute perplexity: exp(loss)
                // Clamp loss to avoid overflow (max perplexity ~88,000)
                float clampedLoss = Math.Min(loss, 11.5f);
                float perplexity = MathF.Exp(clampedLoss);
                _perplexities.Add(perplexity);
            }
        }

        /// <summary>
        /// Record token-level prediction accuracy.
        /// Accuracy measures what percentage of tokens are predicted correctly.
        /// </summary>
        public void RecordTokenAccuracy(float accuracy)
        {
            if (accuracy >= 0f && accuracy <= 1f)
            {
                _tokenAccuracies.Add(accuracy);
            }
        }

        /// <summary>
        /// Record gradient statistics for monitoring training health.
        /// </summary>
        public void RecordGradientStats(float meanNorm, float maxNorm, float minNorm, int nanCount, int infCount)
        {
            _gradientStats.Add(new GradientStats
            {
                MeanNorm = meanNorm,
                MaxNorm = maxNorm,
                MinNorm = minNorm,
                NanCount = nanCount,
                InfCount = infCount
            });
        }

        /// <summary>
        /// Get the most recent training loss.
        /// </summary>
        public float? GetCurrentTrainingLoss()
        {
            return _trainingLosses.Count > 0 ? _trainingLosses[^1] : null;
        }

        /// <summary>
        /// Get the most recent validation loss.
        /// </summary>
        public float? GetCurrentValidationLoss()
        {
            return _validationLosses.Count > 0 ? _validationLosses[^1] : null;
        }

        /// <summary>
        /// Get the most recent perplexity value.
        /// </summary>
        public float? GetCurrentPerplexity()
        {
            return _perplexities.Count > 0 ? _perplexities[^1] : null;
        }

        /// <summary>
        /// Get the most recent token accuracy.
        /// </summary>
        public float? GetCurrentTokenAccuracy()
        {
            return _tokenAccuracies.Count > 0 ? _tokenAccuracies[^1] : null;
        }

        /// <summary>
        /// Get the best (lowest) validation loss observed.
        /// </summary>
        public float? GetBestValidationLoss()
        {
            return _validationLosses.Count > 0 ? _validationLosses.Min() : null;
        }

        /// <summary>
        /// Get the best (lowest) perplexity observed.
        /// </summary>
        public float? GetBestPerplexity()
        {
            return _perplexities.Count > 0 ? _perplexities.Min() : null;
        }

        /// <summary>
        /// Get summary statistics for all metrics.
        /// </summary>
        public MetricsSummary GetSummary()
        {
            return new MetricsSummary
            {
                TrainingLossStats = ComputeStats(_trainingLosses),
                ValidationLossStats = ComputeStats(_validationLosses),
                PerplexityStats = ComputeStats(_perplexities),
                TokenAccuracyStats = ComputeStats(_tokenAccuracies),
                GradientHealthSummary = _gradientStats.Count > 0 ? SummarizeGradientHealth() : null,
                TotalTrainingSteps = _trainingLosses.Count,
                TotalValidationSteps = _validationLosses.Count
            };
        }

        /// <summary>
        /// Check if training is making progress based on recent loss trend.
        /// </summary>
        public bool IsTrainingProgressing(int lookbackSteps = 10)
        {
            if (_trainingLosses.Count < lookbackSteps + 1)
                return true; // Not enough data, assume progressing

            var recent = _trainingLosses.TakeLast(lookbackSteps).ToList();
            var previous = _trainingLosses.Skip(Math.Max(0, _trainingLosses.Count - 2 * lookbackSteps))
                                         .Take(lookbackSteps).ToList();

            if (previous.Count == 0) return true;

            float recentAvg = recent.Average();
            float previousAvg = previous.Average();

            // Progress if recent average is lower (better)
            return recentAvg < previousAvg;
        }

        /// <summary>
        /// Get a formatted report of current metrics.
        /// </summary>
        public string GetReport()
        {
            var summary = GetSummary();
            var report = new System.Text.StringBuilder();

            report.AppendLine("=== Training Metrics Report ===");
            report.AppendLine();

            if (summary.TrainingLossStats != null)
            {
                report.AppendLine("Training Loss:");
                report.AppendLine($"  Current: {GetCurrentTrainingLoss():F4}");
                report.AppendLine($"  Average: {summary.TrainingLossStats.Mean:F4}");
                report.AppendLine($"  Best:    {summary.TrainingLossStats.Min:F4}");
                report.AppendLine();
            }

            if (summary.ValidationLossStats != null)
            {
                report.AppendLine("Validation Loss:");
                report.AppendLine($"  Current: {GetCurrentValidationLoss():F4}");
                report.AppendLine($"  Average: {summary.ValidationLossStats.Mean:F4}");
                report.AppendLine($"  Best:    {GetBestValidationLoss():F4}");
                report.AppendLine();
            }

            if (summary.PerplexityStats != null)
            {
                report.AppendLine("Perplexity (lower is better):");
                report.AppendLine($"  Current: {GetCurrentPerplexity():F2}");
                report.AppendLine($"  Average: {summary.PerplexityStats.Mean:F2}");
                report.AppendLine($"  Best:    {GetBestPerplexity():F2}");
                report.AppendLine();
            }

            if (summary.TokenAccuracyStats != null)
            {
                report.AppendLine("Token Prediction Accuracy:");
                report.AppendLine($"  Current: {GetCurrentTokenAccuracy() * 100:F2}%");
                report.AppendLine($"  Average: {summary.TokenAccuracyStats.Mean * 100:F2}%");
                report.AppendLine($"  Best:    {summary.TokenAccuracyStats.Max * 100:F2}%");
                report.AppendLine();
            }

            if (summary.GradientHealthSummary != null)
            {
                var gh = summary.GradientHealthSummary;
                report.AppendLine("Gradient Health:");
                report.AppendLine($"  Average Norm: {gh.AverageMeanNorm:F6}");
                report.AppendLine($"  Max Norm:     {gh.MaxNormSeen:F6}");
                report.AppendLine($"  Issues:       {gh.TotalNanCount} NaN, {gh.TotalInfCount} Inf");
                report.AppendLine();
            }

            report.AppendLine($"Total Steps: {summary.TotalTrainingSteps} training, {summary.TotalValidationSteps} validation");

            return report.ToString();
        }

        private StatsSummary? ComputeStats(List<float> values)
        {
            if (values.Count == 0) return null;

            return new StatsSummary
            {
                Count = values.Count,
                Mean = values.Average(),
                Min = values.Min(),
                Max = values.Max(),
                StdDev = ComputeStdDev(values)
            };
        }

        private float ComputeStdDev(List<float> values)
        {
            if (values.Count < 2) return 0f;

            float mean = values.Average();
            float sumSquaredDiffs = values.Sum(v => (v - mean) * (v - mean));
            return MathF.Sqrt(sumSquaredDiffs / values.Count);
        }

        private GradientHealthSummary SummarizeGradientHealth()
        {
            return new GradientHealthSummary
            {
                AverageMeanNorm = _gradientStats.Average(g => g.MeanNorm),
                MaxNormSeen = _gradientStats.Max(g => g.MaxNorm),
                MinNormSeen = _gradientStats.Min(g => g.MinNorm),
                TotalNanCount = _gradientStats.Sum(g => g.NanCount),
                TotalInfCount = _gradientStats.Sum(g => g.InfCount),
                HealthyGradientSteps = _gradientStats.Count(g => g.NanCount == 0 && g.InfCount == 0)
            };
        }
    }

    /// <summary>
    /// Statistics for gradient health monitoring.
    /// </summary>
    internal sealed class GradientStats
    {
        public float MeanNorm { get; set; }
        public float MaxNorm { get; set; }
        public float MinNorm { get; set; }
        public int NanCount { get; set; }
        public int InfCount { get; set; }
    }

    /// <summary>
    /// Summary of statistical metrics.
    /// </summary>
    internal sealed class StatsSummary
    {
        public int Count { get; set; }
        public float Mean { get; set; }
        public float Min { get; set; }
        public float Max { get; set; }
        public float StdDev { get; set; }
    }

    /// <summary>
    /// Overall summary of gradient health.
    /// </summary>
    internal sealed class GradientHealthSummary
    {
        public float AverageMeanNorm { get; set; }
        public float MaxNormSeen { get; set; }
        public float MinNormSeen { get; set; }
        public int TotalNanCount { get; set; }
        public int TotalInfCount { get; set; }
        public int HealthyGradientSteps { get; set; }
    }

    /// <summary>
    /// Complete summary of all training metrics.
    /// </summary>
    internal sealed class MetricsSummary
    {
        public StatsSummary? TrainingLossStats { get; set; }
        public StatsSummary? ValidationLossStats { get; set; }
        public StatsSummary? PerplexityStats { get; set; }
        public StatsSummary? TokenAccuracyStats { get; set; }
        public GradientHealthSummary? GradientHealthSummary { get; set; }
        public int TotalTrainingSteps { get; set; }
        public int TotalValidationSteps { get; set; }
    }
}
