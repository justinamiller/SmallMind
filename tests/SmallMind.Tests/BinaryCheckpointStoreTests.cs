using SmallMind.Core;

namespace SmallMind.Tests
{
    /// <summary>
    /// Tests for binary checkpoint format save/load operations.
    /// </summary>
    public class BinaryCheckpointStoreTests : IDisposable
    {
        private readonly string _testDir;

        public BinaryCheckpointStoreTests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "SmallMindTests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, recursive: true);
            }
        }

        [Fact]
        public async Task SaveAndLoad_RoundTrip_PreservesAllData()
        {
            // Arrange
            var store = new BinaryCheckpointStore();
            var path = Path.Combine(_testDir, "test.smnd");

            var checkpoint = new ModelCheckpoint
            {
                FormatVersion = 1,
                Metadata = new ModelMetadata
                {
                    ModelType = "TransformerModel",
                    VocabSize = 256,
                    BlockSize = 128,
                    EmbedDim = 384,
                    NumHeads = 6,
                    NumLayers = 6,
                    FfnHiddenDim = 1536
                },
                Parameters =
                {
                    new TensorData { Shape = new[] { 256, 384 }, Data = CreateTestData(256 * 384, seed: 42) },
                    new TensorData { Shape = new[] { 128, 384 }, Data = CreateTestData(128 * 384, seed: 43) },
                    new TensorData { Shape = new[] { 384 }, Data = CreateTestData(384, seed: 44) }
                }
            };

            // Act
            await store.SaveAsync(checkpoint, path);
            var loaded = await store.LoadAsync(path);

            // Assert
            Assert.Equal(checkpoint.FormatVersion, loaded.FormatVersion);
            Assert.Equal(checkpoint.Metadata.ModelType, loaded.Metadata.ModelType);
            Assert.Equal(checkpoint.Metadata.VocabSize, loaded.Metadata.VocabSize);
            Assert.Equal(checkpoint.Metadata.BlockSize, loaded.Metadata.BlockSize);
            Assert.Equal(checkpoint.Metadata.EmbedDim, loaded.Metadata.EmbedDim);
            Assert.Equal(checkpoint.Metadata.NumHeads, loaded.Metadata.NumHeads);
            Assert.Equal(checkpoint.Metadata.NumLayers, loaded.Metadata.NumLayers);

            Assert.Equal(checkpoint.Parameters.Count, loaded.Parameters.Count);

            for (int i = 0; i < checkpoint.Parameters.Count; i++)
            {
                var orig = checkpoint.Parameters[i];
                var loadedParam = loaded.Parameters[i];

                Assert.Equal(orig.Shape.Length, loadedParam.Shape.Length);
                for (int j = 0; j < orig.Shape.Length; j++)
                {
                    Assert.Equal(orig.Shape[j], loadedParam.Shape[j]);
                }

                Assert.Equal(orig.Data.Length, loadedParam.Data.Length);
                for (int j = 0; j < orig.Data.Length; j++)
                {
                    Assert.Equal(orig.Data[j], loadedParam.Data[j], precision: 6);
                }
            }
        }

        [Fact]
        public async Task Load_InvalidMagicHeader_ThrowsInvalidDataException()
        {
            // Arrange
            var store = new BinaryCheckpointStore();
            var path = Path.Combine(_testDir, "invalid.smnd");

            // Write invalid magic header
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("BADM")); // Wrong magic
                writer.Write(1); // Version
                writer.Write(0L); // Reserved
            }

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidDataException>(() => store.LoadAsync(path));
            Assert.Contains("bad magic header", ex.Message);
        }

        [Fact]
        public async Task Load_NewerFormatVersion_ThrowsInvalidDataException()
        {
            // Arrange
            var store = new BinaryCheckpointStore();
            var path = Path.Combine(_testDir, "newer.smnd");

            // Write header with future version
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("SMND"));
                writer.Write(999); // Future version
                writer.Write(0L); // Reserved
            }

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidDataException>(() => store.LoadAsync(path));
            Assert.Contains("newer than supported", ex.Message);
        }

        [Fact]
        public async Task Load_NonExistentFile_ThrowsFileNotFoundException()
        {
            // Arrange
            var store = new BinaryCheckpointStore();
            var path = Path.Combine(_testDir, "nonexistent.smnd");

            // Act & Assert
            await Assert.ThrowsAsync<FileNotFoundException>(() => store.LoadAsync(path));
        }

        [Fact]
        public async Task SaveAsync_NullCheckpoint_ThrowsArgumentNullException()
        {
            // Arrange
            var store = new BinaryCheckpointStore();
            var path = Path.Combine(_testDir, "test.smnd");

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => store.SaveAsync(null!, path));
        }

        [Fact]
        public async Task SaveAsync_EmptyPath_ThrowsArgumentException()
        {
            // Arrange
            var store = new BinaryCheckpointStore();
            var checkpoint = new ModelCheckpoint();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => store.SaveAsync(checkpoint, ""));
        }

        [Fact]
        public async Task BinaryFormat_IsSmallerThanJson()
        {
            // Arrange
            var binaryStore = new BinaryCheckpointStore();
            var jsonStore = new JsonCheckpointStore();

            var binaryPath = Path.Combine(_testDir, "test.smnd");
            var jsonPath = Path.Combine(_testDir, "test.json");

            var checkpoint = new ModelCheckpoint
            {
                FormatVersion = 1,
                Metadata = new ModelMetadata
                {
                    ModelType = "TransformerModel",
                    VocabSize = 256,
                    BlockSize = 128,
                    EmbedDim = 384,
                    NumHeads = 6,
                    NumLayers = 6
                },
                Parameters =
                {
                    new TensorData { Shape = new[] { 256, 384 }, Data = CreateTestData(256 * 384, seed: 42) },
                    new TensorData { Shape = new[] { 384 }, Data = CreateTestData(384, seed: 43) }
                }
            };

            // Act
            await binaryStore.SaveAsync(checkpoint, binaryPath);
            await jsonStore.SaveAsync(checkpoint, jsonPath);

            var binarySize = new FileInfo(binaryPath).Length;
            var jsonSize = new FileInfo(jsonPath).Length;

            // Assert - Binary should be significantly smaller
            Assert.True(binarySize < jsonSize * 0.6,
                $"Binary size ({binarySize} bytes) should be < 60% of JSON size ({jsonSize} bytes)");
        }

        [Fact]
        public async Task JsonStore_CanLoadLegacyFormat()
        {
            // Arrange
            var jsonStore = new JsonCheckpointStore();
            var path = Path.Combine(_testDir, "legacy.json");

            var checkpoint = new ModelCheckpoint
            {
                FormatVersion = 0, // Legacy format
                Parameters =
                {
                    new TensorData { Shape = new[] { 10, 20 }, Data = CreateTestData(200, seed: 1) }
                }
            };

            // Act
            await jsonStore.SaveAsync(checkpoint, path);
            var loaded = await jsonStore.LoadAsync(path);

            // Assert
            Assert.Equal(200, loaded.Parameters[0].Data.Length);
        }

        [Fact]
        public async Task BinaryStore_PreservesFloatPrecision()
        {
            // Arrange
            var store = new BinaryCheckpointStore();
            var path = Path.Combine(_testDir, "precision.smnd");

            var testValues = new float[]
            {
                1.23456789f,
                -0.00000001f,
                float.MaxValue,
                float.MinValue,
                float.Epsilon,
                3.14159265f
            };

            var checkpoint = new ModelCheckpoint
            {
                FormatVersion = 1,
                Metadata = new ModelMetadata { ModelType = "Test" },
                Parameters =
                {
                    new TensorData { Shape = new[] { testValues.Length }, Data = testValues }
                }
            };

            // Act
            await store.SaveAsync(checkpoint, path);
            var loaded = await store.LoadAsync(path);

            // Assert
            for (int i = 0; i < testValues.Length; i++)
            {
                Assert.Equal(testValues[i], loaded.Parameters[0].Data[i]);
            }
        }

        [Fact]
        public async Task MultiDimensionalTensors_RoundTripCorrectly()
        {
            // Arrange
            var store = new BinaryCheckpointStore();
            var path = Path.Combine(_testDir, "multidim.smnd");

            var checkpoint = new ModelCheckpoint
            {
                FormatVersion = 1,
                Metadata = new ModelMetadata { ModelType = "Test" },
                Parameters =
                {
                    new TensorData { Shape = new[] { 2, 3, 4 }, Data = CreateTestData(24, seed: 1) },
                    new TensorData { Shape = new[] { 5 }, Data = CreateTestData(5, seed: 2) },
                    new TensorData { Shape = new[] { 10, 20, 30, 40 }, Data = CreateTestData(240000, seed: 3) }
                }
            };

            // Act
            await store.SaveAsync(checkpoint, path);
            var loaded = await store.LoadAsync(path);

            // Assert
            Assert.Equal(3, loaded.Parameters.Count);
            Assert.Equal(new[] { 2, 3, 4 }, loaded.Parameters[0].Shape);
            Assert.Equal(new[] { 5 }, loaded.Parameters[1].Shape);
            Assert.Equal(new[] { 10, 20, 30, 40 }, loaded.Parameters[2].Shape);
        }

        private float[] CreateTestData(int count, int seed)
        {
            var random = new Random(seed);
            var data = new float[count];
            for (int i = 0; i < count; i++)
            {
                data[i] = (float)(random.NextDouble() * 2.0 - 1.0); // Range [-1, 1]
            }
            return data;
        }
    }
}
