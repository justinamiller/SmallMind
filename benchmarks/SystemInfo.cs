using System;
using System.Collections.Generic;

namespace SmallMind.Benchmarks
{
    /// <summary>
    /// Contains complete system, runtime, and build metadata for benchmark reproducibility.
    /// </summary>
    public sealed class SystemInfo
    {
        public MachineInfo Machine { get; set; } = new();
        public MemoryInfo Memory { get; set; } = new();
        public OperatingSystemInfo OperatingSystem { get; set; } = new();
        public RuntimeInfo Runtime { get; set; } = new();
        public ProcessInfo Process { get; set; } = new();
        public BuildInfo Build { get; set; } = new();
    }

    /// <summary>
    /// Machine and hardware information.
    /// </summary>
    public sealed class MachineInfo
    {
        public string CpuArchitecture { get; set; } = string.Empty;
        public int LogicalCores { get; set; }
        public string CpuModel { get; set; } = string.Empty;
        public int SimdVectorSize { get; set; }
        public Dictionary<string, bool> SimdCapabilities { get; set; } = new();
        public string Endianness { get; set; } = string.Empty;
        public bool NumaAware { get; set; }
    }

    /// <summary>
    /// System memory information.
    /// </summary>
    public sealed class MemoryInfo
    {
        public long TotalMemoryBytes { get; set; }
        public long AvailableMemoryBytes { get; set; }
        
        public string TotalMemoryFormatted => FormatBytes(TotalMemoryBytes);
        public string AvailableMemoryFormatted => FormatBytes(AvailableMemoryBytes);

        private static string FormatBytes(long bytes)
        {
            if (bytes <= 0) return "Unknown";
            
            const long GB = 1024L * 1024L * 1024L;
            const long MB = 1024L * 1024L;
            
            if (bytes >= GB)
                return $"{bytes / (double)GB:F1} GB";
            if (bytes >= MB)
                return $"{bytes / (double)MB:F1} MB";
            
            return $"{bytes:N0} bytes";
        }
    }

    /// <summary>
    /// Operating system information.
    /// </summary>
    public sealed class OperatingSystemInfo
    {
        public string Platform { get; set; } = string.Empty;
        public string OsName { get; set; } = string.Empty;
        public string OsVersion { get; set; } = string.Empty;
        public string KernelVersion { get; set; } = string.Empty;
    }

    /// <summary>
    /// .NET runtime information.
    /// </summary>
    public sealed class RuntimeInfo
    {
        public string DotNetVersion { get; set; } = string.Empty;
        public string RuntimeIdentifier { get; set; } = string.Empty;
        public string FrameworkDescription { get; set; } = string.Empty;
        public string GcMode { get; set; } = string.Empty;
        public string GcLatencyMode { get; set; } = string.Empty;
        public bool TieredCompilation { get; set; }
        public bool ReadyToRun { get; set; }
    }

    /// <summary>
    /// Process information.
    /// </summary>
    public sealed class ProcessInfo
    {
        public string Bitness { get; set; } = string.Empty;
        public string PriorityClass { get; set; } = string.Empty;
    }

    /// <summary>
    /// Build configuration information.
    /// </summary>
    public sealed class BuildInfo
    {
        public string Configuration { get; set; } = string.Empty;
        public string TargetFramework { get; set; } = string.Empty;
        public string CompilationTimestamp { get; set; } = string.Empty;
        public string GitCommitHash { get; set; } = string.Empty;
        
        public bool IsReleaseBuild => Configuration.Equals("Release", StringComparison.OrdinalIgnoreCase);
    }
}
