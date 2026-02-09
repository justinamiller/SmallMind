using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;

namespace SmallMind.Core.Simd
{
    /// <summary>
    /// Detects and reports SIMD capabilities of the current CPU.
    /// Provides runtime detection for x86/x64 (SSE, AVX, AVX2, AVX-512, FMA) and ARM (NEON/AdvSimd).
    /// </summary>
    internal static class SimdCapabilities
    {
        /// <summary>
        /// Gets the best supported vector width in bits (128, 256, or 512).
        /// Returns 0 if only scalar operations are supported.
        /// </summary>
        public static int VectorWidthBits { get; }

        /// <summary>
        /// Gets the number of float elements that fit in the best supported vector.
        /// </summary>
        public static int FloatsPerVector { get; }

        /// <summary>
        /// Indicates if SSE (128-bit) is supported (x86/x64 only).
        /// </summary>
        public static bool IsSSESupported { get; }

        /// <summary>
        /// Indicates if SSE2 is supported (x86/x64 only).
        /// </summary>
        public static bool IsSSE2Supported { get; }

        /// <summary>
        /// Indicates if AVX (256-bit) is supported (x86/x64 only).
        /// </summary>
        public static bool IsAvxSupported { get; }

        /// <summary>
        /// Indicates if AVX2 is supported (x86/x64 only).
        /// </summary>
        public static bool IsAvx2Supported { get; }

        /// <summary>
        /// Indicates if AVX-512 is supported (x86/x64 only).
        /// Checks for AVX512F (foundation) which is required for all AVX-512 operations.
        /// </summary>
        public static bool IsAvx512Supported { get; }

        /// <summary>
        /// Indicates if FMA (Fused Multiply-Add) is supported (x86/x64 only).
        /// </summary>
        public static bool IsFmaSupported { get; }

        /// <summary>
        /// Indicates if NEON/AdvSimd is supported (ARM64 only).
        /// </summary>
        public static bool IsNeonSupported { get; }

        /// <summary>
        /// Gets the platform type (x86/x64, ARM64, or Unknown).
        /// </summary>
        public static string PlatformType { get; }

        /// <summary>
        /// Gets the best available instruction set name.
        /// </summary>
        public static string BestInstructionSet { get; }

        static SimdCapabilities()
        {
            // Detect x86/x64 capabilities
            IsSSESupported = Sse.IsSupported;
            IsSSE2Supported = Sse2.IsSupported;
            IsAvxSupported = Avx.IsSupported;
            IsAvx2Supported = Avx2.IsSupported;
            IsAvx512Supported = Avx512F.IsSupported;
            IsFmaSupported = Fma.IsSupported;

            // Detect ARM capabilities
            IsNeonSupported = AdvSimd.IsSupported;

            // Determine platform
            if (IsSSESupported || IsAvxSupported || IsAvx512Supported)
            {
                PlatformType = "x86/x64";
            }
            else if (IsNeonSupported)
            {
                PlatformType = "ARM64";
            }
            else
            {
                PlatformType = "Unknown";
            }

            // Determine best vector width and instruction set
            if (IsAvx512Supported)
            {
                VectorWidthBits = 512;
                FloatsPerVector = 16;
                BestInstructionSet = "AVX-512";
            }
            else if (IsAvx2Supported)
            {
                VectorWidthBits = 256;
                FloatsPerVector = 8;
                BestInstructionSet = IsFmaSupported ? "AVX2+FMA" : "AVX2";
            }
            else if (IsAvxSupported)
            {
                VectorWidthBits = 256;
                FloatsPerVector = 8;
                BestInstructionSet = "AVX";
            }
            else if (IsSSE2Supported)
            {
                VectorWidthBits = 128;
                FloatsPerVector = 4;
                BestInstructionSet = "SSE2";
            }
            else if (IsSSESupported)
            {
                VectorWidthBits = 128;
                FloatsPerVector = 4;
                BestInstructionSet = "SSE";
            }
            else if (IsNeonSupported)
            {
                VectorWidthBits = 128;
                FloatsPerVector = 4;
                BestInstructionSet = "NEON (AdvSimd)";
            }
            else
            {
                // Fallback to Vector<T> which is determined at runtime
                VectorWidthBits = Vector<float>.Count * 32; // bits per float
                FloatsPerVector = Vector<float>.Count;
                BestInstructionSet = $"Vector<T> ({FloatsPerVector} floats)";
            }
        }

        /// <summary>
        /// Prints CPU SIMD capabilities to the console.
        /// Useful for debugging and diagnostics.
        /// </summary>
        public static void PrintCapabilities()
        {
            Console.WriteLine("=== SmallMind SIMD Capabilities ===");
            Console.WriteLine($"Platform: {PlatformType}");
            Console.WriteLine($"Best Instruction Set: {BestInstructionSet}");
            Console.WriteLine($"Vector Width: {VectorWidthBits} bits ({FloatsPerVector} floats/vector)");
            Console.WriteLine();

            if (PlatformType == "x86/x64")
            {
                Console.WriteLine("x86/x64 Features:");
                Console.WriteLine($"  SSE:     {FormatSupport(IsSSESupported)}");
                Console.WriteLine($"  SSE2:    {FormatSupport(IsSSE2Supported)}");
                Console.WriteLine($"  AVX:     {FormatSupport(IsAvxSupported)}");
                Console.WriteLine($"  AVX2:    {FormatSupport(IsAvx2Supported)}");
                Console.WriteLine($"  AVX-512: {FormatSupport(IsAvx512Supported)}");
                Console.WriteLine($"  FMA:     {FormatSupport(IsFmaSupported)}");
            }
            else if (PlatformType == "ARM64")
            {
                Console.WriteLine("ARM64 Features:");
                Console.WriteLine($"  NEON (AdvSimd): {FormatSupport(IsNeonSupported)}");
            }
            else
            {
                Console.WriteLine("No hardware SIMD detected. Using Vector<T> fallback.");
            }

            Console.WriteLine();
            Console.WriteLine($"Vector<float>.Count: {Vector<float>.Count}");
            Console.WriteLine($"Vector.IsHardwareAccelerated: {Vector.IsHardwareAccelerated}");
            Console.WriteLine("===================================");
        }

        /// <summary>
        /// Gets a short summary of SIMD capabilities for logging.
        /// </summary>
        public static string GetSummary()
        {
            return $"SIMD: {BestInstructionSet} ({FloatsPerVector} floats/vec, {VectorWidthBits}-bit)";
        }

        /// <summary>
        /// Gets whether AVX-512 is available.
        /// </summary>
        public static bool HasAvx512 => Avx512F.IsSupported;

        /// <summary>
        /// Gets the preferred vector width in float count.
        /// Returns 16 for AVX-512, 8 for AVX2, or Vector&lt;float&gt;.Count otherwise.
        /// </summary>
        public static int PreferredVectorWidth => Avx512F.IsSupported ? 16 : (Avx2.IsSupported ? 8 : Vector<float>.Count);

        /// <summary>
        /// Performs horizontal sum of a Vector512&lt;float&gt;.
        /// Reduces 512 bits → 256 bits → 128 bits → scalar.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float HorizontalSum(Vector512<float> v)
        {
            // 512 → 256: add upper and lower halves
            var v256 = Avx.Add(
                Avx512F.ExtractVector256(v, 0),
                Avx512F.ExtractVector256(v, 1));
            // 256 → 128
            var v128 = Sse.Add(
                Avx.ExtractVector128(v256, 0),
                Avx.ExtractVector128(v256, 1));
            // 128 → scalar (horizontal sum)
            v128 = Sse.Add(v128, Sse.MoveHighToLow(v128, v128));
            v128 = Sse.AddScalar(v128, Sse.Shuffle(v128, v128, 0x01));
            return v128.ToScalar();
        }

        private static string FormatSupport(bool isSupported)
        {
            return isSupported ? "✓ Supported" : "✗ Not Supported";
        }
    }
}
