using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace SmallMind.Core.Core
{
    /// <summary>
    /// Thread-safe metrics collector for tracking LLM performance.
    /// Records per-request timing and token counts for benchmarking.
    /// </summary>
    internal sealed class PerformanceMetrics
    {
        private readonly object _lock = new object();
        // Pre-size for typical benchmark scenarios (hundreds of requests)
        private readonly List<RequestMetrics> _requests = new List<RequestMetrics>(capacity: 512);
        private readonly Stopwatch _overallTimer = new Stopwatch();
        private int _maxConcurrency = 0;
        private int _currentConcurrency = 0;
        private volatile bool _isEnabled = false;

        /// <summary>
        /// Enable or disable metrics collection.
        /// When disabled, recording has minimal overhead.
        /// </summary>
        public bool IsEnabled
        {
            get { return _isEnabled; }
            set { _isEnabled = value; }
        }

        /// <summary>
        /// Start the overall timer for the benchmark run.
        /// </summary>
        public void Start()
        {
            lock (_lock)
            {
                _overallTimer.Restart();
                _isEnabled = true;
            }
        }

        /// <summary>
        /// Stop the overall timer.
        /// </summary>
        public void Stop()
        {
            lock (_lock)
            {
                _overallTimer.Stop();
            }
        }

        /// <summary>
        /// Record the start of a request.
        /// Returns a request ID for tracking.
        /// </summary>
        public int RecordRequestStart()
        {
            if (!IsEnabled) return -1;

            lock (_lock)
            {
                _currentConcurrency++;
                if (_currentConcurrency > _maxConcurrency)
                {
                    _maxConcurrency = _currentConcurrency;
                }

                var request = new RequestMetrics
                {
                    RequestId = _requests.Count,
                    StartTime = _overallTimer.Elapsed
                };
                _requests.Add(request);
                return request.RequestId;
            }
        }

        /// <summary>
        /// Record when inference starts (after queue time).
        /// </summary>
        public void RecordInferenceStart(int requestId)
        {
            if (!IsEnabled || requestId < 0) return;

            lock (_lock)
            {
                if (requestId < _requests.Count)
                {
                    _requests[requestId].InferenceStartTime = _overallTimer.Elapsed;
                }
            }
        }

        /// <summary>
        /// Record the first token emitted.
        /// </summary>
        public void RecordFirstToken(int requestId)
        {
            if (!IsEnabled || requestId < 0) return;

            lock (_lock)
            {
                if (requestId < _requests.Count)
                {
                    _requests[requestId].FirstTokenTime = _overallTimer.Elapsed;
                }
            }
        }

        /// <summary>
        /// Record request completion.
        /// </summary>
        public void RecordRequestComplete(int requestId, int inputTokens, int outputTokens, bool success = true, string? errorMessage = null)
        {
            if (!IsEnabled || requestId < 0) return;

            lock (_lock)
            {
                _currentConcurrency--;
                
                if (requestId < _requests.Count)
                {
                    var request = _requests[requestId];
                    request.EndTime = _overallTimer.Elapsed;
                    request.InputTokens = inputTokens;
                    request.OutputTokens = outputTokens;
                    request.Success = success;
                    request.ErrorMessage = errorMessage;
                }
            }
        }

        /// <summary>
        /// Get a summary of all collected metrics.
        /// Optimized to avoid LINQ allocations.
        /// </summary>
        public MetricsSummary GetSummary(int maxTokensRequested = 0, int concurrencyLevel = 1)
        {
            lock (_lock)
            {
                // Manual filtering instead of LINQ to avoid allocations
                // Pre-size based on total requests (most will be completed)
                var completedRequests = new List<RequestMetrics>(capacity: _requests.Count);
                var failedRequests = new List<RequestMetrics>(capacity: Math.Max(8, _requests.Count / 10));
                
                for (int i = 0; i < _requests.Count; i++)
                {
                    var r = _requests[i];
                    if (r.Success && r.EndTime.HasValue)
                    {
                        completedRequests.Add(r);
                    }
                    else if (!r.Success)
                    {
                        failedRequests.Add(r);
                    }
                }

                var summary = new MetricsSummary
                {
                    Concurrency = Math.Max(concurrencyLevel, _maxConcurrency),
                    MaxTokensRequested = maxTokensRequested,
                    TotalRequests = _requests.Count,
                    CompletedRequests = completedRequests.Count,
                    FailedRequests = failedRequests.Count,
                    DurationSeconds = _overallTimer.Elapsed.TotalSeconds
                };

                if (completedRequests.Count == 0)
                {
                    return summary;
                }

                // Token statistics - manual sum instead of LINQ
                long totalInputTokens = 0;
                long totalOutputTokens = 0;
                for (int i = 0; i < completedRequests.Count; i++)
                {
                    totalInputTokens += completedRequests[i].InputTokens;
                    totalOutputTokens += completedRequests[i].OutputTokens;
                }
                summary.TotalInputTokens = totalInputTokens;
                summary.TotalOutputTokens = totalOutputTokens;

                // Throughput
                if (summary.DurationSeconds > 0)
                {
                    summary.TokensPerSecond = summary.TotalOutputTokens / summary.DurationSeconds;
                    summary.RequestsPerSecond = completedRequests.Count / summary.DurationSeconds;
                }

                // TTFT statistics (Time To First Token) - in milliseconds
                var ttftValues = new List<double>(completedRequests.Count);
                foreach (var req in completedRequests)
                {
                    if (req.FirstTokenTime.HasValue)
                    {
                        double ttft = (req.FirstTokenTime.Value - req.StartTime).TotalMilliseconds;
                        ttftValues.Add(ttft);
                    }
                }
                if (ttftValues.Count > 0)
                {
                    summary.TtftStats = PercentileCalculator.CalculateStats(ttftValues);
                }

                // End-to-end latency statistics - in milliseconds
                var e2eValues = new List<double>(completedRequests.Count);
                foreach (var req in completedRequests)
                {
                    if (req.EndTime.HasValue)
                    {
                        double latency = (req.EndTime.Value - req.StartTime).TotalMilliseconds;
                        e2eValues.Add(latency);
                    }
                }
                if (e2eValues.Count > 0)
                {
                    summary.E2eLatencyStats = PercentileCalculator.CalculateStats(e2eValues);
                }

                // Tokens per request statistics - manual conversion
                var tokensPerReq = new List<double>(completedRequests.Count);
                for (int i = 0; i < completedRequests.Count; i++)
                {
                    tokensPerReq.Add((double)completedRequests[i].OutputTokens);
                }
                if (tokensPerReq.Count > 0)
                {
                    summary.TokensPerRequestStats = PercentileCalculator.CalculateStats(tokensPerReq);
                }

                return summary;
            }
        }

        /// <summary>
        /// Reset all metrics.
        /// </summary>
        public void Reset()
        {
            lock (_lock)
            {
                _requests.Clear();
                _overallTimer.Reset();
                _maxConcurrency = 0;
                _currentConcurrency = 0;
            }
        }

        /// <summary>
        /// Per-request metrics.
        /// </summary>
        private class RequestMetrics
        {
            public int RequestId { get; set; }
            public TimeSpan StartTime { get; set; }
            public TimeSpan? InferenceStartTime { get; set; }
            public TimeSpan? FirstTokenTime { get; set; }
            public TimeSpan? EndTime { get; set; }
            public int InputTokens { get; set; }
            public int OutputTokens { get; set; }
            public bool Success { get; set; } = true;
            public string? ErrorMessage { get; set; }
        }
    }

    /// <summary>
    /// Summary of performance metrics.
    /// </summary>
    internal sealed class MetricsSummary
    {
        // Capacity metrics
        public int Concurrency { get; set; }
        public int MaxTokensRequested { get; set; }
        public int TotalRequests { get; set; }
        public int CompletedRequests { get; set; }
        public int FailedRequests { get; set; }
        public double DurationSeconds { get; set; }
        public long TotalInputTokens { get; set; }
        public long TotalOutputTokens { get; set; }
        public double TokensPerSecond { get; set; }
        public double RequestsPerSecond { get; set; }

        // Latency metrics
        public PercentileStats? TtftStats { get; set; }
        public PercentileStats? E2eLatencyStats { get; set; }
        public PercentileStats? TokensPerRequestStats { get; set; }
    }
}
