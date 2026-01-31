using System;
using System.Text;
using System.Text.Json;
using TinyLLM.Core;

namespace TinyLLM.Core
{
    /// <summary>
    /// Formats performance metrics for output (text or JSON).
    /// </summary>
    public static class MetricsFormatter
    {
        /// <summary>
        /// Format metrics as human-readable text (llama-style output).
        /// </summary>
        public static string FormatText(MetricsSummary summary)
        {
            // Pre-size StringBuilder to reduce allocations
            var sb = new StringBuilder(512);
            sb.AppendLine();
            sb.AppendLine("=== Performance Summary ===");
            sb.AppendLine($"Concurrency: {summary.Concurrency}");
            sb.AppendLine($"Max tokens: {summary.MaxTokensRequested}");
            sb.AppendLine($"Duration: {summary.DurationSeconds:F2}s");
            sb.AppendLine($"Requests: total={summary.TotalRequests} completed={summary.CompletedRequests} failed={summary.FailedRequests}");
            sb.AppendLine($"Throughput: {summary.TokensPerSecond:F2} tok/s, {summary.RequestsPerSecond:F2} req/s");
            sb.AppendLine($"Tokens: input={summary.TotalInputTokens} output={summary.TotalOutputTokens}");
            sb.AppendLine();

            if (summary.TtftStats != null || summary.E2eLatencyStats != null)
            {
                sb.AppendLine("Latency (ms):");
                
                if (summary.TtftStats != null)
                {
                    sb.AppendLine($"  TTFT:  p50={summary.TtftStats.P50:F1}, p95={summary.TtftStats.P95:F1}, p99={summary.TtftStats.P99:F1}, mean={summary.TtftStats.Mean:F1}");
                }
                
                if (summary.E2eLatencyStats != null)
                {
                    sb.AppendLine($"  E2E:   p50={summary.E2eLatencyStats.P50:F1}, p95={summary.E2eLatencyStats.P95:F1}, p99={summary.E2eLatencyStats.P99:F1}, mean={summary.E2eLatencyStats.Mean:F1}");
                }

                if (summary.TokensPerRequestStats != null)
                {
                    sb.AppendLine();
                    sb.AppendLine($"Tokens/Request: p50={summary.TokensPerRequestStats.P50:F1}, p95={summary.TokensPerRequestStats.P95:F1}, p99={summary.TokensPerRequestStats.P99:F1}, mean={summary.TokensPerRequestStats.Mean:F1}");
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Format metrics as JSON.
        /// </summary>
        public static string FormatJson(MetricsSummary summary)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var jsonObject = new
            {
                concurrency = summary.Concurrency,
                maxTokensRequested = summary.MaxTokensRequested,
                totalRequests = summary.TotalRequests,
                completedRequests = summary.CompletedRequests,
                failedRequests = summary.FailedRequests,
                durationSeconds = summary.DurationSeconds,
                totalInputTokens = summary.TotalInputTokens,
                totalOutputTokens = summary.TotalOutputTokens,
                tokensPerSecond = summary.TokensPerSecond,
                requestsPerSecond = summary.RequestsPerSecond,
                ttft = summary.TtftStats != null ? new
                {
                    min = summary.TtftStats.Min,
                    mean = summary.TtftStats.Mean,
                    p50 = summary.TtftStats.P50,
                    p95 = summary.TtftStats.P95,
                    p99 = summary.TtftStats.P99,
                    max = summary.TtftStats.Max
                } : null,
                e2eLatency = summary.E2eLatencyStats != null ? new
                {
                    min = summary.E2eLatencyStats.Min,
                    mean = summary.E2eLatencyStats.Mean,
                    p50 = summary.E2eLatencyStats.P50,
                    p95 = summary.E2eLatencyStats.P95,
                    p99 = summary.E2eLatencyStats.P99,
                    max = summary.E2eLatencyStats.Max
                } : null,
                tokensPerRequest = summary.TokensPerRequestStats != null ? new
                {
                    min = summary.TokensPerRequestStats.Min,
                    mean = summary.TokensPerRequestStats.Mean,
                    p50 = summary.TokensPerRequestStats.P50,
                    p95 = summary.TokensPerRequestStats.P95,
                    p99 = summary.TokensPerRequestStats.P99,
                    max = summary.TokensPerRequestStats.Max
                } : null
            };

            return JsonSerializer.Serialize(jsonObject, options);
        }

        /// <summary>
        /// Format a benchmark results table.
        /// </summary>
        public static string FormatBenchmarkTable(BenchmarkResult[] results)
        {
            if (results == null || results.Length == 0)
            {
                return "No benchmark results available.";
            }

            // Pre-size StringBuilder: header (~170 chars) + ~135 chars per result
            var sb = new StringBuilder(170 + (results.Length * 135));
            sb.AppendLine();
            sb.AppendLine("=== Detailed Results (sorted by throughput) ===");
            sb.AppendLine();
            sb.AppendLine(string.Format("{0,-6} | {1,-12} | {2,-11} | {3,-10} | {4,-10} | {5,-10} | {6,-10} | {7,-10} | {8,-10}",
                "Rank", "Concurrency", "Max Tokens", "Tok/s", "Req/s", "Duration", "Requests", "TTFT p95", "E2E p95"));
            sb.AppendLine(new string('-', 120));

            for (int i = 0; i < results.Length; i++)
            {
                var r = results[i];
                sb.AppendLine(string.Format("{0,-6} | {1,-12} | {2,-11} | {3,-10:F2} | {4,-10:F2} | {5,-10:F2}s | {6,-10} | {7,-10:F1}ms | {8,-10:F1}ms",
                    i + 1,
                    r.Concurrency,
                    r.MaxTokens,
                    r.TokensPerSecond,
                    r.RequestsPerSecond,
                    r.DurationSeconds,
                    r.CompletedRequests,
                    r.TtftP95,
                    r.E2eP95));
            }

            return sb.ToString();
        }

        /// <summary>
        /// Format the "Best throughput" summary line.
        /// </summary>
        public static string FormatBestThroughput(BenchmarkResult best)
        {
            return $"Best throughput: {best.TokensPerSecond:F2} tok/s (concurrency={best.Concurrency}, max_tokens={best.MaxTokens})";
        }
    }

    /// <summary>
    /// Result from a single benchmark run configuration.
    /// </summary>
    public class BenchmarkResult
    {
        public int Concurrency { get; set; }
        public int MaxTokens { get; set; }
        public double TokensPerSecond { get; set; }
        public double RequestsPerSecond { get; set; }
        public double DurationSeconds { get; set; }
        public int CompletedRequests { get; set; }
        public double TtftP95 { get; set; }
        public double E2eP95 { get; set; }

        public static BenchmarkResult FromSummary(MetricsSummary summary)
        {
            return new BenchmarkResult
            {
                Concurrency = summary.Concurrency,
                MaxTokens = summary.MaxTokensRequested,
                TokensPerSecond = summary.TokensPerSecond,
                RequestsPerSecond = summary.RequestsPerSecond,
                DurationSeconds = summary.DurationSeconds,
                CompletedRequests = summary.CompletedRequests,
                TtftP95 = summary.TtftStats?.P95 ?? 0,
                E2eP95 = summary.E2eLatencyStats?.P95 ?? 0
            };
        }
    }
}
