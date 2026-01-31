using System.Collections.Generic;
using Xunit;
using TinyLLM.Core;

namespace TinyLLM.Tests
{
    /// <summary>
    /// Unit tests for the PercentileCalculator class.
    /// Verifies correct calculation of percentile statistics.
    /// </summary>
    public class PercentileCalculatorTests
    {
        [Fact]
        public void Percentile_EmptyList_ReturnsZero()
        {
            // Arrange
            var values = new List<double>();

            // Act
            var result = PercentileCalculator.Percentile(values, 50);

            // Assert
            Assert.Equal(0.0, result);
        }

        [Fact]
        public void Percentile_SingleValue_ReturnsThatValue()
        {
            // Arrange
            var values = new List<double> { 42.0 };

            // Act
            var p50 = PercentileCalculator.Percentile(values, 50);
            var p95 = PercentileCalculator.Percentile(values, 95);

            // Assert
            Assert.Equal(42.0, p50);
            Assert.Equal(42.0, p95);
        }

        [Fact]
        public void Percentile_SortedValues_CalculatesCorrectly()
        {
            // Arrange - values from 1 to 100
            var values = new List<double>();
            for (int i = 1; i <= 100; i++)
            {
                values.Add(i);
            }

            // Act
            var p50 = PercentileCalculator.Percentile(values, 50);
            var p95 = PercentileCalculator.Percentile(values, 95);
            var p99 = PercentileCalculator.Percentile(values, 99);

            // Assert
            Assert.Equal(50.5, p50, precision: 1);
            Assert.Equal(95.05, p95, precision: 1);
            Assert.Equal(99.01, p99, precision: 1);
        }

        [Fact]
        public void Percentile_UnsortedValues_CalculatesCorrectly()
        {
            // Arrange
            var values = new List<double> { 5, 1, 3, 2, 4 };

            // Act
            var p50 = PercentileCalculator.Percentile(values, 50);

            // Assert
            Assert.Equal(3.0, p50);
        }

        [Fact]
        public void CalculateStats_ReturnsCorrectStatistics()
        {
            // Arrange
            var values = new List<double> { 10, 20, 30, 40, 50, 60, 70, 80, 90, 100 };

            // Act
            var stats = PercentileCalculator.CalculateStats(values);

            // Assert
            Assert.Equal(10.0, stats.Min);
            Assert.Equal(100.0, stats.Max);
            Assert.Equal(55.0, stats.Mean);
            Assert.Equal(55.0, stats.P50, precision: 1);
            Assert.True(stats.P95 >= 90);
            Assert.True(stats.P99 >= 95);
        }

        [Fact]
        public void CalculateStats_EmptyList_ReturnsZeros()
        {
            // Arrange
            var values = new List<double>();

            // Act
            var stats = PercentileCalculator.CalculateStats(values);

            // Assert
            Assert.Equal(0.0, stats.Min);
            Assert.Equal(0.0, stats.Max);
            Assert.Equal(0.0, stats.Mean);
            Assert.Equal(0.0, stats.P50);
            Assert.Equal(0.0, stats.P95);
            Assert.Equal(0.0, stats.P99);
        }

        [Fact]
        public void CalculateStats_IdenticalValues_ReturnsSameValue()
        {
            // Arrange
            var values = new List<double> { 42, 42, 42, 42, 42 };

            // Act
            var stats = PercentileCalculator.CalculateStats(values);

            // Assert
            Assert.Equal(42.0, stats.Min);
            Assert.Equal(42.0, stats.Max);
            Assert.Equal(42.0, stats.Mean);
            Assert.Equal(42.0, stats.P50);
            Assert.Equal(42.0, stats.P95);
            Assert.Equal(42.0, stats.P99);
        }

        [Fact]
        public void Percentile_CornerCases_HandlesCorrectly()
        {
            // Arrange
            var values = new List<double> { 1, 2 };

            // Act
            var p0 = PercentileCalculator.Percentile(values, 0);
            var p100 = PercentileCalculator.Percentile(values, 100);

            // Assert
            Assert.Equal(1.0, p0);
            Assert.Equal(2.0, p100);
        }
    }
}
