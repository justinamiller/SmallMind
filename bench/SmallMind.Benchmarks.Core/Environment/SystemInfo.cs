using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
using System.Runtime.Intrinsics.Arm;

namespace SmallMind.Benchmarks.Core.Environment;

/// <summary>
/// Captures cross-platform system information for benchmarking.
/// </summary>
public static class SystemInfo
{
    /// <summary>
    /// Captures a complete snapshot of the current system environment.
    /// </summary>
    /// <returns>Environment snapshot containing all available system information.</returns>
    public static EnvironmentSnapshot CaptureSnapshot()
    {
        return new EnvironmentSnapshot
        {
            OperatingSystem = GetOperatingSystem(),
            RuntimeVersion = GetRuntimeVersion(),
            CpuModel = GetCpuModel(),
            LogicalCores = GetLogicalCores(),
            CpuBaseFrequencyMhz = GetCpuBaseFrequencyMhz(),
            CpuMaxFrequencyMhz = GetCpuMaxFrequencyMhz(),
            SimdCapabilities = GetSimdCapabilities(),
            IsServerGC = GetIsServerGC(),
            GCLatencyMode = GetGCLatencyMode(),
            GitCommitSha = GetGitCommitSha(),
            Timestamp = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// Gets the operating system description.
    /// </summary>
    private static string GetOperatingSystem()
    {
        return RuntimeInformation.OSDescription;
    }

    /// <summary>
    /// Gets the .NET runtime version.
    /// </summary>
    private static string GetRuntimeVersion()
    {
        return RuntimeInformation.FrameworkDescription;
    }

    /// <summary>
    /// Gets the CPU model name using platform-specific methods.
    /// </summary>
    private static string GetCpuModel()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return GetCpuModelLinux();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return GetCpuModelMacOS();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return GetCpuModelWindows();
            }
        }
        catch
        {
            // Silently fall through to unknown
        }

        return "unknown";
    }

    /// <summary>
    /// Gets CPU model from /proc/cpuinfo on Linux.
    /// </summary>
    private static string GetCpuModelLinux()
    {
        const string cpuInfoPath = "/proc/cpuinfo";
        if (!File.Exists(cpuInfoPath))
        {
            return "unknown";
        }

        string[] lines = File.ReadAllLines(cpuInfoPath);
        foreach (string line in lines)
        {
            if (line.StartsWith("model name", StringComparison.OrdinalIgnoreCase))
            {
                int colonIndex = line.IndexOf(':');
                if (colonIndex >= 0 && colonIndex + 1 < line.Length)
                {
                    return line.Substring(colonIndex + 1).Trim();
                }
            }
        }

        return "unknown";
    }

    /// <summary>
    /// Gets CPU model using sysctl on macOS.
    /// </summary>
    private static string GetCpuModelMacOS()
    {
        return ExecuteCommand("sysctl", "-n machdep.cpu.brand_string") ?? "unknown";
    }

    /// <summary>
    /// Gets CPU model using WMI on Windows.
    /// </summary>
    private static string GetCpuModelWindows()
    {
        string? result = ExecuteCommand("wmic", "cpu get name /format:list");
        if (result != null)
        {
            string[] lines = result.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string line in lines)
            {
                if (line.StartsWith("Name=", StringComparison.OrdinalIgnoreCase))
                {
                    return line.Substring(5).Trim();
                }
            }
        }

        return "unknown";
    }

    /// <summary>
    /// Gets the number of logical CPU cores.
    /// </summary>
    private static int GetLogicalCores()
    {
        return System.Environment.ProcessorCount;
    }

    /// <summary>
    /// Gets the CPU base frequency in MHz.
    /// </summary>
    private static double? GetCpuBaseFrequencyMhz()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return GetCpuFrequencyLinux("cpuinfo_cur_freq") ?? GetCpuFrequencyFromCpuInfo();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return GetCpuFrequencyMacOS();
            }
        }
        catch
        {
            // Best effort - return null if unavailable
        }

        return null;
    }

    /// <summary>
    /// Gets the CPU maximum frequency in MHz.
    /// </summary>
    private static double? GetCpuMaxFrequencyMhz()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return GetCpuFrequencyLinux("cpuinfo_max_freq");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return GetCpuFrequencyMacOS();
            }
        }
        catch
        {
            // Best effort - return null if unavailable
        }

        return null;
    }

    /// <summary>
    /// Gets CPU frequency from Linux sysfs.
    /// </summary>
    private static double? GetCpuFrequencyLinux(string freqFile)
    {
        string path = $"/sys/devices/system/cpu/cpu0/cpufreq/{freqFile}";
        if (!File.Exists(path))
        {
            return null;
        }

        string content = File.ReadAllText(path).Trim();
        if (double.TryParse(content, out double khz))
        {
            return khz / 1000.0; // Convert kHz to MHz
        }

        return null;
    }

    /// <summary>
    /// Gets CPU frequency from /proc/cpuinfo on Linux (fallback).
    /// </summary>
    private static double? GetCpuFrequencyFromCpuInfo()
    {
        const string cpuInfoPath = "/proc/cpuinfo";
        if (!File.Exists(cpuInfoPath))
        {
            return null;
        }

        string[] lines = File.ReadAllLines(cpuInfoPath);
        foreach (string line in lines)
        {
            if (line.StartsWith("cpu MHz", StringComparison.OrdinalIgnoreCase))
            {
                int colonIndex = line.IndexOf(':');
                if (colonIndex >= 0 && colonIndex + 1 < line.Length)
                {
                    string freqStr = line.Substring(colonIndex + 1).Trim();
                    if (double.TryParse(freqStr, out double mhz))
                    {
                        return mhz;
                    }
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Gets CPU frequency from macOS sysctl.
    /// </summary>
    private static double? GetCpuFrequencyMacOS()
    {
        string? result = ExecuteCommand("sysctl", "-n hw.cpufrequency");
        if (result != null && long.TryParse(result.Trim(), out long hz))
        {
            return hz / 1_000_000.0; // Convert Hz to MHz
        }

        return null;
    }

    /// <summary>
    /// Gets available SIMD instruction sets.
    /// </summary>
    private static List<string> GetSimdCapabilities()
    {
        var capabilities = new List<string>();

        // x86/x64 SIMD
        if (Sse2.IsSupported)
        {
            capabilities.Add("SSE2");
        }
        if (Avx2.IsSupported)
        {
            capabilities.Add("AVX2");
        }
        if (Avx512F.IsSupported)
        {
            capabilities.Add("AVX512F");
        }

        // ARM SIMD
        if (AdvSimd.IsSupported)
        {
            capabilities.Add("AdvSimd");
        }

        return capabilities;
    }

    /// <summary>
    /// Gets whether Server GC is enabled.
    /// </summary>
    private static bool GetIsServerGC()
    {
        return GCSettings.IsServerGC;
    }

    /// <summary>
    /// Gets the current GC latency mode.
    /// </summary>
    private static string GetGCLatencyMode()
    {
        return GCSettings.LatencyMode.ToString();
    }

    /// <summary>
    /// Gets the Git commit SHA of the current repository.
    /// </summary>
    private static string? GetGitCommitSha()
    {
        try
        {
            string? result = ExecuteCommand("git", "rev-parse HEAD");
            return result?.Trim();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Executes a command and returns its output.
    /// </summary>
    /// <param name="fileName">Command to execute.</param>
    /// <param name="arguments">Command arguments.</param>
    /// <returns>Command output or null if execution failed.</returns>
    private static string? ExecuteCommand(string fileName, string arguments)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                return null;
            }

            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            return process.ExitCode == 0 ? output : null;
        }
        catch
        {
            return null;
        }
    }
}
