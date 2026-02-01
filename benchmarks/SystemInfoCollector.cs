using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
using System.Runtime.Intrinsics.Arm;
using SmallMind.Simd;

#if WINDOWS
using System.Management;
#endif

namespace SmallMind.Benchmarks
{
    /// <summary>
    /// Collects comprehensive system, runtime, and build metadata for benchmark reproducibility.
    /// All probes are wrapped in try/catch to ensure cross-platform safety.
    /// </summary>
    public static class SystemInfoCollector
    {
        /// <summary>
        /// Collects all available system metadata.
        /// Safe to call on any platform - failures are handled gracefully.
        /// </summary>
        public static SystemInfo Collect()
        {
            var info = new SystemInfo
            {
                Machine = CollectMachineInfo(),
                Memory = CollectMemoryInfo(),
                OperatingSystem = CollectOperatingSystemInfo(),
                Runtime = CollectRuntimeInfo(),
                Process = CollectProcessInfo(),
                Build = CollectBuildInfo()
            };

            return info;
        }

        private static MachineInfo CollectMachineInfo()
        {
            var machine = new MachineInfo();

            // CPU Architecture
            try
            {
                machine.CpuArchitecture = RuntimeInformation.ProcessArchitecture.ToString();
            }
            catch { machine.CpuArchitecture = "Unknown"; }

            // Logical Cores
            try
            {
                machine.LogicalCores = Environment.ProcessorCount;
            }
            catch { machine.LogicalCores = 0; }

            // CPU Model (best-effort, platform-specific)
            try
            {
                machine.CpuModel = GetCpuModel();
            }
            catch { machine.CpuModel = "Unknown"; }

            // SIMD Vector Size
            try
            {
                machine.SimdVectorSize = Vector<float>.Count;
            }
            catch { machine.SimdVectorSize = 0; }

            // SIMD Capabilities
            try
            {
                machine.SimdCapabilities = new Dictionary<string, bool>
                {
                    ["Vector_IsHardwareAccelerated"] = Vector.IsHardwareAccelerated,
                    ["SSE"] = Sse.IsSupported,
                    ["SSE2"] = Sse2.IsSupported,
                    ["SSE3"] = Sse3.IsSupported,
                    ["SSSE3"] = Ssse3.IsSupported,
                    ["SSE4.1"] = Sse41.IsSupported,
                    ["SSE4.2"] = Sse42.IsSupported,
                    ["AVX"] = Avx.IsSupported,
                    ["AVX2"] = Avx2.IsSupported,
                    ["AVX-512F"] = Avx512F.IsSupported,
                    ["AVX-512BW"] = Avx512BW.IsSupported,
                    ["FMA"] = Fma.IsSupported,
                    ["ARM_AdvSimd"] = AdvSimd.IsSupported,
                    ["ARM_Aes"] = System.Runtime.Intrinsics.Arm.Aes.IsSupported,
                    ["ARM_Crc32"] = Crc32.IsSupported,
                    ["ARM_Dp"] = Dp.IsSupported,
                    ["ARM_Rdm"] = Rdm.IsSupported,
                    ["ARM_Sha1"] = Sha1.IsSupported,
                    ["ARM_Sha256"] = Sha256.IsSupported
                };
            }
            catch { machine.SimdCapabilities = new Dictionary<string, bool>(); }

            // Endianness
            try
            {
                machine.Endianness = BitConverter.IsLittleEndian ? "Little" : "Big";
            }
            catch { machine.Endianness = "Unknown"; }

            // NUMA awareness (best-effort)
            try
            {
                // Check if we can get NUMA node count on Windows
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    // This is a best-effort check - actual NUMA detection would require P/Invoke
                    machine.NumaAware = machine.LogicalCores > 64; // Heuristic
                }
                else
                {
                    machine.NumaAware = false; // Default for non-Windows
                }
            }
            catch { machine.NumaAware = false; }

            return machine;
        }

        private static MemoryInfo CollectMemoryInfo()
        {
            var memory = new MemoryInfo();

            try
            {
                // Get GC memory info which provides total and available memory
                var gcMemoryInfo = GC.GetGCMemoryInfo();
                memory.TotalMemoryBytes = gcMemoryInfo.TotalAvailableMemoryBytes;
                
                // Available memory is an approximation
                memory.AvailableMemoryBytes = gcMemoryInfo.TotalAvailableMemoryBytes - gcMemoryInfo.MemoryLoadBytes;
            }
            catch
            {
                memory.TotalMemoryBytes = 0;
                memory.AvailableMemoryBytes = 0;
            }

            return memory;
        }

        private static OperatingSystemInfo CollectOperatingSystemInfo()
        {
            var os = new OperatingSystemInfo();

            // Platform
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    os.Platform = "Windows";
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    os.Platform = "Linux";
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    os.Platform = "macOS";
                else
                    os.Platform = "Unknown";
            }
            catch { os.Platform = "Unknown"; }

            // OS Name and Version
            try
            {
                os.OsName = RuntimeInformation.OSDescription;
                os.OsVersion = Environment.OSVersion.Version.ToString();
            }
            catch
            {
                os.OsName = "Unknown";
                os.OsVersion = "Unknown";
            }

            // Kernel Version
            try
            {
                os.KernelVersion = Environment.OSVersion.VersionString;
            }
            catch { os.KernelVersion = "Unknown"; }

            return os;
        }

        private static RuntimeInfo CollectRuntimeInfo()
        {
            var runtime = new RuntimeInfo();

            // .NET Version
            try
            {
                runtime.DotNetVersion = Environment.Version.ToString();
            }
            catch { runtime.DotNetVersion = "Unknown"; }

            // Runtime Identifier
            try
            {
                runtime.RuntimeIdentifier = RuntimeInformation.RuntimeIdentifier;
            }
            catch { runtime.RuntimeIdentifier = "Unknown"; }

            // Framework Description
            try
            {
                runtime.FrameworkDescription = RuntimeInformation.FrameworkDescription;
            }
            catch { runtime.FrameworkDescription = "Unknown"; }

            // GC Mode
            try
            {
                runtime.GcMode = GCSettings.IsServerGC ? "Server" : "Workstation";
                runtime.GcLatencyMode = GCSettings.LatencyMode.ToString();
            }
            catch
            {
                runtime.GcMode = "Unknown";
                runtime.GcLatencyMode = "Unknown";
            }

            // Tiered Compilation
            try
            {
                // Check via AppContext switch
                var tieredCompilationEnabled = AppContext.TryGetSwitch("System.Runtime.TieredCompilation", out bool enabled) 
                    ? enabled 
                    : true; // Default is true in modern .NET
                runtime.TieredCompilation = tieredCompilationEnabled;
            }
            catch { runtime.TieredCompilation = false; }

            // ReadyToRun
            try
            {
                // Check via AppContext switch
                var r2rEnabled = AppContext.TryGetSwitch("System.Runtime.TieredCompilation.QuickJit", out bool enabled)
                    ? !enabled  // QuickJit disabled means R2R is more likely used
                    : false;
                runtime.ReadyToRun = r2rEnabled;
            }
            catch { runtime.ReadyToRun = false; }

            return runtime;
        }

        private static ProcessInfo CollectProcessInfo()
        {
            var process = new ProcessInfo();

            // Bitness
            try
            {
                process.Bitness = Environment.Is64BitProcess ? "64-bit" : "32-bit";
            }
            catch { process.Bitness = "Unknown"; }

            // Priority Class
            try
            {
                using var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
                process.PriorityClass = currentProcess.PriorityClass.ToString();
            }
            catch { process.PriorityClass = "Unknown"; }

            return process;
        }

        private static BuildInfo CollectBuildInfo()
        {
            var build = new BuildInfo();

            // Build Configuration
            try
            {
#if DEBUG
                build.Configuration = "Debug";
#else
                build.Configuration = "Release";
#endif
            }
            catch { build.Configuration = "Unknown"; }

            // Target Framework
            try
            {
                var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
                var targetFrameworkAttr = assembly.GetCustomAttribute<System.Runtime.Versioning.TargetFrameworkAttribute>();
                build.TargetFramework = targetFrameworkAttr?.FrameworkName ?? "Unknown";
            }
            catch { build.TargetFramework = "Unknown"; }

            // Compilation Timestamp (from assembly build time - best effort)
            try
            {
                var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
                var location = assembly.Location;
                if (!string.IsNullOrEmpty(location) && System.IO.File.Exists(location))
                {
                    var buildTime = System.IO.File.GetLastWriteTimeUtc(location);
                    build.CompilationTimestamp = buildTime.ToString("yyyy-MM-dd HH:mm:ss UTC");
                }
                else
                {
                    build.CompilationTimestamp = "Unknown";
                }
            }
            catch { build.CompilationTimestamp = "Unknown"; }

            // Git Commit Hash (from environment variable)
            try
            {
                build.GitCommitHash = Environment.GetEnvironmentVariable("GIT_COMMIT") 
                    ?? Environment.GetEnvironmentVariable("GITHUB_SHA") 
                    ?? "Unknown";
            }
            catch { build.GitCommitHash = "Unknown"; }

            return build;
        }

        private static string GetCpuModel()
        {
            // Platform-specific CPU model detection
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return GetCpuModelWindows();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return GetCpuModelLinux();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return GetCpuModelMacOS();
            }

            return "Unknown";
        }

        private static string GetCpuModelWindows()
        {
#if WINDOWS
            try
            {
                // Try to read from WMI
                using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_Processor");
                foreach (var obj in searcher.Get())
                {
                    return obj["Name"]?.ToString()?.Trim() ?? "Unknown";
                }
            }
            catch
            {
                // If WMI fails, return a generic description
            }
#endif
            return "Windows CPU";
        }

        private static string GetCpuModelLinux()
        {
            try
            {
                // Try to read from /proc/cpuinfo
                if (System.IO.File.Exists("/proc/cpuinfo"))
                {
                    var lines = System.IO.File.ReadAllLines("/proc/cpuinfo");
                    var modelLine = lines.FirstOrDefault(l => l.StartsWith("model name", StringComparison.OrdinalIgnoreCase));
                    if (modelLine != null)
                    {
                        var parts = modelLine.Split(':', 2);
                        if (parts.Length == 2)
                        {
                            return parts[1].Trim();
                        }
                    }
                }
            }
            catch { }

            return "Linux CPU";
        }

        private static string GetCpuModelMacOS()
        {
            try
            {
                // Use sysctl to get CPU brand string
                var psi = new ProcessStartInfo
                {
                    FileName = "sysctl",
                    Arguments = "-n machdep.cpu.brand_string",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = System.Diagnostics.Process.Start(psi);
                if (process != null)
                {
                    var output = process.StandardOutput.ReadToEnd().Trim();
                    process.WaitForExit();
                    if (!string.IsNullOrEmpty(output))
                    {
                        return output;
                    }
                }
            }
            catch { }

            return "macOS CPU";
        }
    }
}
