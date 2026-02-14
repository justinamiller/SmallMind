using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
using System.Runtime.Intrinsics.Arm;
using System.Text;

namespace SmallMind.Benchmarks.Core;

/// <summary>
/// Captures comprehensive environment information for benchmark reproducibility.
/// </summary>
public sealed class EnvironmentInfo
{
    public string GitCommitSha { get; set; } = string.Empty;
    public string RuntimeVersion { get; set; } = string.Empty;
    public string OsDescription { get; set; } = string.Empty;
    public string OsArchitecture { get; set; } = string.Empty;
    public string ProcessArchitecture { get; set; } = string.Empty;
    public string CpuModel { get; set; } = string.Empty;
    public int LogicalCoreCount { get; set; }
    public double? BaseCpuFrequencyGHz { get; set; }
    public double? MaxCpuFrequencyGHz { get; set; }
    public bool IsServerGC { get; set; }
    public bool IsConcurrentGC { get; set; }
    public SimdSupport SimdFlags { get; set; } = new();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public static EnvironmentInfo Capture()
    {
        var info = new EnvironmentInfo
        {
            GitCommitSha = GetGitCommitSha(),
            RuntimeVersion = Environment.Version.ToString(),
            OsDescription = RuntimeInformation.OSDescription,
            OsArchitecture = RuntimeInformation.OSArchitecture.ToString(),
            ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
            LogicalCoreCount = Environment.ProcessorCount,
            IsServerGC = System.Runtime.GCSettings.IsServerGC,
            IsConcurrentGC = System.Runtime.GCSettings.LatencyMode != System.Runtime.GCLatencyMode.Batch,
            SimdFlags = SimdSupport.Detect()
        };

        // Detect CPU info
        var cpuInfo = CpuDetector.DetectCpu();
        info.CpuModel = cpuInfo.Model;
        info.BaseCpuFrequencyGHz = cpuInfo.BaseFrequencyGHz;
        info.MaxCpuFrequencyGHz = cpuInfo.MaxFrequencyGHz;

        return info;
    }

    private static string GetGitCommitSha()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "rev-parse HEAD",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null)
                return "unknown";

            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();
            return process.ExitCode == 0 ? output : "unknown";
        }
        catch
        {
            return "unknown";
        }
    }
}

/// <summary>
/// SIMD instruction set support flags.
/// </summary>
public sealed class SimdSupport
{
    public bool Sse2 { get; set; }
    public bool Avx { get; set; }
    public bool Avx2 { get; set; }
    public bool Avx512F { get; set; }
    public bool AdvSimd { get; set; }

    public static SimdSupport Detect()
    {
        var support = new SimdSupport();

        if (RuntimeInformation.ProcessArchitecture == Architecture.X64 ||
            RuntimeInformation.ProcessArchitecture == Architecture.X86)
        {
            support.Sse2 = System.Runtime.Intrinsics.X86.Sse2.IsSupported;
            support.Avx = System.Runtime.Intrinsics.X86.Avx.IsSupported;
            support.Avx2 = System.Runtime.Intrinsics.X86.Avx2.IsSupported;
            support.Avx512F = System.Runtime.Intrinsics.X86.Avx512F.IsSupported;
        }
        else if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
        {
            support.AdvSimd = System.Runtime.Intrinsics.Arm.AdvSimd.IsSupported;
        }

        return support;
    }

    public override string ToString()
    {
        var flags = new StringBuilder();
        if (Sse2) flags.Append("SSE2 ");
        if (Avx) flags.Append("AVX ");
        if (Avx2) flags.Append("AVX2 ");
        if (Avx512F) flags.Append("AVX512F ");
        if (AdvSimd) flags.Append("AdvSimd ");
        return flags.Length > 0 ? flags.ToString().Trim() : "None";
    }
}

/// <summary>
/// CPU information.
/// </summary>
public sealed class CpuInfo
{
    public string Model { get; set; } = string.Empty;
    public double? BaseFrequencyGHz { get; set; }
    public double? MaxFrequencyGHz { get; set; }
}

/// <summary>
/// Cross-platform CPU detection (best-effort).
/// </summary>
public static class CpuDetector
{
    public static CpuInfo DetectCpu()
    {
        var info = new CpuInfo();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            DetectLinux(info);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            DetectMacOS(info);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            DetectWindows(info);
        }

        return info;
    }

    private static void DetectLinux(CpuInfo info)
    {
        try
        {
            if (!File.Exists("/proc/cpuinfo"))
                return;

            var lines = File.ReadAllLines("/proc/cpuinfo");
            foreach (var line in lines)
            {
                if (line.StartsWith("model name", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = line.Split(':', 2);
                    if (parts.Length == 2)
                    {
                        info.Model = parts[1].Trim();
                        break;
                    }
                }
            }

            // Try to read frequency from cpufreq
            var freqPath = "/sys/devices/system/cpu/cpu0/cpufreq/cpuinfo_max_freq";
            if (File.Exists(freqPath))
            {
                var freqKhzStr = File.ReadAllText(freqPath).Trim();
                if (long.TryParse(freqKhzStr, out var freqKhz))
                {
                    info.MaxFrequencyGHz = freqKhz / 1_000_000.0;
                }
            }

            // Try base frequency
            var baseFreqPath = "/sys/devices/system/cpu/cpu0/cpufreq/base_frequency";
            if (File.Exists(baseFreqPath))
            {
                var freqKhzStr = File.ReadAllText(baseFreqPath).Trim();
                if (long.TryParse(freqKhzStr, out var freqKhz))
                {
                    info.BaseFrequencyGHz = freqKhz / 1_000_000.0;
                }
            }
        }
        catch
        {
            // Best-effort, ignore errors
        }
    }

    private static void DetectMacOS(CpuInfo info)
    {
        try
        {
            // Get CPU model
            var modelResult = RunCommand("sysctl", "-n machdep.cpu.brand_string");
            if (!string.IsNullOrWhiteSpace(modelResult))
            {
                info.Model = modelResult.Trim();
            }

            // Get frequency (in Hz)
            var freqResult = RunCommand("sysctl", "-n hw.cpufrequency_max");
            if (!string.IsNullOrWhiteSpace(freqResult) && long.TryParse(freqResult.Trim(), out var freqHz))
            {
                info.MaxFrequencyGHz = freqHz / 1_000_000_000.0;
            }

            // Try alternative for Apple Silicon
            if (info.MaxFrequencyGHz == null)
            {
                freqResult = RunCommand("sysctl", "-n hw.cpufrequency");
                if (!string.IsNullOrWhiteSpace(freqResult) && long.TryParse(freqResult.Trim(), out freqHz))
                {
                    info.MaxFrequencyGHz = freqHz / 1_000_000_000.0;
                }
            }
        }
        catch
        {
            // Best-effort, ignore errors
        }
    }

    private static void DetectWindows(CpuInfo info)
    {
        try
        {
            // Use registry to get CPU name
            var modelResult = RunCommand("wmic", "cpu get name /value");
            if (!string.IsNullOrWhiteSpace(modelResult))
            {
                var lines = modelResult.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                var nameLine = lines.FirstOrDefault(l => l.StartsWith("Name=", StringComparison.OrdinalIgnoreCase));
                if (nameLine != null)
                {
                    info.Model = nameLine.Substring(5).Trim();
                }
            }

            // Get max clock speed (in MHz)
            var freqResult = RunCommand("wmic", "cpu get MaxClockSpeed /value");
            if (!string.IsNullOrWhiteSpace(freqResult))
            {
                var lines = freqResult.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                var freqLine = lines.FirstOrDefault(l => l.StartsWith("MaxClockSpeed=", StringComparison.OrdinalIgnoreCase));
                if (freqLine != null && int.TryParse(freqLine.Substring(14).Trim(), out var freqMhz))
                {
                    info.MaxFrequencyGHz = freqMhz / 1000.0;
                }
            }
        }
        catch
        {
            // Best-effort, ignore errors
        }
    }

    private static string RunCommand(string fileName, string arguments)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null)
                return string.Empty;

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return process.ExitCode == 0 ? output : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
}
