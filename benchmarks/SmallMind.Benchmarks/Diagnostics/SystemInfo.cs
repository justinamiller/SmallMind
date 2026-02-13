using System;
using System.Diagnostics;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
using System.Numerics;

namespace SmallMind.Benchmarks.Diagnostics
{
    /// <summary>
    /// Collects system information for benchmark reports.
    /// </summary>
    internal sealed class SystemInfo
    {
        public string OperatingSystem { get; init; } = string.Empty;
        public string Architecture { get; init; } = string.Empty;
        public string RuntimeVersion { get; init; } = string.Empty;
        public int ProcessorCount { get; init; }
        public string GCMode { get; init; } = string.Empty;
        public string GCLatencyMode { get; init; } = string.Empty;
        public bool IsServerGC { get; init; }
        public SimdCapabilities Simd { get; init; } = new();
        public string MachineName { get; init; } = string.Empty;
        public DateTime Timestamp { get; init; }

        public static SystemInfo Collect()
        {
            return new SystemInfo
            {
                OperatingSystem = RuntimeInformation.OSDescription,
                Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
                RuntimeVersion = RuntimeInformation.FrameworkDescription,
                ProcessorCount = Environment.ProcessorCount,
                GCMode = GCSettings.IsServerGC ? "Server" : "Workstation",
                GCLatencyMode = GCSettings.LatencyMode.ToString(),
                IsServerGC = GCSettings.IsServerGC,
                Simd = SimdCapabilities.Detect(),
                MachineName = Environment.MachineName,
                Timestamp = DateTime.UtcNow
            };
        }

        public override string ToString()
        {
            return $"{OperatingSystem} ({Architecture}), {RuntimeVersion}, " +
                   $"{ProcessorCount} cores, GC: {GCMode}/{GCLatencyMode}";
        }
    }

    /// <summary>
    /// SIMD capabilities of the current CPU.
    /// </summary>
    internal sealed class SimdCapabilities
    {
        public int VectorSize { get; init; }
        public bool IsHardwareAccelerated { get; init; }
        public bool SupportsAvx { get; init; }
        public bool SupportsAvx2 { get; init; }
        public bool SupportsAvx512F { get; init; }
        public bool SupportsSse { get; init; }
        public bool SupportsSse2 { get; init; }
        public bool SupportsSse3 { get; init; }
        public bool SupportsSsse3 { get; init; }
        public bool SupportsSse41 { get; init; }
        public bool SupportsSse42 { get; init; }
        public bool SupportsFma { get; init; }

        public static SimdCapabilities Detect()
        {
            return new SimdCapabilities
            {
                VectorSize = Vector<float>.Count,
                IsHardwareAccelerated = Vector.IsHardwareAccelerated,
                SupportsAvx = Avx.IsSupported,
                SupportsAvx2 = Avx2.IsSupported,
                SupportsAvx512F = Avx512F.IsSupported,
                SupportsSse = Sse.IsSupported,
                SupportsSse2 = Sse2.IsSupported,
                SupportsSse3 = Sse3.IsSupported,
                SupportsSsse3 = Ssse3.IsSupported,
                SupportsSse41 = Sse41.IsSupported,
                SupportsSse42 = Sse42.IsSupported,
                SupportsFma = Fma.IsSupported
            };
        }

        public string GetBestInstructionSet()
        {
            if (SupportsAvx512F) return "AVX-512";
            if (SupportsAvx2) return "AVX2";
            if (SupportsAvx) return "AVX";
            if (SupportsSse42) return "SSE4.2";
            if (SupportsSse41) return "SSE4.1";
            if (SupportsSsse3) return "SSSE3";
            if (SupportsSse3) return "SSE3";
            if (SupportsSse2) return "SSE2";
            if (SupportsSse) return "SSE";
            return "None";
        }

        public override string ToString()
        {
            return $"Vector<float>.Count={VectorSize}, Best ISA={GetBestInstructionSet()}, " +
                   $"HW Accel={IsHardwareAccelerated}";
        }
    }
}
