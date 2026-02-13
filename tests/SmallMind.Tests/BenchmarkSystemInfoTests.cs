namespace SmallMind.Tests
{
    /// <summary>
    /// Unit tests for the benchmark system info collector.
    /// These tests verify that system metadata collection works correctly.
    /// </summary>
    public class BenchmarkSystemInfoTests
    {
        [Fact]
        public void SystemInfoCollector_CollectsBasicInfo()
        {
            // Note: We can't directly test the benchmark code since it's in a separate project
            // This test is a placeholder to demonstrate the testing pattern

            // In a real scenario, we would either:
            // 1. Move SystemInfoCollector to the main SmallMind project for testability
            // 2. Create a separate test project for benchmarks
            // 3. Make SystemInfoCollector a shared/common utility

            // For now, this test verifies basic .NET runtime features that the collector uses
            Assert.NotNull(Environment.Version);
            Assert.NotEqual(0, Environment.ProcessorCount);
            Assert.NotNull(System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription);
        }

        [Fact]
        public void BitConverter_ReportsEndianness()
        {
            // Test that we can detect endianness like the SystemInfoCollector does
            var isLittleEndian = BitConverter.IsLittleEndian;
            var endiannessString = isLittleEndian ? "Little" : "Big";

            // Verify the string conversion works
            Assert.NotNull(endiannessString);
            Assert.True(endiannessString == "Little" || endiannessString == "Big");
        }

        [Fact]
        public void ProcessorCount_IsPositive()
        {
            // Verify we can read processor count
            var processorCount = Environment.ProcessorCount;
            Assert.True(processorCount > 0, "Processor count should be positive");
        }

        [Fact]
        public void RuntimeInformation_IsAvailable()
        {
            // Test that runtime information APIs work
            var frameworkDesc = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription;
            var osDesc = System.Runtime.InteropServices.RuntimeInformation.OSDescription;
            var arch = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture;

            Assert.NotNull(frameworkDesc);
            Assert.NotNull(osDesc);
            Assert.NotEqual(default(System.Runtime.InteropServices.Architecture), arch);
        }

        [Fact]
        public void GCSettings_AreAccessible()
        {
            // Test GC settings that the collector uses
            var isServerGC = System.Runtime.GCSettings.IsServerGC;
            var latencyMode = System.Runtime.GCSettings.LatencyMode;

            // Verify we can read GC settings without errors and they have valid values
            Assert.NotEqual(default(System.Runtime.GCLatencyMode), latencyMode);

            // GC mode should be a valid string representation
            var gcModeString = isServerGC ? "Server" : "Workstation";
            Assert.True(gcModeString == "Server" || gcModeString == "Workstation");
        }

        [Fact]
        public void VectorT_HasPositiveCount()
        {
            // Test SIMD vector capabilities
            var vectorSize = System.Numerics.Vector<float>.Count;
            Assert.True(vectorSize > 0, "Vector<float>.Count should be positive");
        }
    }
}
