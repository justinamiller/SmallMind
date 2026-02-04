using System;
using SmallMind.Core.Core;
using Xunit;

namespace SmallMind.Tests.Core
{
    public class StrictMemoryBudgetTests
    {
        [Fact]
        public void Constructor_WithValidParameters_Succeeds()
        {
            // Arrange & Act
            var budget = new StrictMemoryBudget(
                maxBytesHard: 1024 * 1024 * 1024, // 1GB
                maxBytesPerSession: 512 * 1024 * 1024, // 512MB
                rejectOnExceed: true,
                preAllocate: true,
                safetyMargin: 0.1);

            // Assert
            Assert.NotNull(budget);
            Assert.Equal(1024 * 1024 * 1024, budget.MaxBytesHard);
            Assert.Equal(512 * 1024 * 1024, budget.MaxBytesPerSession);
            Assert.True(budget.RejectOnExceed);
            Assert.True(budget.PreAllocate);
            Assert.Equal(0.1, budget.SafetyMargin);
        }

        [Fact]
        public void EffectiveLimits_ApplySafetyMargin()
        {
            // Arrange
            var budget = new StrictMemoryBudget(
                maxBytesHard: 1000,
                maxBytesPerSession: 500,
                safetyMargin: 0.1); // 10% margin

            // Act
            var effectiveHard = budget.EffectiveHardLimit;
            var effectiveSession = budget.EffectiveSessionLimit;

            // Assert
            Assert.Equal(900, effectiveHard); // 1000 * 0.9
            Assert.Equal(450, effectiveSession); // 500 * 0.9
        }

        [Fact]
        public void CanAllocate_WithinBudget_ReturnsTrue()
        {
            // Arrange
            var budget = new StrictMemoryBudget(
                maxBytesHard: 1000,
                safetyMargin: 0.1);

            // Act
            var result = budget.CanAllocate(800); // 800 < 900 (effective limit)

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void CanAllocate_ExceedingBudget_ReturnsFalse()
        {
            // Arrange
            var budget = new StrictMemoryBudget(
                maxBytesHard: 1000,
                safetyMargin: 0.1);

            // Act
            var result = budget.CanAllocate(1000); // 1000 > 900 (effective limit)

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void CheckBudget_WithinLimit_ReturnsSuccess()
        {
            // Arrange
            var budget = new StrictMemoryBudget(
                maxBytesHard: 10000,
                safetyMargin: 0.1);

            var breakdown = new MemoryBreakdown(
                modelParametersBytes: 1000,
                activationsBytes: 500,
                kvCacheBytes: 300,
                gradientsBytes: 0,
                optimizerStateBytes: 0,
                overheadBytes: 100);

            // Act
            var result = budget.CheckBudget(breakdown);

            // Assert
            Assert.True(result.CanProceed);
            Assert.Null(result.FailureReason);
        }

        [Fact]
        public void CheckBudget_ExceedingLimit_ReturnsFailure()
        {
            // Arrange
            var budget = new StrictMemoryBudget(
                maxBytesHard: 1000,
                safetyMargin: 0.1);

            var breakdown = new MemoryBreakdown(
                modelParametersBytes: 500,
                activationsBytes: 500,
                kvCacheBytes: 100,
                gradientsBytes: 0,
                optimizerStateBytes: 0,
                overheadBytes: 0);

            // Act
            var result = budget.CheckBudget(breakdown);

            // Assert
            Assert.False(result.CanProceed);
            Assert.NotNull(result.FailureReason);
            Assert.Contains("exceeds", result.FailureReason);
        }

        [Fact]
        public void GetSummary_ReturnsFormattedString()
        {
            // Arrange
            var budget = new StrictMemoryBudget(
                maxBytesHard: 1024 * 1024 * 1024,
                maxBytesPerSession: 512 * 1024 * 1024);

            // Act
            var summary = budget.GetSummary();

            // Assert
            Assert.Contains("Strict Memory Budget", summary);
            Assert.Contains("Hard Limit", summary);
            Assert.Contains("Session Limit", summary);
        }
    }

    public class BudgetCheckResultTests
    {
        [Fact]
        public void Success_CreatesSuccessfulResult()
        {
            // Arrange
            var breakdown = new MemoryBreakdown(
                modelParametersBytes: 1000,
                activationsBytes: 500,
                kvCacheBytes: 0,
                gradientsBytes: 0,
                optimizerStateBytes: 0,
                overheadBytes: 0);

            // Act
            var result = BudgetCheckResult.Success(1500, 10000, breakdown);

            // Assert
            Assert.True(result.CanProceed);
            Assert.Equal(1500, result.EstimatedMemoryBytes);
            Assert.Equal(10000, result.AvailableBudgetBytes);
            Assert.Null(result.FailureReason);
        }

        [Fact]
        public void Failure_CreatesFailedResult()
        {
            // Arrange
            var breakdown = new MemoryBreakdown(
                modelParametersBytes: 10000,
                activationsBytes: 5000,
                kvCacheBytes: 0,
                gradientsBytes: 0,
                optimizerStateBytes: 0,
                overheadBytes: 0);

            // Act
            var result = BudgetCheckResult.Failure(
                15000,
                10000,
                "Budget exceeded",
                breakdown);

            // Assert
            Assert.False(result.CanProceed);
            Assert.Equal(15000, result.EstimatedMemoryBytes);
            Assert.Equal(10000, result.AvailableBudgetBytes);
            Assert.Equal("Budget exceeded", result.FailureReason);
        }

        [Fact]
        public void GetSummary_ReturnsFormattedString()
        {
            // Arrange
            var breakdown = new MemoryBreakdown(
                modelParametersBytes: 1000,
                activationsBytes: 500,
                kvCacheBytes: 0,
                gradientsBytes: 0,
                optimizerStateBytes: 0,
                overheadBytes: 0);
            var result = BudgetCheckResult.Success(1500, 10000, breakdown);

            // Act
            var summary = result.GetSummary();

            // Assert
            Assert.Contains("PASS", summary);
            Assert.Contains("MB", summary);
        }
    }

    public class MemoryBreakdownTests
    {
        [Fact]
        public void TotalBytes_SumsAllComponents()
        {
            // Arrange
            var breakdown = new MemoryBreakdown(
                modelParametersBytes: 1000,
                activationsBytes: 500,
                kvCacheBytes: 300,
                gradientsBytes: 200,
                optimizerStateBytes: 400,
                overheadBytes: 100);

            // Act
            var total = breakdown.TotalBytes;

            // Assert
            Assert.Equal(2500, total);
        }

        [Fact]
        public void GetSummary_ReturnsFormattedBreakdown()
        {
            // Arrange
            var breakdown = new MemoryBreakdown(
                modelParametersBytes: 1024 * 1024,
                activationsBytes: 512 * 1024,
                kvCacheBytes: 0,
                gradientsBytes: 0,
                optimizerStateBytes: 0,
                overheadBytes: 0);

            // Act
            var summary = breakdown.GetSummary();

            // Assert
            Assert.Contains("Memory Breakdown", summary);
            Assert.Contains("Model Parameters", summary);
            Assert.Contains("Activations", summary);
        }
    }
}
