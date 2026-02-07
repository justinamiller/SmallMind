namespace SmallMind.Benchmarks;

/// <summary>
/// Result data for a benchmark scenario.
/// </summary>
public sealed class ScenarioResult
{
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, AggregatedStats> Aggregates { get; set; } = new();
    public RuntimeCounterMetrics? RuntimeCounters { get; set; }
    public MemoryMetrics? MemoryMetrics { get; set; }
    public GcMetrics? GcMetrics { get; set; }
    
    // Optional: raw samples for detailed analysis
    public Dictionary<string, double[]>? RawSamples { get; set; }
}

/// <summary>
/// Memory footprint metrics.
/// </summary>
public sealed class MemoryMetrics
{
    public long WorkingSetMinBytes { get; set; }
    public long WorkingSetMaxBytes { get; set; }
    public long WorkingSetAvgBytes { get; set; }
    public long PrivateMemoryMinBytes { get; set; }
    public long PrivateMemoryMaxBytes { get; set; }
    public long PrivateMemoryAvgBytes { get; set; }
    public long ManagedHeapMinBytes { get; set; }
    public long ManagedHeapMaxBytes { get; set; }
    public long ManagedHeapAvgBytes { get; set; }
    
    public double WorkingSetMinMB => WorkingSetMinBytes / (1024.0 * 1024.0);
    public double WorkingSetMaxMB => WorkingSetMaxBytes / (1024.0 * 1024.0);
    public double WorkingSetAvgMB => WorkingSetAvgBytes / (1024.0 * 1024.0);
    public double PrivateMemoryMinMB => PrivateMemoryMinBytes / (1024.0 * 1024.0);
    public double PrivateMemoryMaxMB => PrivateMemoryMaxBytes / (1024.0 * 1024.0);
    public double PrivateMemoryAvgMB => PrivateMemoryAvgBytes / (1024.0 * 1024.0);
    public double ManagedHeapMinMB => ManagedHeapMinBytes / (1024.0 * 1024.0);
    public double ManagedHeapMaxMB => ManagedHeapMaxBytes / (1024.0 * 1024.0);
    public double ManagedHeapAvgMB => ManagedHeapAvgBytes / (1024.0 * 1024.0);
}

/// <summary>
/// GC and allocation metrics.
/// </summary>
public sealed class GcMetrics
{
    public int Gen0Collections { get; set; }
    public int Gen1Collections { get; set; }
    public int Gen2Collections { get; set; }
    public long TotalAllocatedBytes { get; set; }
    public double AllocationsPerOperation { get; set; }
    
    public double TotalAllocatedMB => TotalAllocatedBytes / (1024.0 * 1024.0);
}

/// <summary>
/// Complete benchmark report.
/// </summary>
public sealed class BenchmarkReport
{
    public int SchemaVersion { get; set; } = 1;
    public string Timestamp { get; set; } = string.Empty;
    public string TimestampUtc { get; set; } = string.Empty;
    public EnvironmentMetadata Environment { get; set; } = new();
    public BenchmarkConfig RunConfig { get; set; } = new();
    public List<ScenarioResult> Scenarios { get; set; } = new();
}

/// <summary>
/// Memory snapshot at a point in time.
/// </summary>
public struct MemorySnapshot
{
    public long WorkingSet;
    public long PrivateMemory;
    public long ManagedHeap;
    
    public static MemorySnapshot Capture()
    {
        var process = System.Diagnostics.Process.GetCurrentProcess();
        process.Refresh();
        
        return new MemorySnapshot
        {
            WorkingSet = process.WorkingSet64,
            PrivateMemory = process.PrivateMemorySize64,
            ManagedHeap = GC.GetTotalMemory(false)
        };
    }
}
