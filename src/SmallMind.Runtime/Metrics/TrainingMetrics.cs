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
            if (_validationLosses.Count == 0)
                return null;

            // Manual min to avoid LINQ allocation
            float min = _validationLosses[0];
            for (int i = 1; i < _validationLosses.Count; i++)
            {
                if (_validationLosses[i] < min)
                    min = _validationLosses[i];
            }
            return min;
        }

        /// <summary>
        /// Get the best (lowest) perplexity observed.
        /// </summary>
        public float? GetBestPerplexity()
        {
            if (_perplexities.Count == 0)
                return null;

            // Manual min to avoid LINQ allocation
            float min = _perplexities[0];
            for (int i = 1; i < _perplexities.Count; i++)
            {
                if (_perplexities[i] < min)
                    min = _perplexities[i];
            }
            return min;
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

            // Manual averaging to avoid LINQ allocations
            int count = _trainingLosses.Count;

            // Calculate recent average (last lookbackSteps items)
            int recentStart = Math.Max(0, count - lookbackSteps);
            int recentCount = count - recentStart;
            float recentSum = 0f;
            for (int i = recentStart; i < count; i++)
            {
                recentSum += _trainingLosses[i];
            }
            float recentAvg = recentSum / recentCount;

            // Calculate previous average (lookbackSteps items before recent)
            int previousStart = Math.Max(0, count - 2 * lookbackSteps);
            int previousEnd = recentStart;
            int previousCount = previousEnd - previousStart;

            if (previousCount == 0) return true;

            float previousSum = 0f;
            for (int i = previousStart; i < previousEnd; i++)
            {
                previousSum += _trainingLosses[i];
            }
            float previousAvg = previousSum / previousCount;

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

            // Manual computation to avoid LINQ allocations
            int count = values.Count;
            float sum = 0f;
            float min = values[0];
            float max = values[0];

            for (int i = 0; i < count; i++)
            {
                float val = values[i];
                sum += val;
                if (val < min) min = val;
                if (val > max) max = val;
            }

            float mean = sum / count;

            return new StatsSummary
            {
                Count = count,
                Mean = mean,
                Min = min,
                Max = max,
                StdDev = ComputeStdDev(values, mean)
            };
        }

        private float ComputeStdDev(List<float> values, float mean)
        {
            if (values.Count < 2) return 0f;

            // Manual computation to avoid LINQ allocations
            float sumSquaredDiffs = 0f;
            for (int i = 0; i < values.Count; i++)
            {
                float diff = values[i] - mean;
                sumSquaredDiffs += diff * diff;
            }
            return MathF.Sqrt(sumSquaredDiffs / values.Count);
        }

        private GradientHealthSummary SummarizeGradientHealth()
        {
            // Manual computation to avoid LINQ allocations
            float sumMeanNorm = 0f;
            float maxNorm = float.NegativeInfinity;
            float minNorm = float.PositiveInfinity;
            int totalNanCount = 0;
            int totalInfCount = 0;
            int healthyCount = 0;

            for (int i = 0; i < _gradientStats.Count; i++)
            {
                var stats = _gradientStats[i];
                sumMeanNorm += stats.MeanNorm;
                if (stats.MaxNorm > maxNorm) maxNorm = stats.MaxNorm;
                if (stats.MinNorm < minNorm) minNorm = stats.MinNorm;
                totalNanCount += stats.NanCount;
                totalInfCount += stats.InfCount;
                if (stats.NanCount == 0 && stats.InfCount == 0)
                    healthyCount++;
            }

            return new GradientHealthSummary
            {
                AverageMeanNorm = sumMeanNorm / _gradientStats.Count,
                MaxNormSeen = maxNorm,
                MinNormSeen = minNorm,
                TotalNanCount = totalNanCount,
                TotalInfCount = totalInfCount,
                HealthyGradientSteps = healthyCount
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
