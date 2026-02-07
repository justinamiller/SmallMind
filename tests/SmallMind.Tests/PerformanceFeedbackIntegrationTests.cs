using System;
using System.Collections.Generic;
using Xunit;
using SmallMind.Core;
using SmallMind.Core.Core;

namespace SmallMind.Tests
{
    /// <summary>
    /// Integration tests for the performance feedback system.
    /// Verifies end-to-end functionality of metrics collection and formatting.
    /// </summary>
    public class PerformanceFeedbackIntegrationTests
    {
        [Fact]
        public void MetricsFormatter_FormatText_ProducesExpectedOutput()
        {
            // Arrange
            var summary = CreateTestSummary();

            // Act
            var output = MetricsFormatter.FormatText(summary);

            // Assert
            Assert.Contains("Performance Summary", output);
            Assert.Contains("Concurrency: 1", output);
            Assert.Contains("Max tokens: 100", output);
            Assert.Contains("Throughput:", output);
            Assert.Contains("tok/s", output);
            Assert.Contains("Latency (ms):", output);
            Assert.Contains("TTFT:", output);
            Assert.Contains("E2E:", output);
        }

        [Fact]
        public void MetricsFormatter_FormatJson_ProducesValidJson()
        {
            // Arrange
            var summary = CreateTestSummary();

            // Act
            var jsonOutput = MetricsFormatter.FormatJson(summary);

            // Assert
            Assert.Contains("\"concurrency\":", jsonOutput);
            Assert.Contains("\"maxTokensRequested\":", jsonOutput);
            Assert.Contains("\"tokensPerSecond\":", jsonOutput);
            Assert.Contains("\"ttft\":", jsonOutput);
            Assert.Contains("\"e2eLatency\":", jsonOutput);
        }

        [Fact]
        public void MetricsFormatter_FormatBenchmarkTable_ProducesTable()
        {
            // Arrange
            var results = new[]
            {
                new BenchmarkResult
                {
                    Concurrency = 1,
                    MaxTokens = 64,
                    TokensPerSecond = 4.5,
                    RequestsPerSecond = 0.02,
                    DurationSeconds = 14.2,
                    CompletedRequests = 1,
                    TtftP95 = 890.0,
                    E2eP95 = 14200.0
                },
                new BenchmarkResult
                {
                    Concurrency = 1,
                    MaxTokens = 128,
                    TokensPerSecond = 4.3,
                    RequestsPerSecond = 0.01,
                    DurationSeconds = 29.8,
                    CompletedRequests = 1,
                    TtftP95 = 895.0,
                    E2eP95 = 29800.0
                }
            };

            // Act
            var output = MetricsFormatter.FormatBenchmarkTable(results);

            // Assert
            Assert.Contains("Detailed Results", output);
            Assert.Contains("Rank", output);
            Assert.Contains("Concurrency", output);
            Assert.Contains("Max Tokens", output);
            Assert.Contains("Tok/s", output);
        }

        [Fact]
        public void MetricsFormatter_FormatBestThroughput_ProducesCorrectFormat()
        {
            // Arrange
            var result = new BenchmarkResult
            {
                Concurrency = 1,
                MaxTokens = 64,
                TokensPerSecond = 4.52
            };

            // Act
            var output = MetricsFormatter.FormatBestThroughput(result);

            // Assert
            Assert.Contains("Best throughput:", output);
            Assert.Contains("4.52 tok/s", output);
            Assert.Contains("concurrency=1", output);
            Assert.Contains("max_tokens=64", output);
        }

        [Fact]
        public void BenchmarkResult_FromSummary_ConvertsCorrectly()
        {
            // Arrange
            var summary = CreateTestSummary();

            // Act
            var result = BenchmarkResult.FromSummary(summary);

            // Assert
            Assert.Equal(1, result.Concurrency);
            Assert.Equal(100, result.MaxTokens);
            Assert.Equal(summary.TokensPerSecond, result.TokensPerSecond);
            Assert.Equal(summary.RequestsPerSecond, result.RequestsPerSecond);
            Assert.Equal(summary.DurationSeconds, result.DurationSeconds);
            Assert.Equal(summary.CompletedRequests, result.CompletedRequests);
        }

        private MetricsSummary CreateTestSummary()
        {
            return new MetricsSummary
            {
                Concurrency = 1,
                MaxTokensRequested = 100,
                TotalRequests = 1,
                CompletedRequests = 1,
                FailedRequests = 0,
                DurationSeconds = 22.5,
                TotalInputTokens = 5,
                TotalOutputTokens = 100,
                TokensPerSecond = 4.44,
                RequestsPerSecond = 0.044,
                TtftStats = new PercentileStats(450, 450, 450, 450, 450, 450),
                E2eLatencyStats = new PercentileStats(22500, 22500, 22500, 22500, 22500, 22500),
                TokensPerRequestStats = new PercentileStats(100, 100, 100, 100, 100, 100)
            };
        }
    }
}
