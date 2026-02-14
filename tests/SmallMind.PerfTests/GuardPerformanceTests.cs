using System.Diagnostics;
using SmallMind.Core.Validation;
using SmallMind.Core.Exceptions;

namespace SmallMind.PerfTests
{
    /// <summary>
    /// Performance tests for Guard validation methods to ensure they don't impact hot paths.
    /// These tests validate that the security fixes for CodeQL path traversal issues
    /// are performant and don't introduce measurable overhead.
    /// </summary>
    public class GuardPerformanceTests
    {
        private const int WarmupIterations = 1000;
        private const int TestIterations = 100000;

        [Fact]
        public void SafeFileName_PerformanceIsAcceptable()
        {
            // Arrange
            string validFileName = "model_checkpoint_epoch_42.smq";
            
            // Warmup
            for (int i = 0; i < WarmupIterations; i++)
            {
                Guard.SafeFileName(validFileName);
            }

            // Act - Measure performance
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < TestIterations; i++)
            {
                Guard.SafeFileName(validFileName);
            }
            sw.Stop();

            // Assert - Should be very fast (< 10 microseconds per call on average)
            var avgMicroseconds = (sw.Elapsed.TotalMicroseconds / TestIterations);
            var avgNanoseconds = (sw.Elapsed.TotalNanoseconds / TestIterations);
            
            // Log performance for visibility
            Console.WriteLine($"SafeFileName: {avgNanoseconds:F2}ns per call ({TestIterations:N0} iterations)");
            
            // Very generous threshold: each call should take < 10μs (10000ns)
            // In practice, this should be well under 1μs (1000ns) on modern hardware
            Assert.True(avgNanoseconds < 10000, 
                $"SafeFileName is too slow: {avgNanoseconds:F2}ns per call (expected < 10000ns)");
        }

        [Fact]
        public void PathWithinDirectory_PerformanceIsAcceptable()
        {
            // Arrange
            var basePath = Path.GetTempPath();
            var relativePath = "subfolder/model.smq";
            
            // Warmup
            for (int i = 0; i < WarmupIterations; i++)
            {
                Guard.PathWithinDirectory(basePath, relativePath);
            }

            // Act - Measure performance
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < TestIterations; i++)
            {
                Guard.PathWithinDirectory(basePath, relativePath);
            }
            sw.Stop();

            // Assert - Should be reasonably fast
            // PathWithinDirectory uses Path.GetFullPath which is more expensive,
            // but still should be acceptable for non-hot-path validation
            var avgMicroseconds = (sw.Elapsed.TotalMicroseconds / TestIterations);
            var avgNanoseconds = (sw.Elapsed.TotalNanoseconds / TestIterations);
            
            // Log performance for visibility
            Console.WriteLine($"PathWithinDirectory: {avgNanoseconds:F2}ns per call ({TestIterations:N0} iterations)");
            
            // Generous threshold: each call should take < 50μs (50000ns)
            // Path.GetFullPath involves OS calls, so this is more expensive than SafeFileName
            Assert.True(avgNanoseconds < 50000, 
                $"PathWithinDirectory is too slow: {avgNanoseconds:F2}ns per call (expected < 50000ns)");
        }

        [Fact]
        public void SafeFileName_NoAllocationsOnSuccess()
        {
            // Arrange
            string validFileName = "model.smq";
            long initialMemory = GC.GetTotalMemory(true);

            // Act - Run many times to detect allocations
            for (int i = 0; i < TestIterations; i++)
            {
                Guard.SafeFileName(validFileName);
            }

            // Force GC to collect any temporary allocations
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long finalMemory = GC.GetTotalMemory(true);
            long allocatedBytes = finalMemory - initialMemory;
            
            // Log allocation info
            Console.WriteLine($"SafeFileName allocations: {allocatedBytes:N0} bytes for {TestIterations:N0} calls");
            
            // Assert - Minimal allocations (allowing some GC overhead)
            // Should be well under 1KB per 100k calls
            var bytesPerCall = (double)allocatedBytes / TestIterations;
            Assert.True(bytesPerCall < 0.01, 
                $"SafeFileName allocates too much: {bytesPerCall:F4} bytes per call");
        }

        [Fact]
        public void InferenceEngine_CachePath_PerformanceIsAcceptable()
        {
            // This test simulates the exact pattern used in InferenceEngine.cs
            // to ensure the security fix doesn't impact performance

            // Arrange
            string ggufPath = "/models/test-model.gguf";
            string cacheDir = Path.Combine(Path.GetTempPath(), "SmallMind", "GgufCache");
            Directory.CreateDirectory(cacheDir);

            try
            {
                // Warmup
                for (int i = 0; i < WarmupIterations; i++)
                {
                    var fileName = Path.GetFileName(ggufPath);
                    var fileNameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                    Guard.SafeFileName(fileNameWithoutExt, nameof(ggufPath));
                    var smqPath = Path.Combine(cacheDir, $"{fileNameWithoutExt}.smq");
                }

                // Act - Measure performance
                var sw = Stopwatch.StartNew();
                for (int i = 0; i < TestIterations; i++)
                {
                    var fileName = Path.GetFileName(ggufPath);
                    var fileNameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                    Guard.SafeFileName(fileNameWithoutExt, nameof(ggufPath));
                    var smqPath = Path.Combine(cacheDir, $"{fileNameWithoutExt}.smq");
                }
                sw.Stop();

                // Assert
                var avgNanoseconds = (sw.Elapsed.TotalNanoseconds / TestIterations);
                Console.WriteLine($"InferenceEngine cache path pattern: {avgNanoseconds:F2}ns per call ({TestIterations:N0} iterations)");
                
                // This includes multiple Path operations plus Guard.SafeFileName
                // Should still be very fast: < 20μs (20000ns) per call
                Assert.True(avgNanoseconds < 20000,
                    $"InferenceEngine cache path pattern is too slow: {avgNanoseconds:F2}ns per call (expected < 20000ns)");
            }
            finally
            {
                if (Directory.Exists(cacheDir))
                {
                    Directory.Delete(cacheDir, true);
                }
            }
        }

        [Fact]
        public void PretrainedRegistry_GetPackFullPath_PerformanceIsAcceptable()
        {
            // This test simulates the exact pattern used in PretrainedRegistry.cs
            // to ensure the security fix doesn't impact performance

            // Arrange
            var basePath = Path.Combine(Path.GetTempPath(), "packs");
            var packPath = "test-pack-v1";
            Directory.CreateDirectory(basePath);

            try
            {
                // Warmup
                for (int i = 0; i < WarmupIterations; i++)
                {
                    Guard.PathWithinDirectory(basePath, packPath, nameof(packPath));
                }

                // Act - Measure performance
                var sw = Stopwatch.StartNew();
                for (int i = 0; i < TestIterations; i++)
                {
                    Guard.PathWithinDirectory(basePath, packPath, nameof(packPath));
                }
                sw.Stop();

                // Assert
                var avgNanoseconds = (sw.Elapsed.TotalNanoseconds / TestIterations);
                Console.WriteLine($"PretrainedRegistry GetPackFullPath pattern: {avgNanoseconds:F2}ns per call ({TestIterations:N0} iterations)");
                
                // Should be reasonably fast: < 50μs (50000ns) per call
                Assert.True(avgNanoseconds < 50000,
                    $"PretrainedRegistry GetPackFullPath pattern is too slow: {avgNanoseconds:F2}ns per call (expected < 50000ns)");
            }
            finally
            {
                if (Directory.Exists(basePath))
                {
                    Directory.Delete(basePath, true);
                }
            }
        }
    }
}
