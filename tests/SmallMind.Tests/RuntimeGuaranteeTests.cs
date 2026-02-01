using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using SmallMind.Abstractions;

namespace SmallMind.Tests
{
    /// <summary>
    /// Tests for runtime guarantee enforcement in the SmallMind engine.
    /// These tests validate the stable runtime contract and production-ready behavior.
    /// Tests only use public APIs - no internal class testing.
    /// </summary>
    public class RuntimeGuaranteeTests : IDisposable
    {
        private readonly ISmallMindEngine _engine;
        private readonly string _testDataDir;

        public RuntimeGuaranteeTests()
        {
            _engine = Engine.SmallMind.Create(new SmallMindOptions
            {
                EnableKvCache = false,
                EnableRag = false,
                EnableBatching = false
            });

            // Set up test data directory
            _testDataDir = Path.Combine(Path.GetTempPath(), "SmallMind", "RuntimeGuaranteeTests");
            Directory.CreateDirectory(_testDataDir);
        }

        public void Dispose()
        {
            _engine?.Dispose();
        }

        #region Model Loading Validation Tests

        [Fact]
        public async Task LoadModel_WithNullPath_ThrowsArgumentException()
        {
            // Arrange
            var request = new ModelLoadRequest
            {
                Path = null!
            };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(async () =>
                await _engine.LoadModelAsync(request));
        }

        [Fact]
        public async Task LoadModel_WithEmptyPath_ThrowsArgumentException()
        {
            // Arrange
            var request = new ModelLoadRequest
            {
                Path = ""
            };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(async () =>
                await _engine.LoadModelAsync(request));
        }

        [Fact]
        public async Task LoadModel_WithNonExistentFile_ThrowsFileNotFoundException()
        {
            // Arrange
            var request = new ModelLoadRequest
            {
                Path = Path.Combine(_testDataDir, "nonexistent.smq")
            };

            // Act & Assert
            await Assert.ThrowsAsync<FileNotFoundException>(async () =>
                await _engine.LoadModelAsync(request));
        }

        [Fact]
        public async Task LoadModel_WithUnsupportedExtension_ThrowsUnsupportedModelException()
        {
            // Arrange
            var unsupportedFile = Path.Combine(_testDataDir, "model.onnx");
            await File.WriteAllTextAsync(unsupportedFile, "dummy content");

            var request = new ModelLoadRequest
            {
                Path = unsupportedFile
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<UnsupportedModelException>(async () =>
                await _engine.LoadModelAsync(request));

            Assert.Contains(".onnx", ex.Message);
            Assert.Contains("Remediation", ex.Message);
            Assert.Equal(".onnx", ex.Extension);
        }

        [Fact]
        public async Task LoadModel_WithGgufWithoutAllowImport_ThrowsUnsupportedModelException()
        {
            // Arrange
            var ggufFile = Path.Combine(_testDataDir, "model.gguf");
            await File.WriteAllTextAsync(ggufFile, "dummy gguf content");

            var request = new ModelLoadRequest
            {
                Path = ggufFile,
                AllowGgufImport = false  // Explicit false
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<UnsupportedModelException>(async () =>
                await _engine.LoadModelAsync(request));

            Assert.Contains("AllowGgufImport", ex.Message);
            Assert.Contains("Remediation", ex.Message);
        }

        [Theory]
        [InlineData(".pt")]
        [InlineData(".pth")]
        [InlineData(".safetensors")]
        [InlineData(".h5")]
        [InlineData(".keras")]
        public async Task LoadModel_WithKnownUnsupportedFormats_ThrowsUnsupportedModelException(string extension)
        {
            // Arrange
            var unsupportedFile = Path.Combine(_testDataDir, $"model{extension}");
            await File.WriteAllTextAsync(unsupportedFile, "dummy content");

            var request = new ModelLoadRequest
            {
                Path = unsupportedFile
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<UnsupportedModelException>(async () =>
                await _engine.LoadModelAsync(request));

            Assert.Contains(extension, ex.Message);
            Assert.Contains("not supported", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Remediation", ex.Message);
        }

        #endregion

        #region Capability Discovery Tests

        [Fact]
        public void Engine_ExposesCapabilities()
        {
            // Act
            var caps = _engine.Capabilities;

            // Assert
            Assert.NotNull(caps);
            Assert.True(caps.SupportsQuantizedInference);
            Assert.True(caps.SupportsGgufImport);
            Assert.True(caps.SupportsDeterministicMode);
            Assert.True(caps.SupportsStreaming);
        }

        #endregion
    }
}
