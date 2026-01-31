using System;
using System.Threading;
using Xunit;
using SmallMind.Core;

namespace SmallMind.Tests
{
    /// <summary>
    /// Unit tests for the PerformanceMetrics class.
    /// Verifies correct recording and aggregation of performance metrics.
    /// </summary>
    public class PerformanceMetricsTests
    {
        [Fact]
        public void RecordRequestStart_WhenEnabled_RecordsRequest()
        {
            // Arrange
            var metrics = new PerformanceMetrics();
            metrics.Start();

            // Act
            var requestId = metrics.RecordRequestStart();
            metrics.Stop();

            // Assert
            Assert.True(requestId >= 0);
            var summary = metrics.GetSummary();
            Assert.Equal(1, summary.TotalRequests);
        }

        [Fact]
        public void RecordRequestStart_WhenDisabled_ReturnsNegative()
        {
            // Arrange
            var metrics = new PerformanceMetrics();
            // Not starting metrics (disabled)

            // Act
            var requestId = metrics.RecordRequestStart();

            // Assert
            Assert.Equal(-1, requestId);
        }

        [Fact]
        public void RecordRequestComplete_TracksSuccessAndFailure()
        {
            // Arrange
            var metrics = new PerformanceMetrics();
            metrics.Start();

            // Act
            var req1 = metrics.RecordRequestStart();
            Thread.Sleep(10);
            metrics.RecordRequestComplete(req1, inputTokens: 10, outputTokens: 20, success: true);

            var req2 = metrics.RecordRequestStart();
            Thread.Sleep(10);
            metrics.RecordRequestComplete(req2, inputTokens: 15, outputTokens: 0, success: false, errorMessage: "Test error");

            metrics.Stop();
            var summary = metrics.GetSummary();

            // Assert
            Assert.Equal(2, summary.TotalRequests);
            Assert.Equal(1, summary.CompletedRequests);
            Assert.Equal(1, summary.FailedRequests);
            Assert.Equal(10, summary.TotalInputTokens);
            Assert.Equal(20, summary.TotalOutputTokens);
        }

        [Fact]
        public void GetSummary_CalculatesThroughput()
        {
            // Arrange
            var metrics = new PerformanceMetrics();
            metrics.Start();

            // Act - simulate 100 tokens in ~0.1 seconds
            var req1 = metrics.RecordRequestStart();
            Thread.Sleep(100);
            metrics.RecordRequestComplete(req1, inputTokens: 10, outputTokens: 100, success: true);
            
            metrics.Stop();
            var summary = metrics.GetSummary();

            // Assert
            Assert.Equal(1, summary.CompletedRequests);
            Assert.Equal(100, summary.TotalOutputTokens);
            Assert.True(summary.DurationSeconds > 0);
            Assert.True(summary.TokensPerSecond > 0);
            Assert.True(summary.RequestsPerSecond > 0);
        }

        [Fact]
        public void RecordFirstToken_CalculatesTTFT()
        {
            // Arrange
            var metrics = new PerformanceMetrics();
            metrics.Start();

            // Act
            var req1 = metrics.RecordRequestStart();
            Thread.Sleep(50);
            metrics.RecordFirstToken(req1);
            Thread.Sleep(50);
            metrics.RecordRequestComplete(req1, inputTokens: 10, outputTokens: 20, success: true);
            
            metrics.Stop();
            var summary = metrics.GetSummary();

            // Assert
            Assert.NotNull(summary.TtftStats);
            Assert.True(summary.TtftStats!.Mean >= 40); // At least 40ms (allowing for timing variance)
            Assert.True(summary.TtftStats.P50 >= 40);
        }

        [Fact]
        public void GetSummary_CalculatesLatencyStats()
        {
            // Arrange
            var metrics = new PerformanceMetrics();
            metrics.Start();

            // Act - create multiple requests with different latencies
            for (int i = 0; i < 5; i++)
            {
                var req = metrics.RecordRequestStart();
                Thread.Sleep(20 * (i + 1)); // 20, 40, 60, 80, 100 ms
                metrics.RecordRequestComplete(req, inputTokens: 10, outputTokens: 10 * (i + 1), success: true);
            }
            
            metrics.Stop();
            var summary = metrics.GetSummary();

            // Assert
            Assert.Equal(5, summary.CompletedRequests);
            Assert.NotNull(summary.E2eLatencyStats);
            Assert.True(summary.E2eLatencyStats!.Min > 0);
            Assert.True(summary.E2eLatencyStats.Max > summary.E2eLatencyStats.Min);
            Assert.True(summary.E2eLatencyStats.Mean > 0);
        }

        [Fact]
        public void GetSummary_CalculatesTokensPerRequestStats()
        {
            // Arrange
            var metrics = new PerformanceMetrics();
            metrics.Start();

            // Act
            var req1 = metrics.RecordRequestStart();
            metrics.RecordRequestComplete(req1, inputTokens: 10, outputTokens: 50, success: true);

            var req2 = metrics.RecordRequestStart();
            metrics.RecordRequestComplete(req2, inputTokens: 10, outputTokens: 100, success: true);

            var req3 = metrics.RecordRequestStart();
            metrics.RecordRequestComplete(req3, inputTokens: 10, outputTokens: 150, success: true);
            
            metrics.Stop();
            var summary = metrics.GetSummary();

            // Assert
            Assert.NotNull(summary.TokensPerRequestStats);
            Assert.Equal(50, summary.TokensPerRequestStats!.Min);
            Assert.Equal(150, summary.TokensPerRequestStats.Max);
            Assert.Equal(100, summary.TokensPerRequestStats.Mean);
        }

        [Fact]
        public void Reset_ClearsAllMetrics()
        {
            // Arrange
            var metrics = new PerformanceMetrics();
            metrics.Start();
            var req1 = metrics.RecordRequestStart();
            metrics.RecordRequestComplete(req1, inputTokens: 10, outputTokens: 20, success: true);

            // Act
            metrics.Reset();
            var summary = metrics.GetSummary();

            // Assert
            Assert.Equal(0, summary.TotalRequests);
            Assert.Equal(0, summary.CompletedRequests);
            Assert.Equal(0, summary.TotalOutputTokens);
        }

        [Fact]
        public void GetSummary_NoCompletedRequests_ReturnsBasicMetrics()
        {
            // Arrange
            var metrics = new PerformanceMetrics();
            metrics.Start();
            metrics.Stop();

            // Act
            var summary = metrics.GetSummary();

            // Assert
            Assert.Equal(0, summary.CompletedRequests);
            Assert.Null(summary.TtftStats);
            Assert.Null(summary.E2eLatencyStats);
        }
    }
}
