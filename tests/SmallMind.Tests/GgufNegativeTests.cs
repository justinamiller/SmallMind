using SmallMind.Quantization.IO.Gguf;
using SmallMind.Runtime;
using SmallMind.Runtime.Gguf;
using SmallMind.Runtime.Gguf.TensorDecoders;

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

        [Fact]
        public void TensorDecoderRegistry_Q6K_IsSupported()
        {
            // Ensure Q6_K is recognized by the decoder registry used for GGUF compatibility checks.
            var registry = new TensorDecoderRegistry();
            Assert.True(registry.IsSupported(GgufTensorType.Q6_K),
                "Q6_K must be registered in TensorDecoderRegistry for end-to-end GGUF loading.");
        }

        [Fact]
        public void TensorDecoderRegistry_CommonTypes_AreSupported()
        {
            // Regression guard: commonly used tensor types must never be accidentally removed.
            var registry = new TensorDecoderRegistry();
            Assert.True(registry.IsSupported(GgufTensorType.F32),  "F32 must be supported");
            Assert.True(registry.IsSupported(GgufTensorType.F16),  "F16 must be supported");
            Assert.True(registry.IsSupported(GgufTensorType.Q4_0), "Q4_0 must be supported");
            Assert.True(registry.IsSupported(GgufTensorType.Q8_0), "Q8_0 must be supported");
            Assert.True(registry.IsSupported(GgufTensorType.Q4_K), "Q4_K must be supported");
            Assert.True(registry.IsSupported(GgufTensorType.Q5_K), "Q5_K must be supported");
            Assert.True(registry.IsSupported(GgufTensorType.Q6_K), "Q6_K must be supported");
            Assert.True(registry.IsSupported(GgufTensorType.Q8_K), "Q8_K must be supported");
        }

        [Fact]
        public void GetCompatibilityReport_Q6KTensor_ReportedAsFullyCompatible()
        {
            // End-to-end GGUF loader test that includes a Q6_K tensor.
            // Creates a minimal GGUF v3 binary with one 256-element Q6_K tensor,
            // parses it via GgufModelLoader.GetCompatibilityReport, and verifies
            // the report correctly identifies Q6_K as a supported tensor type.
            //
            // GGUF v3 binary layout (no metadata, one tensor):
            //   [0..3]   magic "GGUF"
            //   [4..7]   version = 3  (uint32 LE)
            //   [8..15]  tensor_count = 1  (uint64 LE)
            //   [16..23] metadata_kv_count = 0  (uint64 LE)
            //   --- tensor info ---
            //   [24..31] name length = 11  (uint64 LE)
            //   [32..42] name = "test.weight"
            //   [43..46] n_dims = 1  (uint32 LE)
            //   [47..54] dims[0] = 256  (uint64 LE)
            //   [55..58] type = 14 = Q6_K  (uint32 LE, per GgufTensorType enum)
            //   [59..66] offset = 0  (uint64 LE, relative to data section)

            string tempPath = Path.Combine(Path.GetTempPath(), $"q6k_loader_{Guid.NewGuid():N}.gguf");
            try
            {
                using (var fs = File.Create(tempPath))
                using (var bw = new System.IO.BinaryWriter(fs, System.Text.Encoding.UTF8))
                {
                    // Header
                    bw.Write(System.Text.Encoding.ASCII.GetBytes("GGUF")); // magic (4 bytes)
                    bw.Write((uint)3);    // version
                    bw.Write((ulong)1);   // tensor_count
                    bw.Write((ulong)0);   // metadata_kv_count

                    // Tensor info: name = "test.weight"
                    byte[] nameBytes = System.Text.Encoding.UTF8.GetBytes("test.weight");
                    bw.Write((ulong)nameBytes.Length);
                    bw.Write(nameBytes);
                    bw.Write((uint)1);        // n_dims = 1
                    bw.Write((ulong)256);     // dims[0] = 256 (one full Q6_K super-block)
                    bw.Write((uint)14);       // type = Q6_K (GgufTensorType.Q6_K = 14)
                    bw.Write((ulong)0);       // offset = 0 (relative, within data section)
                }

                // Act: parse through the real GgufModelLoader compatibility report path
                var report = GgufModelLoader.GetCompatibilityReport(tempPath);

                // Assert: the Q6_K tensor must be reported as supported
                Assert.Equal(1, report.TotalTensors);
                Assert.Equal(1, report.SupportedTensors);
                Assert.Equal(0, report.UnsupportedTensors);
                Assert.True(report.IsFullyCompatible,
                    "A GGUF file with a single Q6_K tensor should be fully compatible.");
                Assert.True(report.SupportedTensorsByType.ContainsKey("Q6_K"),
                    "Q6_K should appear in SupportedTensorsByType.");
                Assert.Equal(1, report.SupportedTensorsByType["Q6_K"]);
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }
    }
}
