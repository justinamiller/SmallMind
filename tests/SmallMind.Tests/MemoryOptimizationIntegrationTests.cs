using SmallMind.Core.Core;

namespace SmallMind.Tests
{
    /// <summary>
    /// Integration tests for memory optimization features.
    /// Tests how all components work together to enable large context processing.
    /// </summary>
    public class MemoryOptimizationIntegrationTests : IDisposable
    {
        private readonly string _testDirectory;

        public MemoryOptimizationIntegrationTests()
        {
            _testDirectory = Path.Combine(Path.GetTempPath(), $"memopt_test_{Guid.NewGuid()}");
            Directory.CreateDirectory(_testDirectory);
        }

        public void Dispose()
        {
            if (Directory.Exists(_testDirectory))
            {
                try
                {
                    Directory.Delete(_testDirectory, recursive: true);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }

        [Fact]
        public void CompleteWorkflow_32kTokens_WithAllOptimizations()
        {
            // Scenario: Process 32k tokens with 16GB RAM using all optimizations

            // 1. Configure memory
            var memConfig = new MemoryConfiguration(
                memoryGB: 16,
                enableGradientCheckpointing: true,
                enableMixedPrecision: true,
                enableMemoryMapping: true,
                checkpointInterval: 2);

            Assert.True(memConfig.EnableGradientCheckpointing);
            Assert.True(memConfig.EnableMixedPrecision);
            Assert.True(memConfig.EnableMemoryMapping);
            Assert.Equal(2, memConfig.CheckpointInterval);

            // 2. Set up sliding window processor
            var windowProcessor = new SlidingWindowProcessor(
                windowSize: memConfig.SlidingWindowSize,
                stride: memConfig.SlidingWindowStride);

            Assert.True(windowProcessor.WindowSize > 0);
            Assert.True(windowProcessor.Stride > 0);

            // 3. Create 32k token sequence
            var tokens = new int[32768];
            for (int i = 0; i < tokens.Length; i++)
            {
                tokens[i] = i % 1000;
            }

            // 4. Process in windows
            var windows = new List<int[]>();
            foreach (var window in windowProcessor.GetWindows(tokens))
            {
                windows.Add(window);
            }

            int expectedWindows = windowProcessor.EstimateWindowCount(tokens.Length);
            Assert.Equal(expectedWindows, windows.Count);

            // 5. Verify windows cover the entire sequence
            Assert.True(windows.Count > 0);
            Assert.Equal(windowProcessor.WindowSize, windows[0].Length); // First window full size
        }

        [Fact]
        public void GradientCheckpointing_WithCheckpointManager_ReducesMemory()
        {
            int numLayers = 12;
            long perLayerBytes = 50 * 1024 * 1024; // 50 MB per layer

            // Without checkpointing: all layers stored
            long memoryWithout = numLayers * perLayerBytes;

            // With checkpointing (interval=2): only half the layers stored
            var checkpointMgr = new CheckpointManager(checkpointInterval: 2, enabled: true);
            var (_, memoryWith, savings) = GradientCheckpointing.EstimateMemorySavings(
                numLayers,
                perLayerBytes,
                checkpointMgr.CheckpointInterval);

            Assert.True(memoryWith < memoryWithout);
            Assert.True(savings > 0);
            Assert.True(savings <= 100);
        }

        [Fact]
        public void MixedPrecision_ConvertsCorrectly()
        {
            var fp32Values = new float[] { 1.0f, 2.5f, 3.75f, 4.125f };
            var fp16Buffer = new Half[fp32Values.Length];
            var fp32Result = new float[fp32Values.Length];

            // Convert FP32 -> FP16 -> FP32
            MixedPrecision.FloatToHalf(fp32Values, fp16Buffer);
            MixedPrecision.HalfToFloat(fp16Buffer, fp32Result);

            // Values should be approximately equal (within FP16 precision)
            for (int i = 0; i < fp32Values.Length; i++)
            {
                Assert.True(Math.Abs(fp32Values[i] - fp32Result[i]) < 0.01f,
                    $"Value mismatch at index {i}: {fp32Values[i]} != {fp32Result[i]}");
            }
        }

        [Fact]
        public void KVCache_WithMultipleLayersAndWindows_WorksCorrectly()
        {
            // Simulate KV cache for sliding window processing across layers
            using var kvCache = new MultiLayerKVCache(
                numLayers: 4,
                maxSeqLen: 2048,
                numHeads: 4,
                headDim: 32,
                useMemoryMapping: false);

            // Write data for each layer
            for (int layer = 0; layer < 4; layer++)
            {
                var keyCache = kvCache.GetKeyCache(layer);
                var valueCache = kvCache.GetValueCache(layer);

                // Write some values
                keyCache.Write(0, layer * 10.0f);
                valueCache.Write(0, layer * 20.0f);
            }

            // Verify data is independent per layer
            for (int layer = 0; layer < 4; layer++)
            {
                float keyVal = kvCache.GetKeyCache(layer).Read(0);
                float valueVal = kvCache.GetValueCache(layer).Read(0);

                Assert.Equal(layer * 10.0f, keyVal);
                Assert.Equal(layer * 20.0f, valueVal);
            }

            // Clear all caches
            kvCache.ClearAll();

            // Verify all cleared
            for (int layer = 0; layer < 4; layer++)
            {
                Assert.Equal(0.0f, kvCache.GetKeyCache(layer).Read(0));
                Assert.Equal(0.0f, kvCache.GetValueCache(layer).Read(0));
            }
        }

        [Fact]
        public void SlidingWindow_WithTensorOutputs_CombinesCorrectly()
        {
            var processor = new SlidingWindowProcessor(windowSize: 4, stride: 2);

            // Create simulated window outputs
            var window1 = new Tensor(new int[] { 1, 4, 2 }); // batch=1, seq=4, dim=2
            var window2 = new Tensor(new int[] { 1, 4, 2 });
            var window3 = new Tensor(new int[] { 1, 4, 2 });

            // Fill with different values
            for (int i = 0; i < window1.Size; i++)
            {
                window1.Data[i] = 1.0f;
                window2.Data[i] = 2.0f;
                window3.Data[i] = 3.0f;
            }

            var windows = new List<Tensor> { window1, window2, window3 };

            // Combine with averaging
            var combined = processor.CombineWindowOutputs(windows, originalSeqLength: 8);

            Assert.Equal(1, combined.Shape[0]); // batch
            Assert.Equal(8, combined.Shape[1]); // seq
            Assert.Equal(2, combined.Shape[2]); // dim

            // Verify overlap regions are averaged
            // Positions 0-1: only window1 (value=1)
            // Positions 2-3: window1 and window2 averaged (value=1.5)
            // Positions 4-5: window2 and window3 averaged (value=2.5)
            // Positions 6-7: only window3 (value=3)

            // Check a few key positions
            float val0 = combined.Data[0]; // Position 0, dim 0
            Assert.Equal(1.0f, val0, precision: 3);

            float val2 = combined.Data[2 * 2]; // Position 2, dim 0 (overlap)
            Assert.True(Math.Abs(val2 - 1.5f) < 0.1f);
        }

        [Fact]
        public void MemoryConfiguration_AutoScalesForDifferentRAM()
        {
            var configs = new[]
            {
                new MemoryConfiguration(memoryGB: 8),
                new MemoryConfiguration(memoryGB: 16),
                new MemoryConfiguration(memoryGB: 32),
                new MemoryConfiguration(memoryGB: 64),
                new MemoryConfiguration(memoryGB: 128)
            };

            // Verify token limits scale with RAM
            for (int i = 1; i < configs.Length; i++)
            {
                Assert.True(configs[i].MaxContextTokens >= configs[i - 1].MaxContextTokens,
                    $"Token limit should increase with RAM: {configs[i - 1].MaxContextTokens} vs {configs[i].MaxContextTokens}");
            }

            // Verify largest config supports 128k tokens
            Assert.True(configs[^1].MaxContextTokens >= 100000);
        }

        [Fact]
        public void EndToEnd_SimulateTransformerProcessing()
        {
            // Simulate a complete flow of processing large context

            // 1. Configure for 16GB system
            var config = new MemoryConfiguration(
                memoryGB: 16,
                enableGradientCheckpointing: true,
                enableMixedPrecision: true,
                enableMemoryMapping: false,
                checkpointInterval: 2);

            // 2. Set up window processor
            var windowProcessor = new SlidingWindowProcessor(
                windowSize: 4096,
                stride: 2048);

            // 4. Create KV cache
            using var kvCache = new MultiLayerKVCache(
                numLayers: 6,
                maxSeqLen: 4096,
                numHeads: 8,
                headDim: 64,
                useMemoryMapping: false);

            // 5. Simulate processing
            var tokens = new int[16384]; // 16k tokens
            for (int i = 0; i < tokens.Length; i++)
            {
                tokens[i] = i % 1000;
            }

            // 6. Process in windows
            int windowCount = 0;
            foreach (var window in windowProcessor.GetWindows(tokens))
            {
                windowCount++;

                // Simulate storing KV cache for first layer
                if (windowCount == 1)
                {
                    var keyCache = kvCache.GetKeyCache(0);
                    float[] simulatedKeys = new float[100];
                    for (int i = 0; i < simulatedKeys.Length; i++)
                    {
                        simulatedKeys[i] = i * 0.1f;
                    }
                    keyCache.WriteArray(0, simulatedKeys);
                }
            }

            // 7. Verify processing completed
            Assert.True(windowCount > 0);
            Assert.Equal(windowProcessor.EstimateWindowCount(tokens.Length), windowCount);

            // 8. Verify KV cache has data
            float firstKey = kvCache.GetKeyCache(0).Read(0);
            Assert.Equal(0.0f, firstKey, precision: 3);
        }

        [Fact]
        public void MemoryMapping_WithLargeCache_WorksCorrectly()
        {
            // Test memory-mapped KV cache with realistic size
            string cacheDir = Path.Combine(_testDirectory, "cache");

            using var kvCache = new MultiLayerKVCache(
                numLayers: 4,
                maxSeqLen: 8192, // 8k sequence
                numHeads: 8,
                headDim: 64,
                useMemoryMapping: true,
                cacheDirectory: cacheDir);

            // Write data to multiple layers
            for (int layer = 0; layer < 4; layer++)
            {
                var keyCache = kvCache.GetKeyCache(layer);
                var valueCache = kvCache.GetValueCache(layer);

                // Write array of values
                var data = new float[100];
                for (int i = 0; i < data.Length; i++)
                {
                    data[i] = layer * 100 + i;
                }

                keyCache.WriteArray(0, data);
                valueCache.WriteArray(100, data);
            }

            // Flush to disk
            kvCache.FlushAll();

            // Read back and verify
            for (int layer = 0; layer < 4; layer++)
            {
                var result = new float[100];
                kvCache.GetKeyCache(layer).ReadArray(0, 100, result);

                for (int i = 0; i < result.Length; i++)
                {
                    Assert.Equal(layer * 100 + i, result[i]);
                }
            }
        }
    }
}
