using System;
using System.IO;
using System.Runtime.InteropServices;
using Xunit;

namespace SmallMind.ModelRegistry.Tests
{
    /// <summary>
    /// Tests for CachePathResolver.
    /// </summary>
    public class CachePathResolverTests
    {
        [Fact]
        public void GetDefaultCacheDirectory_WithEnvOverride_ReturnsOverridePath()
        {
            // Arrange
            string testPath = "/custom/cache/path";
            Environment.SetEnvironmentVariable("SMALLMIND_MODEL_CACHE", testPath);

            try
            {
                // Act
                string result = CachePathResolver.GetDefaultCacheDirectory();

                // Assert
                Assert.Equal(testPath, result);
            }
            finally
            {
                Environment.SetEnvironmentVariable("SMALLMIND_MODEL_CACHE", null);
            }
        }

        [Fact]
        public void GetDefaultCacheDirectory_WithoutEnvOverride_ReturnsPlatformSpecificPath()
        {
            // Arrange
            Environment.SetEnvironmentVariable("SMALLMIND_MODEL_CACHE", null);

            // Act
            string result = CachePathResolver.GetDefaultCacheDirectory();

            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Assert.Contains("SmallMind", result);
                Assert.Contains("models", result);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Assert.Contains("Library", result);
                Assert.Contains("Caches", result);
                Assert.Contains("SmallMind", result);
            }
            else
            {
                Assert.Contains("cache", result.ToLowerInvariant());
                Assert.Contains("smallmind", result.ToLowerInvariant());
            }
        }

        [Fact]
        public void GetModelDirectory_ReturnsCorrectPath()
        {
            // Arrange
            string cacheRoot = "/tmp/cache";
            string modelId = "test-model";

            // Act
            string result = CachePathResolver.GetModelDirectory(cacheRoot, modelId);

            // Assert
            Assert.Equal(Path.Combine(cacheRoot, modelId), result);
        }

        [Fact]
        public void GetManifestPath_ReturnsCorrectPath()
        {
            // Arrange
            string cacheRoot = "/tmp/cache";
            string modelId = "test-model";

            // Act
            string result = CachePathResolver.GetManifestPath(cacheRoot, modelId);

            // Assert
            Assert.Equal(Path.Combine(cacheRoot, modelId, "manifest.json"), result);
        }
    }
}
