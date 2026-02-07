using System;
using Xunit;
using SmallMind.Runtime.Metrics;
using SmallMind.Core.Core;
using SmallMind.Runtime;

namespace SmallMind.Tests.Metrics
{
    /// <summary>
    /// Tests for training metrics tracking and computation.
    /// </summary>
    public class TrainingMetricsTests
    {
        [Fact]
        public void RecordTrainingLoss_StoresValidLoss()
        {
            // Arrange
            var metrics = new TrainingMetrics();
            
            // Act
            metrics.RecordTrainingLoss(2.5f);
            metrics.RecordTrainingLoss(2.3f);
            metrics.RecordTrainingLoss(2.1f);
            
            // Assert
            Assert.Equal(2.1f, metrics.GetCurrentTrainingLoss());
            var summary = metrics.GetSummary();
            Assert.NotNull(summary.TrainingLossStats);
            Assert.Equal(3, summary.TrainingLossStats.Count);
            Assert.Equal(2.1f, summary.TrainingLossStats.Min);
        }

        [Fact]
        public void RecordValidationLoss_ComputesPerplexity()
        {
            // Arrange
            var metrics = new TrainingMetrics();
            
            // Act
            metrics.RecordValidationLoss(1.0f);
            
            // Assert
            float? perplexity = metrics.GetCurrentPerplexity();
            Assert.NotNull(perplexity);
            // exp(1.0) ≈ 2.718
            Assert.InRange(perplexity.Value, 2.7f, 2.8f);
        }

        [Fact]
        public void RecordValidationLoss_ClampsHighLoss()
        {
            // Arrange
            var metrics = new TrainingMetrics();
            
            // Act - record a very high loss
            metrics.RecordValidationLoss(100.0f);
            
            // Assert - perplexity should be clamped to prevent overflow
            float? perplexity = metrics.GetCurrentPerplexity();
            Assert.NotNull(perplexity);
            Assert.True(perplexity.Value < float.MaxValue);
        }

        [Fact]
        public void RecordTokenAccuracy_TracksAccuracy()
        {
            // Arrange
            var metrics = new TrainingMetrics();
            
            // Act
            metrics.RecordTokenAccuracy(0.5f);
            metrics.RecordTokenAccuracy(0.6f);
            metrics.RecordTokenAccuracy(0.7f);
            
            // Assert
            Assert.Equal(0.7f, metrics.GetCurrentTokenAccuracy());
            var summary = metrics.GetSummary();
            Assert.NotNull(summary.TokenAccuracyStats);
            Assert.Equal(0.7f, summary.TokenAccuracyStats.Max);
        }

        [Fact]
        public void RecordGradientStats_TracksGradientHealth()
        {
            // Arrange
            var metrics = new TrainingMetrics();
            
            // Act
            metrics.RecordGradientStats(meanNorm: 0.01f, maxNorm: 0.1f, minNorm: 0.001f, nanCount: 0, infCount: 0);
            metrics.RecordGradientStats(meanNorm: 0.02f, maxNorm: 0.2f, minNorm: 0.002f, nanCount: 0, infCount: 0);
            
            // Assert
            var summary = metrics.GetSummary();
            Assert.NotNull(summary.GradientHealthSummary);
            Assert.Equal(2, summary.GradientHealthSummary.HealthyGradientSteps);
            Assert.Equal(0, summary.GradientHealthSummary.TotalNanCount);
        }

        [Fact]
        public void GetBestValidationLoss_ReturnsMinimum()
        {
            // Arrange
            var metrics = new TrainingMetrics();
            
            // Act
            metrics.RecordValidationLoss(3.0f);
            metrics.RecordValidationLoss(2.5f);
            metrics.RecordValidationLoss(2.8f);
            
            // Assert
            Assert.Equal(2.5f, metrics.GetBestValidationLoss());
        }

        [Fact]
        public void IsTrainingProgressing_DetectsProgress()
        {
            // Arrange
            var metrics = new TrainingMetrics();
            
            // Act - add decreasing losses (good progress)
            for (int i = 0; i < 20; i++)
            {
                metrics.RecordTrainingLoss(5.0f - i * 0.1f);
            }
            
            // Assert
            Assert.True(metrics.IsTrainingProgressing(lookbackSteps: 5));
        }

        [Fact]
        public void IsTrainingProgressing_DetectsNoProgress()
        {
            // Arrange
            var metrics = new TrainingMetrics();
            
            // Act - add increasing losses (no progress)
            for (int i = 0; i < 20; i++)
            {
                metrics.RecordTrainingLoss(2.0f + i * 0.1f);
            }
            
            // Assert
            Assert.False(metrics.IsTrainingProgressing(lookbackSteps: 5));
        }

        [Fact]
        public void GetReport_GeneratesFormattedOutput()
        {
            // Arrange
            var metrics = new TrainingMetrics();
            metrics.RecordTrainingLoss(2.5f);
            metrics.RecordValidationLoss(2.8f);
            metrics.RecordTokenAccuracy(0.65f);
            
            // Act
            string report = metrics.GetReport();
            
            // Assert
            Assert.Contains("Training Metrics Report", report);
            Assert.Contains("Training Loss", report);
            Assert.Contains("Validation Loss", report);
            Assert.Contains("Perplexity", report);
            Assert.Contains("Token Prediction Accuracy", report);
        }

        [Fact]
        public void RecordTrainingLoss_IgnoresNaNAndInfinity()
        {
            // Arrange
            var metrics = new TrainingMetrics();
            
            // Act
            metrics.RecordTrainingLoss(2.0f);
            metrics.RecordTrainingLoss(float.NaN);
            metrics.RecordTrainingLoss(float.PositiveInfinity);
            metrics.RecordTrainingLoss(1.5f);
            
            // Assert
            var summary = metrics.GetSummary();
            Assert.Equal(2, summary.TrainingLossStats!.Count); // Only 2 valid values
        }
    }
}
