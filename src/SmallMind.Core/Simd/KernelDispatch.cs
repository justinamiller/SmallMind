using System.Runtime.CompilerServices;
using SmallMind.Abstractions.Telemetry;

namespace SmallMind.Core.Simd
{
    /// <summary>
    /// Central kernel dispatch system for SIMD operations.
    /// Detects CPU capabilities once at startup and assigns optimal kernel implementations.
    /// Eliminates runtime branching overhead by using function pointers selected at initialization.
    /// </summary>
    [SkipLocalsInit]
    internal static class KernelDispatch
    {
        /// <summary>
        /// Kernel selection information for diagnostics and telemetry.
        /// </summary>
        public static readonly KernelInfo Info;

        static KernelDispatch()
        {
            // Initialize kernel info
            Info = new KernelInfo
            {
                Platform = SimdCapabilities.PlatformType,
                BestInstructionSet = SimdCapabilities.BestInstructionSet,
                VectorWidthBits = SimdCapabilities.VectorWidthBits,
                FloatsPerVector = SimdCapabilities.FloatsPerVector,

                // CPU capabilities
                IsAvx512Supported = SimdCapabilities.IsAvx512Supported,
                IsAvx2Supported = SimdCapabilities.IsAvx2Supported,
                IsFmaSupported = SimdCapabilities.IsFmaSupported,
                IsNeonSupported = SimdCapabilities.IsNeonSupported,

                // Kernel selections (will be filled by specific kernel init)
                MatMulKernel = DetermineMatMulKernel(),
                SoftmaxKernel = DetermineSoftmaxKernel(),
                ActivationKernel = DetermineActivationKernel(),
            };
        }

        private static string DetermineMatMulKernel()
        {
            if (SimdCapabilities.IsAvx512Supported)
                return "AVX-512 + FMA";
            else if (SimdCapabilities.IsAvx2Supported && SimdCapabilities.IsFmaSupported)
                return "AVX2 + FMA";
            else if (SimdCapabilities.IsNeonSupported)
                return "ARM NEON";
            else
                return "Vector<T>";
        }

        private static string DetermineSoftmaxKernel()
        {
            if (SimdCapabilities.IsAvx512Supported)
                return "AVX-512";
            else if (SimdCapabilities.IsAvx2Supported)
                return "AVX2";
            else if (SimdCapabilities.IsNeonSupported)
                return "ARM NEON";
            else
                return "Vector<T>";
        }

        private static string DetermineActivationKernel()
        {
            if (SimdCapabilities.IsAvx512Supported)
                return "AVX-512";
            else if (SimdCapabilities.IsAvx2Supported)
                return "AVX2";
            else if (SimdCapabilities.IsNeonSupported)
                return "ARM NEON";
            else
                return "Vector<T>";
        }

        /// <summary>
        /// Kernel selection and platform information.
        /// </summary>
        public struct KernelInfo
        {
            /// <summary>
            /// Platform type (x86/x64, ARM64, or Unknown).
            /// </summary>
            public string Platform;

            /// <summary>
            /// Best available instruction set (e.g., "AVX-512", "ARM NEON").
            /// </summary>
            public string BestInstructionSet;

            /// <summary>
            /// Vector width in bits (128, 256, 512, or 0 for scalar).
            /// </summary>
            public int VectorWidthBits;

            /// <summary>
            /// Number of floats per vector.
            /// </summary>
            public int FloatsPerVector;

            // CPU capabilities
            public bool IsAvx512Supported;
            public bool IsAvx2Supported;
            public bool IsFmaSupported;
            public bool IsNeonSupported;

            // Selected kernels
            public string MatMulKernel;
            public string SoftmaxKernel;
            public string ActivationKernel;

            /// <summary>
            /// Returns a human-readable summary of kernel selections.
            /// </summary>
            public override string ToString()
            {
                return $"Platform: {Platform}, Instructions: {BestInstructionSet}, " +
                       $"VectorWidth: {VectorWidthBits} bits, " +
                       $"MatMul: {MatMulKernel}, Softmax: {SoftmaxKernel}, Activation: {ActivationKernel}";
            }
        }

        /// <summary>
        /// Logs kernel dispatch information.
        /// Useful for diagnostics and performance analysis.
        /// </summary>
        /// <param name="logger">Optional logger instance. If null, no logging occurs.</param>
        public static void PrintKernelInfo(IRuntimeLogger? logger = null)
        {
            if (logger == null)
                return;

            logger.Info("=== SmallMind Kernel Dispatch ===");
            logger.Info($"Platform: {Info.Platform}");
            logger.Info($"Best Instruction Set: {Info.BestInstructionSet}");
            logger.Info($"Vector Width: {Info.VectorWidthBits} bits ({Info.FloatsPerVector} floats)");
            logger.Info("");
            logger.Info("CPU Capabilities:");
            logger.Info($"  AVX-512: {Info.IsAvx512Supported}");
            logger.Info($"  AVX2: {Info.IsAvx2Supported}");
            logger.Info($"  FMA: {Info.IsFmaSupported}");
            logger.Info($"  ARM NEON: {Info.IsNeonSupported}");
            logger.Info("");
            logger.Info("Selected Kernels:");
            logger.Info($"  MatMul: {Info.MatMulKernel}");
            logger.Info($"  Softmax: {Info.SoftmaxKernel}");
            logger.Info($"  Activation: {Info.ActivationKernel}");
            logger.Info("=================================");
        }
    }
}
