using SmallMind.Runtime;
using SmallMind.Runtime.Gguf;

namespace SmallMind.Tests
{
    /// <summary>
    /// Fuzz tests for GGUF parser to validate robustness against malformed, random, and edge case inputs.
    /// Tests ensure the parser fails gracefully without crashes, hangs, or memory corruption.
    /// </summary>
    public class GgufFuzzTests
    {
        private const uint ValidMagic = 0x46554747; // "GGUF" in little-endian
        private const uint ValidVersion = 3;

        #region Random Binary Data Fuzzing

        [Fact]
        public void GgufReader_RandomBinaryData_HandlesGracefully()
        {
            // Arrange
            var random = new Random(42);
            byte[] randomData = new byte[1024];
            random.NextBytes(randomData);

            string tempPath = Path.Combine(Path.GetTempPath(), $"fuzz_random_{Guid.NewGuid()}.gguf");

            try
            {
                File.WriteAllBytes(tempPath, randomData);

                // Act & Assert - should throw exception, not crash
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

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(3)]
        [InlineData(7)]
        [InlineData(15)]
        [InlineData(31)]
        [InlineData(63)]
        [InlineData(127)]
        public void GgufReader_VariousSmallerRandomSizes_HandlesGracefully(int size)
        {
            // Arrange
            var random = new Random(42 + size);
            byte[] randomData = new byte[size];
            random.NextBytes(randomData);

            string tempPath = Path.Combine(Path.GetTempPath(), $"fuzz_size_{size}_{Guid.NewGuid()}.gguf");

            try
            {
                File.WriteAllBytes(tempPath, randomData);

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

        #endregion

        #region Boundary Value Testing

        [Theory]
        [InlineData(0, 0, 0, 0)] // All zeros
        [InlineData(uint.MaxValue, uint.MaxValue, ulong.MaxValue, ulong.MaxValue)] // Max values
        [InlineData(0, uint.MaxValue, 0, ulong.MaxValue)] // Mixed
        public void GgufReader_BoundaryValues_HandlesGracefully(uint version, uint magic, ulong tensorCount, ulong metadataCount)
        {
            // Arrange
            string tempPath = Path.Combine(Path.GetTempPath(), $"fuzz_boundary_{Guid.NewGuid()}.gguf");

            try
            {
                using (var fs = File.Create(tempPath))
                using (var writer = new BinaryWriter(fs))
                {
                    writer.Write(magic);
                    writer.Write(version);
                    writer.Write(tensorCount);
                    writer.Write(metadataCount);
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

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(4)]
        [InlineData(5)]
        [InlineData(100)]
        [InlineData(uint.MaxValue)]
        public void GgufReader_InvalidVersionNumbers_ThrowsException(uint version)
        {
            // Arrange
            string tempPath = Path.Combine(Path.GetTempPath(), $"fuzz_version_{version}_{Guid.NewGuid()}.gguf");

            try
            {
                using (var fs = File.Create(tempPath))
                using (var writer = new BinaryWriter(fs))
                {
                    writer.Write(ValidMagic);
                    writer.Write(version);
                    writer.Write((ulong)0); // tensor count
                    writer.Write((ulong)0); // metadata count
                }

                // Act & Assert
                if (version == 2 || version == 3)
                {
                    // Valid versions should not throw
                    var report = GgufModelLoader.GetCompatibilityReport(tempPath);
                    Assert.NotNull(report);
                }
                else
                {
                    // Invalid versions should throw
                    Assert.ThrowsAny<Exception>(() =>
                    {
                        GgufModelLoader.LoadFromGguf(tempPath);
                    });
                }
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }

        #endregion

        #region Large Value Fuzzing

        [Theory]
        [InlineData(1000000)] // 1 million tensors claimed
        [InlineData(100000000)] // 100 million tensors claimed
        public void GgufReader_ExcessiveTensorCount_HandlesGracefully(ulong tensorCount)
        {
            // Arrange
            string tempPath = Path.Combine(Path.GetTempPath(), $"fuzz_tensor_count_{Guid.NewGuid()}.gguf");

            try
            {
                using (var fs = File.Create(tempPath))
                using (var writer = new BinaryWriter(fs))
                {
                    writer.Write(ValidMagic);
                    writer.Write(ValidVersion);
                    writer.Write(tensorCount);
                    writer.Write((ulong)0); // metadata count
                    // Don't write actual tensor data - test truncation handling
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

        [Theory]
        [InlineData(1000000)] // 1 million metadata entries claimed
        [InlineData(100000000)] // 100 million metadata entries claimed
        public void GgufReader_ExcessiveMetadataCount_HandlesGracefully(ulong metadataCount)
        {
            // Arrange
            string tempPath = Path.Combine(Path.GetTempPath(), $"fuzz_metadata_count_{Guid.NewGuid()}.gguf");

            try
            {
                using (var fs = File.Create(tempPath))
                using (var writer = new BinaryWriter(fs))
                {
                    writer.Write(ValidMagic);
                    writer.Write(ValidVersion);
                    writer.Write((ulong)0); // tensor count
                    writer.Write(metadataCount);
                    // Don't write actual metadata - test truncation handling
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

        #endregion

        #region String Encoding Fuzzing

        [Fact]
        public void GgufReader_ExcessiveStringLength_HandlesGracefully()
        {
            // Arrange
            string tempPath = Path.Combine(Path.GetTempPath(), $"fuzz_string_length_{Guid.NewGuid()}.gguf");

            try
            {
                using (var fs = File.Create(tempPath))
                using (var writer = new BinaryWriter(fs))
                {
                    writer.Write(ValidMagic);
                    writer.Write(ValidVersion);
                    writer.Write((ulong)0); // tensor count
                    writer.Write((ulong)1); // 1 metadata entry

                    // Write a metadata entry with excessive string length
                    writer.Write((ulong)uint.MaxValue); // Claim extremely long key
                    // Don't write actual string data
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
        public void GgufReader_InvalidUtf8Sequences_HandlesGracefully()
        {
            // Arrange
            string tempPath = Path.Combine(Path.GetTempPath(), $"fuzz_utf8_{Guid.NewGuid()}.gguf");

            try
            {
                using (var fs = File.Create(tempPath))
                using (var writer = new BinaryWriter(fs))
                {
                    writer.Write(ValidMagic);
                    writer.Write(ValidVersion);
                    writer.Write((ulong)0); // tensor count
                    writer.Write((ulong)1); // 1 metadata entry

                    // Write metadata with invalid UTF-8 sequence
                    writer.Write((ulong)4); // String length
                    writer.Write((byte)0xFF); // Invalid UTF-8 start byte
                    writer.Write((byte)0xFF);
                    writer.Write((byte)0xFF);
                    writer.Write((byte)0xFF);

                    // Complete the KV entry (type + value)
                    writer.Write((uint)8); // String type
                    writer.Write((ulong)0); // Empty value string
                }

                // Act & Assert - UTF-8 decoder should handle invalid sequences
                // The decoder may replace invalid sequences or throw
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

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(10)]
        public void GgufReader_NullBytesInStrings_HandlesGracefully(int nullByteCount)
        {
            // Arrange
            string tempPath = Path.Combine(Path.GetTempPath(), $"fuzz_null_bytes_{Guid.NewGuid()}.gguf");

            try
            {
                using (var fs = File.Create(tempPath))
                using (var writer = new BinaryWriter(fs))
                {
                    writer.Write(ValidMagic);
                    writer.Write(ValidVersion);
                    writer.Write((ulong)0); // tensor count
                    writer.Write((ulong)1); // 1 metadata entry

                    // Write key with null bytes
                    writer.Write((ulong)nullByteCount);
                    for (int i = 0; i < nullByteCount; i++)
                        writer.Write((byte)0);

                    // Complete the KV entry
                    writer.Write((uint)8); // String type
                    writer.Write((ulong)0); // Empty value string
                }

                // Act & Assert - All null byte counts should throw
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

        #endregion

        #region Malformed Metadata Fuzzing

        [Theory]
        [InlineData(999)] // Unknown type
        [InlineData(uint.MaxValue)]
        public void GgufReader_InvalidMetadataType_HandlesGracefully(uint metadataType)
        {
            // Arrange
            string tempPath = Path.Combine(Path.GetTempPath(), $"fuzz_metadata_type_{Guid.NewGuid()}.gguf");

            try
            {
                using (var fs = File.Create(tempPath))
                using (var writer = new BinaryWriter(fs))
                {
                    writer.Write(ValidMagic);
                    writer.Write(ValidVersion);
                    writer.Write((ulong)0); // tensor count
                    writer.Write((ulong)1); // 1 metadata entry

                    // Write metadata with invalid type
                    writer.Write((ulong)4); // Key length
                    writer.Write(new byte[] { (byte)'t', (byte)'e', (byte)'s', (byte)'t' }); // "test"
                    writer.Write(metadataType); // Invalid type
                    // Don't write value data
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
        public void GgufReader_NestedArrayOverflow_HandlesGracefully()
        {
            // Arrange
            string tempPath = Path.Combine(Path.GetTempPath(), $"fuzz_nested_array_{Guid.NewGuid()}.gguf");

            try
            {
                using (var fs = File.Create(tempPath))
                using (var writer = new BinaryWriter(fs))
                {
                    writer.Write(ValidMagic);
                    writer.Write(ValidVersion);
                    writer.Write((ulong)0); // tensor count
                    writer.Write((ulong)1); // 1 metadata entry

                    // Write metadata with array type
                    writer.Write((ulong)5); // Key length
                    writer.Write(new byte[] { (byte)'a', (byte)'r', (byte)'r', (byte)'a', (byte)'y' }); // "array"
                    writer.Write((uint)9); // Array type
                    writer.Write((uint)0); // Element type (uint8)
                    writer.Write(ulong.MaxValue); // Claim max elements
                    // Don't write actual array data
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

        #endregion

        #region Malformed Tensor Fuzzing

        [Theory]
        [InlineData(0)] // No dimensions
        [InlineData(1000)] // Excessive dimensions
        public void GgufReader_InvalidTensorDimensionCount_HandlesGracefully(uint nDims)
        {
            // Arrange
            string tempPath = Path.Combine(Path.GetTempPath(), $"fuzz_tensor_dims_{Guid.NewGuid()}.gguf");

            try
            {
                using (var fs = File.Create(tempPath))
                using (var writer = new BinaryWriter(fs))
                {
                    writer.Write(ValidMagic);
                    writer.Write(ValidVersion);
                    writer.Write((ulong)1); // 1 tensor
                    writer.Write((ulong)0); // 0 metadata entries

                    // Write tensor info with extreme dimension count
                    writer.Write((ulong)6); // Name length
                    writer.Write(new byte[] { (byte)'t', (byte)'e', (byte)'n', (byte)'s', (byte)'o', (byte)'r' });
                    writer.Write(nDims);
                    // Don't write dimension values or rest of tensor info
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
        public void GgufReader_TensorDimensionOverflow_HandlesGracefully()
        {
            // Arrange
            string tempPath = Path.Combine(Path.GetTempPath(), $"fuzz_tensor_overflow_{Guid.NewGuid()}.gguf");

            try
            {
                using (var fs = File.Create(tempPath))
                using (var writer = new BinaryWriter(fs))
                {
                    writer.Write(ValidMagic);
                    writer.Write(ValidVersion);
                    writer.Write((ulong)1); // 1 tensor
                    writer.Write((ulong)0); // 0 metadata entries

                    // Write tensor with dimensions that would overflow when multiplied
                    writer.Write((ulong)6); // Name length
                    writer.Write(new byte[] { (byte)'t', (byte)'e', (byte)'n', (byte)'s', (byte)'o', (byte)'r' });
                    writer.Write((uint)3); // 3 dimensions
                    writer.Write(ulong.MaxValue / 2); // Dimension 0
                    writer.Write(ulong.MaxValue / 2); // Dimension 1
                    writer.Write((ulong)10); // Dimension 2
                    writer.Write((uint)0); // Tensor type (F32)
                    writer.Write((ulong)0); // Offset
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

        [Theory]
        [InlineData(999)] // Unknown tensor type
        [InlineData(uint.MaxValue)]
        public void GgufReader_InvalidTensorType_HandlesGracefully(uint tensorType)
        {
            // Arrange
            string tempPath = Path.Combine(Path.GetTempPath(), $"fuzz_tensor_type_{Guid.NewGuid()}.gguf");

            try
            {
                using (var fs = File.Create(tempPath))
                using (var writer = new BinaryWriter(fs))
                {
                    writer.Write(ValidMagic);
                    writer.Write(ValidVersion);
                    writer.Write((ulong)1); // 1 tensor
                    writer.Write((ulong)0); // 0 metadata entries

                    // Write tensor with invalid type
                    writer.Write((ulong)6); // Name length
                    writer.Write(new byte[] { (byte)'t', (byte)'e', (byte)'n', (byte)'s', (byte)'o', (byte)'r' });
                    writer.Write((uint)2); // 2 dimensions
                    writer.Write((ulong)10); // Dimension 0
                    writer.Write((ulong)10); // Dimension 1
                    writer.Write(tensorType); // Invalid type
                    writer.Write((ulong)0); // Offset
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

        #endregion

        #region Alignment Edge Cases

        [Theory]
        [InlineData(0)] // Zero alignment
        [InlineData(1)] // Minimal alignment
        [InlineData(3)] // Non-power-of-2
        [InlineData(7)] // Non-power-of-2
        [InlineData(uint.MaxValue)] // Maximum alignment
        public void GgufReader_VariousAlignmentValues_HandlesGracefully(uint alignment)
        {
            // Arrange
            string tempPath = Path.Combine(Path.GetTempPath(), $"fuzz_alignment_{alignment}_{Guid.NewGuid()}.gguf");

            try
            {
                using (var fs = File.Create(tempPath))
                using (var writer = new BinaryWriter(fs))
                {
                    writer.Write(ValidMagic);
                    writer.Write(ValidVersion);
                    writer.Write((ulong)0); // tensor count
                    writer.Write((ulong)1); // 1 metadata entry

                    // Write alignment metadata
                    WriteGgufString(writer, "general.alignment");
                    writer.Write((uint)4); // UInt32 type
                    writer.Write(alignment);
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

        #endregion

        #region Repeated Structure Fuzzing

        [Fact]
        public void GgufReader_DuplicateMetadataKeys_HandlesGracefully()
        {
            // Arrange
            string tempPath = Path.Combine(Path.GetTempPath(), $"fuzz_duplicate_keys_{Guid.NewGuid()}.gguf");

            try
            {
                using (var fs = File.Create(tempPath))
                using (var writer = new BinaryWriter(fs))
                {
                    writer.Write(ValidMagic);
                    writer.Write(ValidVersion);
                    writer.Write((ulong)0); // tensor count
                    writer.Write((ulong)2); // 2 metadata entries

                    // Write two entries with same key
                    for (int i = 0; i < 2; i++)
                    {
                        WriteGgufString(writer, "duplicate.key");
                        writer.Write((uint)4); // UInt32 type
                        writer.Write((uint)i);
                    }
                }

                // Act & Assert - Should handle duplicate keys gracefully
                var report = GgufModelLoader.GetCompatibilityReport(tempPath);
                Assert.NotNull(report);
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }

        [Fact]
        public void GgufReader_DuplicateTensorNames_HandlesGracefully()
        {
            // Arrange
            string tempPath = Path.Combine(Path.GetTempPath(), $"fuzz_duplicate_tensors_{Guid.NewGuid()}.gguf");

            try
            {
                using (var fs = File.Create(tempPath))
                using (var writer = new BinaryWriter(fs))
                {
                    writer.Write(ValidMagic);
                    writer.Write(ValidVersion);
                    writer.Write((ulong)2); // 2 tensors
                    writer.Write((ulong)0); // 0 metadata entries

                    // Write two tensors with same name
                    for (int i = 0; i < 2; i++)
                    {
                        WriteGgufString(writer, "duplicate.tensor");
                        writer.Write((uint)2); // 2 dimensions
                        writer.Write((ulong)10);
                        writer.Write((ulong)10);
                        writer.Write((uint)2); // Q4_0 type
                        writer.Write((ulong)0); // Offset
                    }
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

        #endregion

        #region Mixed Corruption Fuzzing

        [Fact]
        public void GgufReader_PartiallyValidFile_HandlesGracefully()
        {
            // Arrange - Valid header but corrupted after
            string tempPath = Path.Combine(Path.GetTempPath(), $"fuzz_partial_{Guid.NewGuid()}.gguf");
            var random = new Random(123);

            try
            {
                using (var fs = File.Create(tempPath))
                using (var writer = new BinaryWriter(fs))
                {
                    // Write valid header
                    writer.Write(ValidMagic);
                    writer.Write(ValidVersion);
                    writer.Write((ulong)1); // tensor count
                    writer.Write((ulong)1); // metadata count

                    // Write random bytes for the rest
                    byte[] randomData = new byte[256];
                    random.NextBytes(randomData);
                    writer.Write(randomData);
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
        public void GgufReader_AlternatingValidInvalidBytes_HandlesGracefully()
        {
            // Arrange
            string tempPath = Path.Combine(Path.GetTempPath(), $"fuzz_alternating_{Guid.NewGuid()}.gguf");

            try
            {
                using (var fs = File.Create(tempPath))
                using (var writer = new BinaryWriter(fs))
                {
                    writer.Write(ValidMagic);
                    writer.Write(ValidVersion);

                    // Alternate between valid and random bytes
                    for (int i = 0; i < 100; i++)
                    {
                        if (i % 2 == 0)
                            writer.Write((byte)0);
                        else
                            writer.Write((byte)0xFF);
                    }
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

        #endregion

        #region Helper Methods

        private static void WriteGgufString(BinaryWriter writer, string str)
        {
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(str);
            writer.Write((ulong)bytes.Length);
            writer.Write(bytes);
        }

        #endregion
    }
}
