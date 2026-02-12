using System.Diagnostics;
using SmallMind.Showcase.Core.Interfaces;
using SmallMind.Showcase.Core.Models;

namespace SmallMind.Showcase.Core.Services;

/// <summary>
/// Collects and tracks real-time generation metrics.
/// </summary>
public sealed class MetricsCollector : IMetricsCollector
{
    private readonly MetricsAggregator _aggregator = new(windowSize: 50);
    private readonly Stopwatch _requestStopwatch = new();
    private readonly Stopwatch _prefillStopwatch = new();
    private readonly Stopwatch _decodeStopwatch = new();
    
    private readonly object _lock = new();
    private GenerationMetrics _currentMetrics = new();
    
    private long _gcGen0Start;
    private long _gcGen1Start;
    private long _gcGen2Start;
    private long _memoryStart;

    public GenerationMetrics GetCurrentMetrics()
    {
        lock (_lock)
        {
            return _currentMetrics;
        }
    }

    public MetricsPercentiles GetPercentiles()
    {
        lock (_lock)
        {
            return _aggregator.GetPercentiles();
        }
    }

    public void OnRequestStart(int promptTokens, int contextWindowSize)
    {
        lock (_lock)
        {
            // Capture GC state before request
            _gcGen0Start = GC.CollectionCount(0);
            _gcGen1Start = GC.CollectionCount(1);
            _gcGen2Start = GC.CollectionCount(2);
            _memoryStart = GC.GetTotalMemory(false);

            _currentMetrics = new GenerationMetrics
            {
                PromptTokens = promptTokens,
                ContextWindowSize = contextWindowSize
            };

            _requestStopwatch.Restart();
            _prefillStopwatch.Restart();
        }
    }

    public void OnFirstToken()
    {
        lock (_lock)
        {
            _prefillStopwatch.Stop();
            _currentMetrics.TimeToFirstToken = _requestStopwatch.Elapsed;

            // Calculate prefill tok/s
            if (_prefillStopwatch.Elapsed.TotalSeconds > 0)
            {
                _currentMetrics.PrefillTokensPerSecond = 
                    _currentMetrics.PromptTokens / _prefillStopwatch.Elapsed.TotalSeconds;
            }

            _decodeStopwatch.Restart();
        }
    }

    public void OnTokenGenerated()
    {
        lock (_lock)
        {
            _currentMetrics.GeneratedTokens++;
        }
    }

    public void OnRequestComplete(int generatedTokens)
    {
        lock (_lock)
        {
            _requestStopwatch.Stop();
            _decodeStopwatch.Stop();

            _currentMetrics.GeneratedTokens = generatedTokens;
            _currentMetrics.EndToEndLatency = _requestStopwatch.Elapsed;

            // Calculate decode tok/s
            if (_decodeStopwatch.Elapsed.TotalSeconds > 0 && generatedTokens > 0)
            {
                _currentMetrics.DecodeTokensPerSecond = 
                    generatedTokens / _decodeStopwatch.Elapsed.TotalSeconds;
            }

            // Calculate per-token latency
            if (generatedTokens > 0)
            {
                _currentMetrics.PerTokenLatencyMs = 
                    _decodeStopwatch.Elapsed.TotalMilliseconds / generatedTokens;
            }

            // Capture GC stats
            _currentMetrics.Gen0Collections = (int)(GC.CollectionCount(0) - _gcGen0Start);
            _currentMetrics.Gen1Collections = (int)(GC.CollectionCount(1) - _gcGen1Start);
            _currentMetrics.Gen2Collections = (int)(GC.CollectionCount(2) - _gcGen2Start);
            
            // Memory stats
            _currentMetrics.ManagedHeapSizeBytes = GC.GetTotalMemory(false);
            _currentMetrics.TotalAllocatedBytes = _currentMetrics.ManagedHeapSizeBytes - _memoryStart;

            // Add to percentile aggregator
            if (_currentMetrics.EndToEndLatency.HasValue)
            {
                _aggregator.AddLatency(_currentMetrics.EndToEndLatency.Value.TotalMilliseconds);
            }

            // Best-effort CPU usage (using current process)
            try
            {
                using var process = Process.GetCurrentProcess();
                var cpuTime = process.TotalProcessorTime;
                var wallTime = _requestStopwatch.Elapsed;
                if (wallTime.TotalSeconds > 0)
                {
                    _currentMetrics.CpuUsagePercent = 
                        (cpuTime.TotalSeconds / wallTime.TotalSeconds) * 100.0 / Environment.ProcessorCount;
                }
            }
            catch
            {
                // CPU metrics are best-effort
            }
        }
    }

    public void RecordError(string error)
    {
        lock (_lock)
        {
            _currentMetrics.LastError = error;
        }
    }

    public void RecordWarning(string warning)
    {
        lock (_lock)
        {
            _currentMetrics.LastWarning = warning;
        }
    }

    public void Reset()
    {
        lock (_lock)
        {
            _requestStopwatch.Reset();
            _prefillStopwatch.Reset();
            _decodeStopwatch.Reset();
            _currentMetrics = new GenerationMetrics();
        }
    }
}
