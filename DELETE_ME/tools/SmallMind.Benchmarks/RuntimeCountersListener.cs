using System.Diagnostics.Tracing;

namespace SmallMind.Benchmarks;

/// <summary>
/// Listener for .NET runtime event counters.
/// Captures performance metrics from "System.Runtime" event source.
/// </summary>
public sealed class RuntimeCountersListener : EventListener
{
    private readonly object _lock = new();
    private readonly Dictionary<string, double> _latestValues = new();
    private readonly Dictionary<string, List<double>> _samples = new();
    private bool _disposed;
    
    protected override void OnEventSourceCreated(EventSource eventSource)
    {
        if (eventSource.Name == "System.Runtime")
        {
            // Enable counters with 1 second interval
            var args = new Dictionary<string, string?>
            {
                ["EventCounterIntervalSec"] = "1"
            };
            EnableEvents(eventSource, EventLevel.Informational, EventKeywords.All, args);
        }
    }
    
    protected override void OnEventWritten(EventWrittenEventArgs eventData)
    {
        if (_disposed || eventData.Payload == null)
            return;
            
        try
        {
            // Event counters have a specific payload structure
            for (int i = 0; i < eventData.Payload.Count; i++)
            {
                if (eventData.Payload[i] is IDictionary<string, object> payloadDict)
                {
                    if (payloadDict.TryGetValue("Name", out object? nameObj) && nameObj is string name)
                    {
                        object? value = null;
                        
                        // Try to get Mean (for EventCounters) or Increment (for IncrementingEventCounters)
                        if (payloadDict.TryGetValue("Mean", out object? meanVal))
                        {
                            value = meanVal;
                        }
                        else if (payloadDict.TryGetValue("Increment", out object? incrementVal))
                        {
                            value = incrementVal;
                        }
                        
                        if (value != null)
                        {
                            double numericValue = Convert.ToDouble(value);
                            
                            lock (_lock)
                            {
                                _latestValues[name] = numericValue;
                                
                                if (!_samples.ContainsKey(name))
                                {
                                    _samples[name] = new List<double>();
                                }
                                _samples[name].Add(numericValue);
                            }
                        }
                    }
                }
            }
        }
        catch
        {
            // Gracefully degrade - counter parsing can vary by runtime version
        }
    }
    
    /// <summary>
    /// Get the latest value for a counter.
    /// </summary>
    public bool TryGetLatestValue(string counterName, out double value)
    {
        lock (_lock)
        {
            return _latestValues.TryGetValue(counterName, out value);
        }
    }
    
    /// <summary>
    /// Get all samples for a counter.
    /// </summary>
    public double[] GetSamples(string counterName)
    {
        lock (_lock)
        {
            if (_samples.TryGetValue(counterName, out var samples))
            {
                return samples.ToArray();
            }
            return Array.Empty<double>();
        }
    }
    
    /// <summary>
    /// Get average value for a counter.
    /// </summary>
    public double GetAverage(string counterName)
    {
        var samples = GetSamples(counterName);
        if (samples.Length == 0)
            return 0.0;
            
        double sum = 0.0;
        for (int i = 0; i < samples.Length; i++)
        {
            sum += samples[i];
        }
        return sum / samples.Length;
    }
    
    /// <summary>
    /// Get peak (max) value for a counter.
    /// </summary>
    public double GetPeak(string counterName)
    {
        var samples = GetSamples(counterName);
        if (samples.Length == 0)
            return 0.0;
            
        return Statistics.Max(samples);
    }
    
    /// <summary>
    /// Reset all collected samples.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _latestValues.Clear();
            _samples.Clear();
        }
    }
    
    /// <summary>
    /// Get all available counter names.
    /// </summary>
    public string[] GetAvailableCounters()
    {
        lock (_lock)
        {
            return _latestValues.Keys.ToArray();
        }
    }
    
    public new void Dispose()
    {
        _disposed = true;
        base.Dispose();
    }
}

/// <summary>
/// Aggregated runtime counter metrics.
/// </summary>
public sealed class RuntimeCounterMetrics
{
    public double CpuUsageAvg { get; set; }
    public double CpuUsagePeak { get; set; }
    public double WorkingSetAvgMB { get; set; }
    public double WorkingSetPeakMB { get; set; }
    public double GcHeapSizeAvgMB { get; set; }
    public double GcHeapSizePeakMB { get; set; }
    public double AllocRateAvgMBPerSec { get; set; }
    public double AllocRatePeakMBPerSec { get; set; }
    public int Gen0GcCount { get; set; }
    public int Gen1GcCount { get; set; }
    public int Gen2GcCount { get; set; }
    public double TimeInGcPercent { get; set; }
    public double ThreadPoolThreadCountAvg { get; set; }
    public double LockContentionCountAvg { get; set; }
    
    public static RuntimeCounterMetrics Capture(RuntimeCountersListener listener)
    {
        var metrics = new RuntimeCounterMetrics();
        
        // CPU usage
        metrics.CpuUsageAvg = listener.GetAverage("cpu-usage");
        metrics.CpuUsagePeak = listener.GetPeak("cpu-usage");
        
        // Working set (convert bytes to MB)
        double workingSetAvg = listener.GetAverage("working-set");
        double workingSetPeak = listener.GetPeak("working-set");
        metrics.WorkingSetAvgMB = workingSetAvg / (1024.0 * 1024.0);
        metrics.WorkingSetPeakMB = workingSetPeak / (1024.0 * 1024.0);
        
        // GC heap size (convert bytes to MB)
        double gcHeapAvg = listener.GetAverage("gc-heap-size");
        double gcHeapPeak = listener.GetPeak("gc-heap-size");
        metrics.GcHeapSizeAvgMB = gcHeapAvg / (1024.0 * 1024.0);
        metrics.GcHeapSizePeakMB = gcHeapPeak / (1024.0 * 1024.0);
        
        // Allocation rate (convert bytes to MB/sec)
        double allocRateAvg = listener.GetAverage("alloc-rate");
        double allocRatePeak = listener.GetPeak("alloc-rate");
        metrics.AllocRateAvgMBPerSec = allocRateAvg / (1024.0 * 1024.0);
        metrics.AllocRatePeakMBPerSec = allocRatePeak / (1024.0 * 1024.0);
        
        // GC counts (use latest value)
        listener.TryGetLatestValue("gen-0-gc-count", out double gen0);
        listener.TryGetLatestValue("gen-1-gc-count", out double gen1);
        listener.TryGetLatestValue("gen-2-gc-count", out double gen2);
        metrics.Gen0GcCount = (int)gen0;
        metrics.Gen1GcCount = (int)gen1;
        metrics.Gen2GcCount = (int)gen2;
        
        // Time in GC
        metrics.TimeInGcPercent = listener.GetAverage("time-in-gc");
        
        // ThreadPool
        metrics.ThreadPoolThreadCountAvg = listener.GetAverage("threadpool-thread-count");
        
        // Lock contention
        metrics.LockContentionCountAvg = listener.GetAverage("monitor-lock-contention-count");
        
        return metrics;
    }
}
