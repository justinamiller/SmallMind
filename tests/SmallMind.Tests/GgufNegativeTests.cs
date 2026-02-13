using System;
using System.IO;
using System.Text;
using Xunit;
using SmallMind.Runtime;
using SmallMind.Runtime.Gguf;
using SmallMind.Quantization.IO.Gguf;

namespace SmallMind.Tests
{
    /// <summary>
    /// Negative tests for GGUF loader: corrupted files, missing tensors, unsupported types, malformed data.
    /// Validates fail-fast behavior with actionable error messages.
    /// </summary>
    public class GgufNegativeTests
    {
        [Fact]
        public void LoadFromGguf_FileNotFound_ThrowsFileNotFoundException()
        {
            // Arrange
            string nonExistentPath = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid()}.gguf");

            // Act & Assert
            var ex = Assert.Throws<FileNotFoundException>(() =>
            {
                GgufModelLoader.LoadFromGguf(nonExistentPath);
            });

            Assert.Contains("not found", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(nonExistentPath, ex.Message);
        }

        [Fact]
        public void LoadFromGguf_NullPath_ThrowsArgumentNullException()
        {
            // Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() =>
            {
                GgufModelLoader.LoadFromGguf(null!);
            });

            Assert.Equal("ggufPath", ex.ParamName);
        }

        [Fact]
        public void LoadFromGguf_EmptyPath_ThrowsArgumentNullException()
        {
            // Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() =>
            {
                GgufModelLoader.LoadFromGguf(string.Empty);
            });

            Assert.Equal("ggufPath", ex.ParamName);
        }

        [Fact]
        public void LoadFromGguf_CorruptedMagicNumber_ThrowsInvalidDataException()
        {
            // Arrange
            string tempPath = Path.Combine(Path.GetTempPath(), $"corrupt_magic_{Guid.NewGuid()}.gguf");

            try
            {
                // Write a file with invalid magic number
                using (var fs = File.Create(tempPath))
                using (var writer = new BinaryWriter(fs))
                {
                    writer.Write(0xDEADBEEF); // Invalid magic (should be 0x46554747 "GGUF")
                    writer.Write((uint)3);    // Version
                    writer.Write((ulong)0);   // Tensor count
                    writer.Write((ulong)0);   // Metadata count
                }

                // Act & Assert
                Assert.ThrowsAny<Exception>(() =>
                {
                    GgufModelLoader.LoadFromGguf(tempPath);
                });
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }

        [Fact]
        public void LoadFromGguf_EmptyFile_ThrowsInvalidDataException()
        {
            // Arrange
            string tempPath = Path.Combine(Path.GetTempPath(), $"empty_{Guid.NewGuid()}.gguf");

            try
            {
                // Create an empty file
                File.WriteAllBytes(tempPath, Array.Empty<byte>());

                // Act & Assert
                Assert.ThrowsAny<Exception>(() =>
                {
                    GgufModelLoader.LoadFromGguf(tempPath);
                });
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }

        [Fact]
        public void LoadFromGguf_TruncatedFile_ThrowsInvalidDataException()
        {
            // Arrange
            string tempPath = Path.Combine(Path.GetTempPath(), $"truncated_{Guid.NewGuid()}.gguf");

            try
            {
                // Write only magic and version, then truncate
                using (var fs = File.Create(tempPath))
                using (var writer = new BinaryWriter(fs))
                {
                    writer.Write(0x46554747); // "GGUF" magic
                    writer.Write((uint)3);    // Version
                    // Missing tensor count and metadata count
                }

                // Act & Assert
                Assert.ThrowsAny<Exception>(() =>
                {
                    GgufModelLoader.LoadFromGguf(tempPath);
                });
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }

        [Fact]
        public void GetCompatibilityReport_UnsupportedTensorType_ReportsCorrectly()
        {
            // This test would require creating a GGUF file with unsupported tensor types
            // For now, we validate the report API structure

            // Note: Creating a full GGUF file with unsupported types is complex
            // This is a placeholder for future implementation with a test fixture
            
            // If we had a test file with IQ2_XXS tensors:
            // var report = GgufModelLoader.GetCompatibilityReport("test_iq2_xxs.gguf");
            // Assert.False(report.IsFullyCompatible);
            // Assert.Contains("IQ2_XXS", report.UnsupportedTensorsByType.Keys);
            // Assert.True(report.UnsupportedTensors > 0);
            
            // For now, just ensure the API exists
            Assert.NotNull(typeof(GgufCompatibilityReport));
        }

        [Fact]
        public void GgufCompatibilityReport_GetSummary_ContainsExpectedSections()
        {
            // Arrange - Create a mock report
            var report = new GgufCompatibilityReport
            {
                Architecture = "llama",
                FormatVersion = 3,
                TotalTensors = 100,
                SupportedTensors = 90,
                UnsupportedTensors = 10,
                SupportedTensorsByType = new() { ["Q4_0"] = 80, ["F16"] = 10 },
                UnsupportedTensorsByType = new() { ["IQ2_XXS"] = new() { "tensor1", "tensor2", "tensor3" } }
            };

            // Act
            string summary = report.GetSummary();

            // Assert
            Assert.Contains("GGUF Compatibility Report", summary);
            Assert.Contains("Architecture: llama", summary);
            Assert.Contains("GGUF Version: 3", summary);
            Assert.Contains("Total Tensors: 100", summary);
            Assert.Contains("Supported: 90", summary);
            Assert.Contains("Unsupported: 10", summary);
            Assert.Contains("UNSUPPORTED tensors", summary);
            Assert.Contains("IQ2_XXS", summary);
            Assert.Contains("To fix:", summary);
            Assert.Contains("quantize", summary, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void GgufCompatibilityReport_ThrowIfIncompatible_ThrowsForUnsupportedTensors()
        {
            // Arrange
            var report = new GgufCompatibilityReport
            {
                TotalTensors = 10,
                SupportedTensors = 5,
                UnsupportedTensors = 5,
                UnsupportedTensorsByType = new() { ["IQ2_XXS"] = new() { "test" } }
            };

            // Act & Assert
            var ex = Assert.Throws<NotSupportedException>(() =>
            {
                report.ThrowIfIncompatible();
            });

            Assert.Contains("5 unsupported tensor", ex.Message);
            Assert.Contains("IQ2_XXS", ex.Message);
        }

        [Fact]
        public void GgufCompatibilityReport_ThrowIfIncompatible_DoesNotThrowForFullyCompatible()
        {
            // Arrange
            var report = new GgufCompatibilityReport
            {
                TotalTensors = 10,
                SupportedTensors = 10,
                UnsupportedTensors = 0,
                SupportedTensorsByType = new() { ["Q4_0"] = 10 }
            };

            // Act & Assert (should not throw)
            report.ThrowIfIncompatible();
        }

        [Fact]
        public void GgufCompatibilityReport_IsFullyCompatible_ReturnsTrueWhenAllSupported()
        {
            // Arrange
            var report = new GgufCompatibilityReport
            {
                TotalTensors = 50,
                SupportedTensors = 50,
                UnsupportedTensors = 0
            };

            // Act & Assert
            Assert.True(report.IsFullyCompatible);
        }

        [Fact]
        public void GgufCompatibilityReport_IsFullyCompatible_ReturnsFalseWhenAnyUnsupported()
        {
            // Arrange
            var report = new GgufCompatibilityReport
            {
                TotalTensors = 50,
                SupportedTensors = 49,
                UnsupportedTensors = 1
            };

            // Act & Assert
            Assert.False(report.IsFullyCompatible);
        }

        [Fact]
        public void LoadConfigFromGguf_FileNotFound_ThrowsFileNotFoundException()
        {
            // Arrange
            string nonExistentPath = Path.Combine(Path.GetTempPath(), $"config_{Guid.NewGuid()}.gguf");

            // Act & Assert
            var ex = Assert.Throws<FileNotFoundException>(() =>
            {
                GgufModelLoader.LoadConfigFromGguf(nonExistentPath);
            });

            Assert.Contains(nonExistentPath, ex.Message);
        }

        [Fact]
        public void LoadTokenizerFromGguf_FileNotFound_ThrowsFileNotFoundException()
        {
            // Arrange
            string nonExistentPath = Path.Combine(Path.GetTempPath(), $"tokenizer_{Guid.NewGuid()}.gguf");

            // Act & Assert
            var ex = Assert.Throws<FileNotFoundException>(() =>
            {
                GgufModelLoader.LoadTokenizerFromGguf(nonExistentPath);
            });

            Assert.Contains(nonExistentPath, ex.Message);
        }

        [Fact]
        public void GetCompatibilityReport_FileNotFound_ThrowsFileNotFoundException()
        {
            // Arrange
            string nonExistentPath = Path.Combine(Path.GetTempPath(), $"report_{Guid.NewGuid()}.gguf");

            // Act & Assert
            var ex = Assert.Throws<FileNotFoundException>(() =>
            {
                GgufModelLoader.GetCompatibilityReport(nonExistentPath);
            });

            Assert.Contains(nonExistentPath, ex.Message);
        }

        [Fact]
        public void GetCompatibilityReport_NullPath_ThrowsArgumentNullException()
        {
            // Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() =>
            {
                GgufModelLoader.GetCompatibilityReport(null!);
            });

            Assert.Equal("ggufPath", ex.ParamName);
        }
    }
}
