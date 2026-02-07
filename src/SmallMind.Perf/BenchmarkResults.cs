using System.Diagnostics;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Numerics;
using System.Text.Json;

namespace SmallMind.Perf;

/// <summary>
/// Collects system and environment information for benchmarks.
/// </summary>
public sealed class SystemInfo
{
    public string OS { get; set; } = "";
    public string Architecture { get; set; } = "";
    public string Framework { get; set; } = "";
    public int ProcessorCount { get; set; }
    public string GCMode { get; set; } = "";
    public string GCLatencyMode { get; set; } = "";
    public int VectorFloatCount { get; set; }
    public bool VectorHardwareAccelerated { get; set; }
    public bool Avx2Supported { get; set; }
    public bool Avx512FSupported { get; set; }
    
    public static SystemInfo Collect()
    {
        var info = new SystemInfo
        {
            OS = RuntimeInformation.OSDescription,
            Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            Framework = RuntimeInformation.FrameworkDescription,
            ProcessorCount = Environment.ProcessorCount,
            GCMode = GCSettings.IsServerGC ? "Server" : "Workstation",
            GCLatencyMode = GCSettings.LatencyMode.ToString(),
            VectorFloatCount = Vector<float>.Count,
            VectorHardwareAccelerated = Vector.IsHardwareAccelerated
        };

#if NET7_0_OR_GREATER
        info.Avx2Supported = System.Runtime.Intrinsics.X86.Avx2.IsSupported;
        info.Avx512FSupported = System.Runtime.Intrinsics.X86.Avx512F.IsSupported;
#endif

        return info;
    }

    public void Print()
    {
        Console.WriteLine("=== System Information ===");
        Console.WriteLine($"OS: {OS}");
        Console.WriteLine($"Architecture: {Architecture}");
        Console.WriteLine($"Framework: {Framework}");
        Console.WriteLine($"Processor Count: {ProcessorCount}");
        Console.WriteLine($"GC Mode: {GCMode}");
        Console.WriteLine($"GC Latency Mode: {GCLatencyMode}");
        Console.WriteLine($"Vector<float>.Count: {VectorFloatCount}");
        Console.WriteLine($"Vector Hardware Accelerated: {VectorHardwareAccelerated}");
        Console.WriteLine($"AVX2 Supported: {Avx2Supported}");
        Console.WriteLine($"AVX-512F Supported: {Avx512FSupported}");
        Console.WriteLine();
    }
}

/// <summary>
/// Metrics for a single benchmark run.
/// </summary>
public sealed class BenchmarkMetrics
{
    public string Name { get; set; } = "";
    public double TimeMs { get; set; }
    public double CpuTimeMs { get; set; }
    public long AllocatedBytes { get; set; }
    public int Gen0Collections { get; set; }
    public int Gen1Collections { get; set; }
    public int Gen2Collections { get; set; }
    public Dictionary<string, object> CustomMetrics { get; set; } = new();
}

/// <summary>
/// Results for a complete benchmark suite.
/// </summary>
public sealed class BenchmarkResults
{
    public SystemInfo System { get; set; } = new();
    public List<BenchmarkMetrics> Benchmarks { get; set; } = new();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    public void PrintJson()
    {
        var options = new JsonSerializerOptions 
        { 
            WriteIndented = true 
        };
        Console.WriteLine(JsonSerializer.Serialize(this, options));
    }
    
    public void PrintText()
    {
        System.Print();
        
        Console.WriteLine("=== Benchmark Results ===");
        foreach (var bench in Benchmarks)
        {
            Console.WriteLine($"\n{bench.Name}:");
            Console.WriteLine($"  Time: {bench.TimeMs:F3} ms");
            Console.WriteLine($"  CPU Time: {bench.CpuTimeMs:F3} ms");
            Console.WriteLine($"  Allocated: {FormatBytes(bench.AllocatedBytes)}");
            Console.WriteLine($"  GC: Gen0={bench.Gen0Collections}, Gen1={bench.Gen1Collections}, Gen2={bench.Gen2Collections}");
            
            foreach (var (key, value) in bench.CustomMetrics)
            {
                Console.WriteLine($"  {key}: {value}");
            }
        }
    }
    
    private static string FormatBytes(long bytes)
    {
        if (bytes == 0) return "0 B";
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F2} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F2} MB";
        return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
    }
}
