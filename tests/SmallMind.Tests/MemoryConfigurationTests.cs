using SmallMind.Core.Core;

namespace SmallMind.Tests
{
    public class MemoryConfigurationTests
    {
        [Fact]
        public void Constructor_AutoDetect_CreatesConfiguration()
        {
            var config = new MemoryConfiguration();

            Assert.True(config.SystemMemoryBytes > 0);
            Assert.True(config.SystemMemoryGB > 0);
            Assert.True(config.MaxContextTokens >= MemoryConfiguration.MIN_SAFE_TOKENS);
        }

        [Fact]
        public void Constructor_With16GB_SetsCorrectTokens()
        {
            var config = new MemoryConfiguration(memoryGB: 16);

            Assert.Equal(MemoryConfiguration.DEFAULT_MAX_TOKENS_16GB, config.MaxContextTokens);
            Assert.Equal(16.0 * 1024 * 1024 * 1024, config.SystemMemoryBytes, tolerance: 1.0);
        }

        [Fact]
        public void Constructor_With32GB_SetsCorrectTokens()
        {
            var config = new MemoryConfiguration(memoryGB: 32);

            Assert.Equal(MemoryConfiguration.DEFAULT_MAX_TOKENS_32GB, config.MaxContextTokens);
        }

        [Fact]
        public void Constructor_With64GB_SetsCorrectTokens()
        {
            var config = new MemoryConfiguration(memoryGB: 64);

            Assert.Equal(MemoryConfiguration.DEFAULT_MAX_TOKENS_64GB, config.MaxContextTokens);
        }

        [Fact]
        public void Constructor_With128GB_SetsCorrectTokens()
        {
            var config = new MemoryConfiguration(memoryGB: 128);

            Assert.Equal(MemoryConfiguration.DEFAULT_MAX_TOKENS_128GB, config.MaxContextTokens);
        }

        [Fact]
        public void Constructor_WithLowMemory_ScalesAppropriately()
        {
            var config = new MemoryConfiguration(memoryGB: 8);

            // 8GB should give about 8 * 2048 = 16384 tokens
            Assert.True(config.MaxContextTokens >= MemoryConfiguration.MIN_SAFE_TOKENS);
            Assert.True(config.MaxContextTokens < MemoryConfiguration.DEFAULT_MAX_TOKENS_16GB);
        }

        [Fact]
        public void Constructor_WithCustomTokens_UsesCustomValue()
        {
            var config = new MemoryConfiguration(
                enableGradientCheckpointing: true,
                enableMixedPrecision: false,
                enableMemoryMapping: false,
                checkpointInterval: 2,
                customMaxTokens: 16384);

            Assert.Equal(16384, config.MaxContextTokens);
        }

        [Fact]
        public void Constructor_GradientCheckpointingEnabled_SetsCorrectly()
        {
            var config = new MemoryConfiguration(
                enableGradientCheckpointing: true,
                checkpointInterval: 4);

            Assert.True(config.EnableGradientCheckpointing);
            Assert.Equal(4, config.CheckpointInterval);
        }

        [Fact]
        public void Constructor_MixedPrecisionEnabled_SetsCorrectly()
        {
            var config = new MemoryConfiguration(
                enableMixedPrecision: true);

            Assert.True(config.EnableMixedPrecision);
        }

        [Fact]
        public void Constructor_MemoryMappingEnabled_SetsCorrectly()
        {
            var config = new MemoryConfiguration(
                enableMemoryMapping: true);

            Assert.True(config.EnableMemoryMapping);
        }

        [Fact]
        public void Constructor_InvalidCheckpointInterval_ThrowsException()
        {
            Assert.Throws<SmallMind.Core.Exceptions.ValidationException>(() =>
                new MemoryConfiguration(checkpointInterval: 0));

            Assert.Throws<SmallMind.Core.Exceptions.ValidationException>(() =>
                new MemoryConfiguration(checkpointInterval: -1));
        }

        [Fact]
        public void Constructor_InvalidMemory_ThrowsException()
        {
            Assert.Throws<SmallMind.Core.Exceptions.ValidationException>(() =>
                new MemoryConfiguration(memoryGB: 0));

            Assert.Throws<SmallMind.Core.Exceptions.ValidationException>(() =>
                new MemoryConfiguration(memoryGB: -1));
        }

        [Fact]
        public void SlidingWindow_ConfiguredAutomatically()
        {
            var config = new MemoryConfiguration(customMaxTokens: 32768);

            // Window size should be 1/8 of max tokens
            Assert.Equal(4096, config.SlidingWindowSize);
            // Stride should be 50% of window (50% overlap)
            Assert.Equal(2048, config.SlidingWindowStride);
        }

        [Fact]
        public void SlidingWindow_MinimumSize()
        {
            var config = new MemoryConfiguration(customMaxTokens: 4096);

            // Should use minimum of 2048 even if 1/8 would be less
            Assert.True(config.SlidingWindowSize >= 2048);
        }

        [Fact]
        public void SetMaxContextTokens_UpdatesValue()
        {
            var config = new MemoryConfiguration();

            config.SetMaxContextTokens(65536);

            Assert.Equal(65536, config.MaxContextTokens);
        }

        [Fact]
        public void SetMaxContextTokens_InvalidValue_ThrowsException()
        {
            var config = new MemoryConfiguration();

            Assert.Throws<SmallMind.Core.Exceptions.ValidationException>(() => config.SetMaxContextTokens(0));
            Assert.Throws<SmallMind.Core.Exceptions.ValidationException>(() => config.SetMaxContextTokens(-100));
        }

        [Fact]
        public void EstimateMemoryUsage_ReturnsPositiveValue()
        {
            var config = new MemoryConfiguration(memoryGB: 16);

            long memoryUsage = config.EstimateMemoryUsage(
                vocabSize: 50000,
                embeddingDim: 512,
                numLayers: 6,
                numHeads: 8,
                batchSize: 2,
                seqLength: 1024);

            Assert.True(memoryUsage > 0);
        }

        [Fact]
        public void EstimateMemoryUsage_WithGradientCheckpointing_UsesSmallerMemory()
        {
            var configWithCheckpointing = new MemoryConfiguration(
                memoryGB: 16,
                enableGradientCheckpointing: true,
                checkpointInterval: 2);

            var configWithoutCheckpointing = new MemoryConfiguration(
                memoryGB: 16,
                enableGradientCheckpointing: false);

            long withCheckpointing = configWithCheckpointing.EstimateMemoryUsage(
                vocabSize: 50000,
                embeddingDim: 512,
                numLayers: 12,
                numHeads: 8,
                batchSize: 2,
                seqLength: 1024);

            long withoutCheckpointing = configWithoutCheckpointing.EstimateMemoryUsage(
                vocabSize: 50000,
                embeddingDim: 512,
                numLayers: 12,
                numHeads: 8,
                batchSize: 2,
                seqLength: 1024);

            // Checkpointing should use less memory
            Assert.True(withCheckpointing < withoutCheckpointing);
        }

        [Fact]
        public void EstimateMemoryUsage_WithMixedPrecision_UsesSmallerMemory()
        {
            var configFP16 = new MemoryConfiguration(
                memoryGB: 16,
                enableMixedPrecision: true);

            var configFP32 = new MemoryConfiguration(
                memoryGB: 16,
                enableMixedPrecision: false);

            long fp16Memory = configFP16.EstimateMemoryUsage(
                vocabSize: 50000,
                embeddingDim: 512,
                numLayers: 6,
                numHeads: 8,
                batchSize: 2,
                seqLength: 1024);

            long fp32Memory = configFP32.EstimateMemoryUsage(
                vocabSize: 50000,
                embeddingDim: 512,
                numLayers: 6,
                numHeads: 8,
                batchSize: 2,
                seqLength: 1024);

            // FP16 should use less memory (about 50% for weights)
            Assert.True(fp16Memory < fp32Memory);
        }

        [Fact]
        public void EstimateMemoryUsage_WithMemoryMapping_ExcludesKVCache()
        {
            var configWithMapping = new MemoryConfiguration(
                memoryGB: 16,
                enableMemoryMapping: true);

            var configWithoutMapping = new MemoryConfiguration(
                memoryGB: 16,
                enableMemoryMapping: false);

            long withMapping = configWithMapping.EstimateMemoryUsage(
                vocabSize: 50000,
                embeddingDim: 512,
                numLayers: 6,
                numHeads: 8,
                batchSize: 2,
                seqLength: 2048);

            long withoutMapping = configWithoutMapping.EstimateMemoryUsage(
                vocabSize: 50000,
                embeddingDim: 512,
                numLayers: 6,
                numHeads: 8,
                batchSize: 2,
                seqLength: 2048);

            // Memory mapping should use less memory (KV cache offloaded to disk)
            Assert.True(withMapping < withoutMapping);
        }

        [Fact]
        public void CanFitInMemory_SmallModel_ReturnsTrue()
        {
            var config = new MemoryConfiguration(memoryGB: 16);

            bool canFit = config.CanFitInMemory(
                vocabSize: 10000,
                embeddingDim: 256,
                numLayers: 4,
                numHeads: 4,
                batchSize: 1,
                seqLength: 512);

            Assert.True(canFit);
        }

        [Fact]
        public void CanFitInMemory_LargeModel_ReturnsFalse()
        {
            var config = new MemoryConfiguration(memoryGB: 1); // Very low memory

            bool canFit = config.CanFitInMemory(
                vocabSize: 100000,
                embeddingDim: 2048,
                numLayers: 24,
                numHeads: 16,
                batchSize: 8,
                seqLength: 4096);

            Assert.False(canFit);
        }

        [Fact]
        public void GetSummary_ReturnsValidString()
        {
            var config = new MemoryConfiguration(
                memoryGB: 16,
                enableGradientCheckpointing: true,
                enableMixedPrecision: true,
                enableMemoryMapping: true,
                checkpointInterval: 2);

            string summary = config.GetSummary();

            Assert.Contains("16", summary);
            Assert.Contains("Enabled", summary);
            Assert.Contains("FP16/FP32", summary);
        }

        [Fact]
        public void RealWorldScenario_32kTokensWith64GB()
        {
            // Use 64GB RAM with extensive optimizations for 32k tokens
            var config = new MemoryConfiguration(
                memoryGB: 64,
                enableGradientCheckpointing: true,
                enableMixedPrecision: true,
                enableMemoryMapping: true,
                checkpointInterval: 4); // More aggressive checkpointing

            // Small model configuration for 32k tokens
            bool canFit = config.CanFitInMemory(
                vocabSize: 30000, // Smaller vocab
                embeddingDim: 256, // Smaller embedding
                numLayers: 4, // Fewer layers
                numHeads: 4,
                batchSize: 1,
                seqLength: 32768);

            // Should fit with all optimizations enabled
            Assert.True(canFit, "32k tokens with optimizations should fit in 64GB");
        }
    }
}
