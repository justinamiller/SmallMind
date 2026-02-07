using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SmallMind.Benchmarks;

/// <summary>
/// Generates Markdown reports.
/// </summary>
public static class MarkdownReportGenerator
{
    public static string Generate(BenchmarkReport report)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("# SmallMind Benchmark Results");
        sb.AppendLine();
        sb.AppendLine($"**Generated:** {report.Timestamp}");
        sb.AppendLine($"**Generated (UTC):** {report.TimestampUtc}");
        sb.AppendLine();
        
        // Environment metadata
        AppendEnvironmentSection(sb, report.Environment);
        
        // Run configuration
        AppendRunConfigSection(sb, report.RunConfig);
        
        // Scenarios
        AppendScenariosSection(sb, report.Scenarios);
        
        return sb.ToString();
    }
    
    private static void AppendEnvironmentSection(StringBuilder sb, EnvironmentMetadata env)
    {
        sb.AppendLine("## Environment");
        sb.AppendLine();
        
        sb.AppendLine("### Hardware & OS");
        sb.AppendLine("| Property | Value |");
        sb.AppendLine("|----------|-------|");
        sb.AppendLine($"| OS | {env.OsDescription} |");
        sb.AppendLine($"| OS Version | {env.OsVersion} |");
        sb.AppendLine($"| Architecture | {env.ProcessArchitecture} |");
        sb.AppendLine($"| CPU Cores | {env.ProcessorCount} |");
        sb.AppendLine();
        
        sb.AppendLine("### .NET Runtime");
        sb.AppendLine("| Property | Value |");
        sb.AppendLine("|----------|-------|");
        sb.AppendLine($"| .NET Version | {env.DotNetVersion} |");
        sb.AppendLine($"| Runtime | {env.RuntimeDescription} |");
        sb.AppendLine($"| Build Config | {env.BuildConfiguration} |");
        sb.AppendLine();
        
        sb.AppendLine("### Engine Configuration");
        sb.AppendLine("| Property | Value |");
        sb.AppendLine("|----------|-------|");
        sb.AppendLine($"| Threads | {env.EngineThreads} |");
        sb.AppendLine($"| Repo Commit | {env.RepoCommitHash} |");
        sb.AppendLine();
    }
    
    private static void AppendRunConfigSection(StringBuilder sb, BenchmarkConfig config)
    {
        sb.AppendLine("## Run Configuration");
        sb.AppendLine();
        sb.AppendLine("| Parameter | Value |");
        sb.AppendLine("|-----------|-------|");
        sb.AppendLine($"| Model Path | `{config.ModelPath}` |");
        sb.AppendLine($"| Scenario | {config.Scenario} |");
        sb.AppendLine($"| Iterations | {config.Iterations} |");
        sb.AppendLine($"| Warmup | {config.Warmup} |");
        sb.AppendLine($"| Max New Tokens | {config.MaxNewTokens} |");
        sb.AppendLine($"| Prompt Profile | {config.PromptProfile} |");
        sb.AppendLine($"| Temperature | {config.Temperature} |");
        sb.AppendLine($"| Top-K | {config.TopK} |");
        sb.AppendLine($"| Top-P | {config.TopP} |");
        sb.AppendLine($"| Seed | {config.Seed} |");
        sb.AppendLine($"| Cold Start | {config.ColdStart} |");
        sb.AppendLine();
    }
    
    private static void AppendScenariosSection(StringBuilder sb, List<ScenarioResult> scenarios)
    {
        sb.AppendLine("## Benchmark Results");
        sb.AppendLine();
        
        foreach (var scenario in scenarios)
        {
            sb.AppendLine($"### {scenario.Name}");
            sb.AppendLine();
            
            // Aggregated metrics
            if (scenario.Aggregates.Count > 0)
            {
                sb.AppendLine("#### Metrics");
                sb.AppendLine("| Metric | Min | P50 | P90 | P95 | P99 | Max | Mean | StdDev |");
                sb.AppendLine("|--------|-----|-----|-----|-----|-----|-----|------|--------|");
                
                foreach (var kvp in scenario.Aggregates)
                {
                    var stats = kvp.Value;
                    sb.AppendLine($"| {kvp.Key} | {stats.Min:F2} | {stats.P50:F2} | {stats.P90:F2} | {stats.P95:F2} | {stats.P99:F2} | {stats.Max:F2} | {stats.Mean:F2} | {stats.StdDev:F2} |");
                }
                sb.AppendLine();
            }
            
            // Memory metrics
            if (scenario.MemoryMetrics != null)
            {
                var mem = scenario.MemoryMetrics;
                sb.AppendLine("#### Memory Footprint");
                sb.AppendLine("| Metric | Min (MB) | Max (MB) | Avg (MB) |");
                sb.AppendLine("|--------|----------|----------|----------|");
                sb.AppendLine($"| Working Set | {mem.WorkingSetMinMB:F2} | {mem.WorkingSetMaxMB:F2} | {mem.WorkingSetAvgMB:F2} |");
                sb.AppendLine($"| Private Memory | {mem.PrivateMemoryMinMB:F2} | {mem.PrivateMemoryMaxMB:F2} | {mem.PrivateMemoryAvgMB:F2} |");
                sb.AppendLine($"| Managed Heap | {mem.ManagedHeapMinMB:F2} | {mem.ManagedHeapMaxMB:F2} | {mem.ManagedHeapAvgMB:F2} |");
                sb.AppendLine();
            }
            
            // GC metrics
            if (scenario.GcMetrics != null)
            {
                var gc = scenario.GcMetrics;
                sb.AppendLine("#### GC & Allocations");
                sb.AppendLine("| Metric | Value |");
                sb.AppendLine("|--------|-------|");
                sb.AppendLine($"| Gen0 Collections | {gc.Gen0Collections} |");
                sb.AppendLine($"| Gen1 Collections | {gc.Gen1Collections} |");
                sb.AppendLine($"| Gen2 Collections | {gc.Gen2Collections} |");
                sb.AppendLine($"| Total Allocated (MB) | {gc.TotalAllocatedMB:F2} |");
                sb.AppendLine($"| Allocations/Op (MB) | {gc.AllocationsPerOperation / (1024.0 * 1024.0):F2} |");
                sb.AppendLine();
            }
            
            // Runtime counters
            if (scenario.RuntimeCounters != null)
            {
                var rc = scenario.RuntimeCounters;
                sb.AppendLine("#### Runtime Counters");
                sb.AppendLine("| Counter | Avg | Peak |");
                sb.AppendLine("|---------|-----|------|");
                sb.AppendLine($"| CPU Usage (%) | {rc.CpuUsageAvg:F2} | {rc.CpuUsagePeak:F2} |");
                sb.AppendLine($"| Working Set (MB) | {rc.WorkingSetAvgMB:F2} | {rc.WorkingSetPeakMB:F2} |");
                sb.AppendLine($"| GC Heap Size (MB) | {rc.GcHeapSizeAvgMB:F2} | {rc.GcHeapSizePeakMB:F2} |");
                sb.AppendLine($"| Alloc Rate (MB/s) | {rc.AllocRateAvgMBPerSec:F2} | {rc.AllocRatePeakMBPerSec:F2} |");
                sb.AppendLine($"| Time in GC (%) | {rc.TimeInGcPercent:F2} | - |");
                sb.AppendLine($"| ThreadPool Threads | {rc.ThreadPoolThreadCountAvg:F0} | - |");
                sb.AppendLine();
            }
        }
    }
}

/// <summary>
/// Generates JSON reports with deterministic ordering.
/// </summary>
public static class JsonReportGenerator
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
    
    public static string Generate(BenchmarkReport report)
    {
        // Create a custom object with deterministic property ordering
        var orderedReport = new
        {
            schemaVersion = report.SchemaVersion,
            timestamp = report.Timestamp,
            timestampUtc = report.TimestampUtc,
            environment = new
            {
                osDescription = report.Environment.OsDescription,
                osVersion = report.Environment.OsVersion,
                processArchitecture = report.Environment.ProcessArchitecture,
                processorCount = report.Environment.ProcessorCount,
                dotNetVersion = report.Environment.DotNetVersion,
                runtimeDescription = report.Environment.RuntimeDescription,
                buildConfiguration = report.Environment.BuildConfiguration,
                repoCommitHash = report.Environment.RepoCommitHash,
                engineThreads = report.Environment.EngineThreads
            },
            runConfig = new
            {
                modelPath = report.RunConfig.ModelPath,
                scenario = report.RunConfig.Scenario,
                iterations = report.RunConfig.Iterations,
                warmup = report.RunConfig.Warmup,
                concurrency = report.RunConfig.Concurrency,
                maxNewTokens = report.RunConfig.MaxNewTokens,
                promptProfile = report.RunConfig.PromptProfile,
                seed = report.RunConfig.Seed,
                temperature = report.RunConfig.Temperature,
                topK = report.RunConfig.TopK,
                topP = report.RunConfig.TopP,
                threads = report.RunConfig.Threads,
                coldStart = report.RunConfig.ColdStart
            },
            scenarios = report.Scenarios.Select(s => new
            {
                name = s.Name,
                aggregates = s.Aggregates.ToDictionary(
                    kvp => kvp.Key,
                    kvp => new
                    {
                        min = kvp.Value.Min,
                        p50 = kvp.Value.P50,
                        p90 = kvp.Value.P90,
                        p95 = kvp.Value.P95,
                        p99 = kvp.Value.P99,
                        max = kvp.Value.Max,
                        mean = kvp.Value.Mean,
                        stdDev = kvp.Value.StdDev
                    }
                ),
                memoryMetrics = s.MemoryMetrics == null ? null : new
                {
                    workingSetMinBytes = s.MemoryMetrics.WorkingSetMinBytes,
                    workingSetMaxBytes = s.MemoryMetrics.WorkingSetMaxBytes,
                    workingSetAvgBytes = s.MemoryMetrics.WorkingSetAvgBytes,
                    privateMemoryMinBytes = s.MemoryMetrics.PrivateMemoryMinBytes,
                    privateMemoryMaxBytes = s.MemoryMetrics.PrivateMemoryMaxBytes,
                    privateMemoryAvgBytes = s.MemoryMetrics.PrivateMemoryAvgBytes,
                    managedHeapMinBytes = s.MemoryMetrics.ManagedHeapMinBytes,
                    managedHeapMaxBytes = s.MemoryMetrics.ManagedHeapMaxBytes,
                    managedHeapAvgBytes = s.MemoryMetrics.ManagedHeapAvgBytes
                },
                gcMetrics = s.GcMetrics == null ? null : new
                {
                    gen0Collections = s.GcMetrics.Gen0Collections,
                    gen1Collections = s.GcMetrics.Gen1Collections,
                    gen2Collections = s.GcMetrics.Gen2Collections,
                    totalAllocatedBytes = s.GcMetrics.TotalAllocatedBytes,
                    allocationsPerOperation = s.GcMetrics.AllocationsPerOperation
                },
                runtimeCounters = s.RuntimeCounters == null ? null : new
                {
                    cpuUsageAvg = s.RuntimeCounters.CpuUsageAvg,
                    cpuUsagePeak = s.RuntimeCounters.CpuUsagePeak,
                    workingSetAvgMB = s.RuntimeCounters.WorkingSetAvgMB,
                    workingSetPeakMB = s.RuntimeCounters.WorkingSetPeakMB,
                    gcHeapSizeAvgMB = s.RuntimeCounters.GcHeapSizeAvgMB,
                    gcHeapSizePeakMB = s.RuntimeCounters.GcHeapSizePeakMB,
                    allocRateAvgMBPerSec = s.RuntimeCounters.AllocRateAvgMBPerSec,
                    allocRatePeakMBPerSec = s.RuntimeCounters.AllocRatePeakMBPerSec,
                    gen0GcCount = s.RuntimeCounters.Gen0GcCount,
                    gen1GcCount = s.RuntimeCounters.Gen1GcCount,
                    gen2GcCount = s.RuntimeCounters.Gen2GcCount,
                    timeInGcPercent = s.RuntimeCounters.TimeInGcPercent,
                    threadPoolThreadCountAvg = s.RuntimeCounters.ThreadPoolThreadCountAvg,
                    lockContentionCountAvg = s.RuntimeCounters.LockContentionCountAvg
                }
            }).ToList()
        };
        
        return JsonSerializer.Serialize(orderedReport, Options);
    }
}
