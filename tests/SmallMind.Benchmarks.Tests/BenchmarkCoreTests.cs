using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;
using SmallMind.Benchmarks.Core;

namespace SmallMind.Benchmarks.Tests;

public class ModelManifestTests
{
    [Fact]
    public void ModelManifest_Serialization_RoundTrips()
    {
        // Arrange
        var manifest = new ModelManifest
        {
            Version = "1.0",
            Models = new()
            {
                new ModelManifestEntry
                {
                    Name = "Test Model",
                    Url = "https://example.com/model.gguf",
                    Sha256 = "abc123",
                    Size = 1024000,
                    QuantType = "Q4_0",
                    ContextLength = 2048,
                    Ci = true,
                    Description = "Test",
                    Tags = new[] { "test" }
                }
            }
        };

        // Act
        var json = JsonSerializer.Serialize(manifest);
        var deserialized = JsonSerializer.Deserialize<ModelManifest>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal("1.0", deserialized!.Version);
        Assert.Single(deserialized.Models);
        Assert.Equal("Test Model", deserialized.Models[0].Name);
        Assert.Equal("Q4_0", deserialized.Models[0].QuantType);
        Assert.True(deserialized.Models[0].Ci);
    }

    [Fact]
    public void ModelManifestEntry_HasRequiredProperties()
    {
        // Arrange & Act
        var entry = new ModelManifestEntry
        {
            Name = "Model",
            Url = "https://example.com/test.gguf",
            Sha256 = "hash",
            Size = 1000,
            QuantType = "Q4_0",
            ContextLength = 512,
            Ci = false
        };

        // Assert
        Assert.NotEmpty(entry.Name);
        Assert.NotEmpty(entry.Url);
        Assert.NotEmpty(entry.Sha256);
        Assert.True(entry.Size > 0);
        Assert.True(entry.ContextLength > 0);
    }
}

public class StatsSummaryTests
{
    [Fact]
    public void StatsSummary_Calculate_ComputesCorrectly()
    {
        // Arrange
        Span<double> values = stackalloc double[] { 1.0, 2.0, 3.0, 4.0, 5.0 };

        // Act
        var stats = StatsSummary.Calculate(values);

        // Assert
        Assert.Equal(3.0, stats.Median);
        Assert.Equal(3.0, stats.Mean);
        Assert.Equal(1.0, stats.Min);
        Assert.Equal(5.0, stats.Max);
        Assert.Equal(5, stats.Samples);
        Assert.True(stats.Stddev > 0);
    }

    [Fact]
    public void StatsSummary_Calculate_HandlesEvenCount()
    {
        // Arrange
        Span<double> values = stackalloc double[] { 1.0, 2.0, 3.0, 4.0 };

        // Act
        var stats = StatsSummary.Calculate(values);

        // Assert
        Assert.Equal(2.5, stats.Median); // (2 + 3) / 2
        Assert.Equal(2.5, stats.Mean);
        Assert.Equal(4, stats.Samples);
    }

    [Fact]
    public void StatsSummary_Calculate_HandlesEmptyArray()
    {
        // Arrange
        Span<double> values = stackalloc double[0];

        // Act
        var stats = StatsSummary.Calculate(values);

        // Assert
        Assert.Equal(0, stats.Samples);
        Assert.Equal(0.0, stats.Median);
    }
}

public class EnvironmentInfoTests
{
    [Fact]
    public void EnvironmentInfo_Capture_ReturnsValidData()
    {
        // Act
        var info = EnvironmentInfo.Capture();

        // Assert
        Assert.NotNull(info);
        Assert.NotEmpty(info.OsDescription);
        Assert.NotEmpty(info.RuntimeVersion);
        Assert.True(info.LogicalCoreCount > 0);
        Assert.NotNull(info.SimdFlags);
        Assert.NotEqual(default, info.Timestamp);
    }

    [Fact]
    public void SimdSupport_Detect_ReturnsValidFlags()
    {
        // Act
        var simd = SimdSupport.Detect();

        // Assert
        Assert.NotNull(simd);
        // At least one SIMD should be supported on modern hardware
        _ = simd.Sse2 || simd.Avx || simd.Avx2 || simd.Avx512F || simd.AdvSimd;
        // We can't assert this is true because it depends on the runner
        // Just verify the detection ran without errors
    }

    [Fact]
    public void SimdSupport_ToString_ReturnsFormattedString()
    {
        // Arrange
        var simd = new SimdSupport { Sse2 = true, Avx = true };

        // Act
        var str = simd.ToString();

        // Assert
        Assert.Contains("SSE2", str);
        Assert.Contains("AVX", str);
    }
}

public class NormalizationTests
{
    [Fact]
    public void NormalizationCalculator_Calculate_ComputesTokPerSecPerCore()
    {
        // Arrange
        var scenario = new ScenarioResult
        {
            Threads = 4,
            TokensPerSecond = new StatsSummary { Median = 40.0 }
        };
        var env = new EnvironmentInfo { LogicalCoreCount = 8 };

        // Act
        var normalized = NormalizationCalculator.Calculate(scenario, env);

        // Assert
        Assert.NotNull(normalized.TokPerSecPerCore);
        Assert.Equal(10.0, normalized.TokPerSecPerCore.Value); // 40 / 4 threads
    }

    [Fact]
    public void NormalizationCalculator_Calculate_ComputesCyclesPerToken_ForSingleThread()
    {
        // Arrange
        var scenario = new ScenarioResult
        {
            Threads = 1,
            TokensPerSecond = new StatsSummary { Median = 10.0 }
        };
        var env = new EnvironmentInfo
        {
            LogicalCoreCount = 4,
            BaseCpuFrequencyGHz = 2.5
        };

        // Act
        var normalized = NormalizationCalculator.Calculate(scenario, env);

        // Assert
        Assert.NotNull(normalized.CyclesPerToken);
        // (2.5 * 1e9) / 10 = 250,000,000
        Assert.Equal(250_000_000.0, normalized.CyclesPerToken.Value, precision: 1);
    }

    [Fact]
    public void NormalizationCalculator_FormatMetric_HandlesNull()
    {
        // Act
        var result = NormalizationCalculator.FormatMetric(null, "tok/s");

        // Assert
        Assert.Equal("N/A", result);
    }

    [Fact]
    public void NormalizationCalculator_FormatMetric_FormatsValue()
    {
        // Act
        var result = NormalizationCalculator.FormatMetric(12.345, "tok/s", 2);

        // Assert
        Assert.Contains("12.35", result);
        Assert.Contains("tok/s", result);
    }
}

public class ModelDownloaderTests
{
    [Fact]
    public async Task ComputeSha256Async_ComputesCorrectHash()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempFile, "test content");

            // Act
            var hash = await ModelDownloader.ComputeSha256Async(tempFile);

            // Assert
            Assert.NotEmpty(hash);
            Assert.Equal(64, hash.Length); // SHA256 is 32 bytes = 64 hex chars
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ComputeSha256Async_ConsistentForSameContent()
    {
        // Arrange
        var tempFile1 = Path.GetTempFileName();
        var tempFile2 = Path.GetTempFileName();
        try
        {
            var content = "identical content";
            await File.WriteAllTextAsync(tempFile1, content);
            await File.WriteAllTextAsync(tempFile2, content);

            // Act
            var hash1 = await ModelDownloader.ComputeSha256Async(tempFile1);
            var hash2 = await ModelDownloader.ComputeSha256Async(tempFile2);

            // Assert
            Assert.Equal(hash1, hash2);
        }
        finally
        {
            if (File.Exists(tempFile1)) File.Delete(tempFile1);
            if (File.Exists(tempFile2)) File.Delete(tempFile2);
        }
    }
}
