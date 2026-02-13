using SmallMind.Quantization.IO.Smq;
using SmallMind.Quantization.Tensors;

namespace SmallMind.Quantization.Tests
{
    /// <summary>
    /// Tests for SMQ format writer, reader, and validator.
    /// Validates round-trip serialization and error detection.
    /// </summary>
    public class SmqRoundTripTests
    {
        [Fact]
        public void SmqRoundTrip_Q8Tensor_PreservesData()
        {
            // Arrange
            var random = new Random(42);
            int rows = 8, cols = 16;
            var source = GenerateRandomFloats(random, rows * cols, -10f, 10f);
            var originalTensor = Q8Tensor.Quantize(source, rows, cols, blockSize: 8);

            var tensors = new Dictionary<string, object>
            {
                { "test.weight", originalTensor }
            };

            // Act: Write to memory stream
            using var stream = new MemoryStream();
            using (var writer = new SmqWriter(stream, leaveOpen: true))
            {
                long bytesWritten = writer.WriteModel(tensors);
                Assert.True(bytesWritten > 0);
            }

            // Act: Read back
            stream.Position = 0;
            using var reader = new SmqReader(stream, leaveOpen: true);
            reader.ReadHeader();
            var loadedTensor = (Q8Tensor)reader.LoadTensor("test.weight");

            // Assert
            Assert.Equal(originalTensor.Rows, loadedTensor.Rows);
            Assert.Equal(originalTensor.Cols, loadedTensor.Cols);
            Assert.Equal(originalTensor.BlockSize, loadedTensor.BlockSize);
            Assert.Equal(originalTensor.Data.Length, loadedTensor.Data.Length);
            Assert.Equal(originalTensor.Scales.Length, loadedTensor.Scales.Length);

            for (int i = 0; i < originalTensor.Data.Length; i++)
            {
                Assert.Equal(originalTensor.Data[i], loadedTensor.Data[i]);
            }

            for (int i = 0; i < originalTensor.Scales.Length; i++)
            {
                Assert.Equal(originalTensor.Scales[i], loadedTensor.Scales[i], precision: 6);
            }
        }

        [Fact]
        public void SmqRoundTrip_Q4Tensor_PreservesData()
        {
            // Arrange
            var random = new Random(42);
            int rows = 8, cols = 16;
            var source = GenerateRandomFloats(random, rows * cols, -5f, 5f);
            var originalTensor = Q4Tensor.Quantize(source, rows, cols, blockSize: 8);

            var tensors = new Dictionary<string, object>
            {
                { "test.weight", originalTensor }
            };

            // Act: Write to memory stream
            using var stream = new MemoryStream();
            using (var writer = new SmqWriter(stream, leaveOpen: true))
            {
                long bytesWritten = writer.WriteModel(tensors);
                Assert.True(bytesWritten > 0);
            }

            // Act: Read back
            stream.Position = 0;
            using var reader = new SmqReader(stream, leaveOpen: true);
            reader.ReadHeader();
            var loadedTensor = (Q4Tensor)reader.LoadTensor("test.weight");

            // Assert
            Assert.Equal(originalTensor.Rows, loadedTensor.Rows);
            Assert.Equal(originalTensor.Cols, loadedTensor.Cols);
            Assert.Equal(originalTensor.BlockSize, loadedTensor.BlockSize);
            Assert.Equal(originalTensor.Data.Length, loadedTensor.Data.Length);
            Assert.Equal(originalTensor.Scales.Length, loadedTensor.Scales.Length);

            for (int i = 0; i < originalTensor.Data.Length; i++)
            {
                Assert.Equal(originalTensor.Data[i], loadedTensor.Data[i]);
            }

            for (int i = 0; i < originalTensor.Scales.Length; i++)
            {
                Assert.Equal(originalTensor.Scales[i], loadedTensor.Scales[i], precision: 6);
            }
        }

        [Fact]
        public void SmqRoundTrip_MixedQ8Q4Model_PreservesAllTensors()
        {
            // Arrange
            var random = new Random(42);

            var q8Source = GenerateRandomFloats(random, 4 * 8, -10f, 10f);
            var q8Tensor = Q8Tensor.Quantize(q8Source, 4, 8, blockSize: 4);

            var q4Source = GenerateRandomFloats(random, 8 * 16, -5f, 5f);
            var q4Tensor = Q4Tensor.Quantize(q4Source, 8, 16, blockSize: 8);

            var tensors = new Dictionary<string, object>
            {
                { "layer1.wq", q8Tensor },
                { "layer1.wk", q4Tensor }
            };

            var metadata = new Dictionary<string, object>
            {
                { "model_name", "test_model" },
                { "version", 1 }
            };

            // Act: Write
            using var stream = new MemoryStream();
            using (var writer = new SmqWriter(stream, leaveOpen: true))
            {
                writer.WriteModel(tensors, metadata);
            }

            // Act: Read
            stream.Position = 0;
            using var reader = new SmqReader(stream, leaveOpen: true);
            reader.ReadHeader();

            var tensorNames = new List<string>(reader.GetTensorNames());
            Assert.Equal(2, tensorNames.Count);
            Assert.Contains("layer1.wq", tensorNames);
            Assert.Contains("layer1.wk", tensorNames);

            var loadedQ8 = (Q8Tensor)reader.LoadTensor("layer1.wq");
            var loadedQ4 = (Q4Tensor)reader.LoadTensor("layer1.wk");

            // Assert Q8
            Assert.Equal(q8Tensor.Rows, loadedQ8.Rows);
            Assert.Equal(q8Tensor.Cols, loadedQ8.Cols);

            // Assert Q4
            Assert.Equal(q4Tensor.Rows, loadedQ4.Rows);
            Assert.Equal(q4Tensor.Cols, loadedQ4.Cols);
        }

        [Fact]
        public void SmqRoundTrip_Metadata_RoundTrips()
        {
            // Arrange
            var random = new Random(42);
            var source = GenerateRandomFloats(random, 4 * 4, -1f, 1f);
            var tensor = Q8Tensor.Quantize(source, 4, 4, blockSize: 4);

            var tensors = new Dictionary<string, object>
            {
                { "test", tensor }
            };

            var metadata = new Dictionary<string, object>
            {
                { "model_name", "test_model" },
                { "vocab_size", 1000 },
                { "n_layers", 12 }
            };

            // Act: Write
            using var stream = new MemoryStream();
            using (var writer = new SmqWriter(stream, leaveOpen: true))
            {
                writer.WriteModel(tensors, metadata);
            }

            // Act: Read
            stream.Position = 0;
            using var reader = new SmqReader(stream, leaveOpen: true);
            reader.ReadHeader();
            var loadedMetadata = reader.GetMetadata();

            // Assert
            Assert.NotNull(loadedMetadata);
            Assert.True(loadedMetadata.ContainsKey("model_name"));
            Assert.True(loadedMetadata.ContainsKey("vocab_size"));
            Assert.True(loadedMetadata.ContainsKey("n_layers"));
        }

        [Fact]
        public void SmqReader_LoadAllTensors_ReturnsAllTensors()
        {
            // Arrange
            var random = new Random(42);
            var q8 = Q8Tensor.Quantize(GenerateRandomFloats(random, 16, -1f, 1f), 4, 4, blockSize: 4);
            var q4 = Q4Tensor.Quantize(GenerateRandomFloats(random, 16, -1f, 1f), 4, 4, blockSize: 4);

            var tensors = new Dictionary<string, object>
            {
                { "weight1", q8 },
                { "weight2", q4 }
            };

            // Act: Write
            using var stream = new MemoryStream();
            using (var writer = new SmqWriter(stream, leaveOpen: true))
            {
                writer.WriteModel(tensors);
            }

            // Act: Read all
            stream.Position = 0;
            using var reader = new SmqReader(stream, leaveOpen: true);
            reader.ReadHeader();
            var allTensors = reader.LoadAllTensors();

            // Assert
            Assert.Equal(2, allTensors.Count);
            Assert.True(allTensors.ContainsKey("weight1"));
            Assert.True(allTensors.ContainsKey("weight2"));
            Assert.IsType<Q8Tensor>(allTensors["weight1"]);
            Assert.IsType<Q4Tensor>(allTensors["weight2"]);
        }

        [Fact]
        public void SmqReader_TryLoadTensor_ReturnsFalseForMissingTensor()
        {
            // Arrange
            var random = new Random(42);
            var tensor = Q8Tensor.Quantize(GenerateRandomFloats(random, 16, -1f, 1f), 4, 4, blockSize: 4);
            var tensors = new Dictionary<string, object> { { "existing", tensor } };

            using var stream = new MemoryStream();
            using (var writer = new SmqWriter(stream, leaveOpen: true))
            {
                writer.WriteModel(tensors);
            }

            // Act
            stream.Position = 0;
            using var reader = new SmqReader(stream, leaveOpen: true);
            reader.ReadHeader();

            bool foundExisting = reader.TryLoadTensor("existing", out var loadedExisting);
            bool foundMissing = reader.TryLoadTensor("missing", out var loadedMissing);

            // Assert
            Assert.True(foundExisting);
            Assert.NotNull(loadedExisting);
            Assert.False(foundMissing);
            Assert.Null(loadedMissing);
        }

        [Fact]
        public void SmqValidator_ValidFile_ReturnsNoErrors()
        {
            // Arrange: Create valid SMQ file
            var random = new Random(42);
            var tensor = Q8Tensor.Quantize(GenerateRandomFloats(random, 16, -1f, 1f), 4, 4, blockSize: 4);
            var tensors = new Dictionary<string, object> { { "test", tensor } };

            using var stream = new MemoryStream();
            using (var writer = new SmqWriter(stream, leaveOpen: true))
            {
                writer.WriteModel(tensors);
            }

            // Act: Validate
            stream.Position = 0;
            var errors = SmqValidator.Validate(stream);

            // Assert
            Assert.Empty(errors);
        }

        [Fact]
        public void SmqValidator_InvalidMagicHeader_ReturnsError()
        {
            // Arrange: Create stream with invalid magic
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("BADMAGIC")); // Wrong magic
                writer.Write((uint)1); // Version
                writer.Write((uint)32); // Header size
                writer.Write((uint)0); // Tensor count
                writer.Write((uint)0); // Metadata length
                writer.Write(0UL); // Reserved
            }

            // Act
            stream.Position = 0;
            var errors = SmqValidator.Validate(stream);

            // Assert
            Assert.NotEmpty(errors);
            Assert.Contains(errors, e => e.Message.Contains("Invalid magic header"));
        }

        [Fact]
        public void SmqValidator_WrongVersion_ReturnsError()
        {
            // Arrange: Create stream with wrong version
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("SMQv0001")); // Correct magic
                writer.Write((uint)999); // Wrong version
                writer.Write((uint)32);
                writer.Write((uint)0);
                writer.Write((uint)0);
                writer.Write(0UL);
            }

            // Act
            stream.Position = 0;
            var errors = SmqValidator.Validate(stream);

            // Assert
            Assert.NotEmpty(errors);
            Assert.Contains(errors, e => e.Message.Contains("Unsupported format version"));
        }

        [Fact]
        public void SmqValidator_TruncatedFile_ReturnsError()
        {
            // Arrange: Create truncated file
            using var stream = new MemoryStream();
            stream.Write(new byte[10]); // Too short for header

            // Act
            stream.Position = 0;
            var errors = SmqValidator.Validate(stream);

            // Assert
            Assert.NotEmpty(errors);
            Assert.Contains(errors, e => e.Message.Contains("File too small"));
        }

        [Fact]
        public void SmqReader_InvalidMagicHeader_ThrowsException()
        {
            // Arrange
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("BADMAGIC"));
                writer.Write((uint)1);
                writer.Write((uint)32);
                writer.Write((uint)0);
                writer.Write((uint)0);
                writer.Write(0UL);
            }

            // Act & Assert
            stream.Position = 0;
            using var reader = new SmqReader(stream, leaveOpen: true);
            Assert.Throws<InvalidDataException>(() => reader.ReadHeader());
        }

        [Fact]
        public void SmqReader_TensorNotFound_ThrowsException()
        {
            // Arrange
            var random = new Random(42);
            var tensor = Q8Tensor.Quantize(GenerateRandomFloats(random, 16, -1f, 1f), 4, 4, blockSize: 4);
            var tensors = new Dictionary<string, object> { { "exists", tensor } };

            using var stream = new MemoryStream();
            using (var writer = new SmqWriter(stream, leaveOpen: true))
            {
                writer.WriteModel(tensors);
            }

            // Act & Assert
            stream.Position = 0;
            using var reader = new SmqReader(stream, leaveOpen: true);
            reader.ReadHeader();
            Assert.Throws<ArgumentException>(() => reader.LoadTensor("does_not_exist"));
        }

        [Fact]
        public void SmqReader_BeforeReadHeader_ThrowsException()
        {
            // Arrange
            using var stream = new MemoryStream();
            using var reader = new SmqReader(stream, leaveOpen: true);

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => reader.LoadTensor("test"));
            Assert.Throws<InvalidOperationException>(() => reader.GetMetadata());
            Assert.Throws<InvalidOperationException>(() => _ = reader.GetTensorNames());
        }

        [Fact]
        public void SmqRoundTrip_LongTensorName_PreservesName()
        {
            // Arrange
            var random = new Random(42);
            var tensor = Q8Tensor.Quantize(GenerateRandomFloats(random, 16, -1f, 1f), 4, 4, blockSize: 4);
            string longName = "model.layers.0.attention.self_attn.query_proj.weight";
            var tensors = new Dictionary<string, object> { { longName, tensor } };

            // Act
            using var stream = new MemoryStream();
            using (var writer = new SmqWriter(stream, leaveOpen: true))
            {
                writer.WriteModel(tensors);
            }

            stream.Position = 0;
            using var reader = new SmqReader(stream, leaveOpen: true);
            reader.ReadHeader();
            var names = new List<string>(reader.GetTensorNames());

            // Assert
            Assert.Single(names);
            Assert.Equal(longName, names[0]);
        }

        [Fact]
        public void SmqRoundTrip_OddDimensions_HandlesCorrectly()
        {
            // Arrange: Test non-power-of-2 dimensions
            var random = new Random(42);
            int rows = 7, cols = 13; // Odd dimensions
            var source = GenerateRandomFloats(random, rows * cols, -1f, 1f);
            var tensor = Q8Tensor.Quantize(source, rows, cols, blockSize: 5);

            var tensors = new Dictionary<string, object> { { "odd", tensor } };

            // Act
            using var stream = new MemoryStream();
            using (var writer = new SmqWriter(stream, leaveOpen: true))
            {
                writer.WriteModel(tensors);
            }

            stream.Position = 0;
            using var reader = new SmqReader(stream, leaveOpen: true);
            reader.ReadHeader();
            var loaded = (Q8Tensor)reader.LoadTensor("odd");

            // Assert
            Assert.Equal(rows, loaded.Rows);
            Assert.Equal(cols, loaded.Cols);
            Assert.Equal(rows * cols, loaded.Data.Length);
        }

        // Helper methods

        private static float[] GenerateRandomFloats(Random random, int count, float min, float max)
        {
            var result = new float[count];
            float range = max - min;
            for (int i = 0; i < count; i++)
            {
                result[i] = (float)random.NextDouble() * range + min;
            }
            return result;
        }
    }
}
