using System;
using SmallMind.Core.Core;

namespace SmallMind.Samples
{
    /// <summary>
    /// Example demonstrating memory optimization features for large context windows.
    /// Shows how to configure the TransformerModel for 32k+ token contexts with 16GB RAM.
    /// </summary>
    public static class MemoryOptimizationExample
    {
        public static void Run()
        {
            Console.WriteLine("=== SmallMind Memory Optimization Example ===\n");

            // 1. Configure memory settings based on system RAM
            DemonstrateMemoryConfiguration();

            // 2. Show gradient checkpointing
            DemonstrateGradientCheckpointing();

            // 3. Show sliding window processing
            DemonstrateSlidingWindow();

            // 4. Show KV cache with memory mapping
            DemonstrateKVCache();

            // 5. Show mixed precision
            DemonstrateMixedPrecision();

            Console.WriteLine("\n=== Example Complete ===");
        }

        private static void DemonstrateMemoryConfiguration()
        {
            Console.WriteLine("1. Memory Configuration\n");

            // Auto-detect system memory and configure accordingly
            var config = new MemoryConfiguration(
                enableGradientCheckpointing: true,
                enableMixedPrecision: true,
                enableMemoryMapping: false,
                checkpointInterval: 2);

            Console.WriteLine(config.GetSummary());
            Console.WriteLine();

            // Estimate memory usage for a model
            long memoryUsage = config.EstimateMemoryUsage(
                vocabSize: 50000,
                embeddingDim: 512,
                numLayers: 6,
                numHeads: 8,
                batchSize: 1,
                seqLength: 4096);

            Console.WriteLine($"Estimated memory usage: {memoryUsage / (1024.0 * 1024.0 * 1024.0):F2} GB");

            bool canFit = config.CanFitInMemory(
                vocabSize: 50000,
                embeddingDim: 512,
                numLayers: 6,
                numHeads: 8,
                batchSize: 1,
                seqLength: 4096);

            Console.WriteLine($"Can fit in memory: {canFit}");
            Console.WriteLine();
        }

        private static void DemonstrateGradientCheckpointing()
        {
            Console.WriteLine("2. Gradient Checkpointing\n");

            int numLayers = 12;
            long perLayerBytes = 100 * 1024 * 1024; // 100 MB per layer
            long availableMemory = 8L * 1024 * 1024 * 1024; // 8 GB

            // Calculate optimal checkpoint interval
            int interval = GradientCheckpointing.GetOptimalCheckpointInterval(
                numLayers,
                availableMemory,
                perLayerBytes,
                CheckpointStrategy.SqrtLayers);

            Console.WriteLine($"Optimal checkpoint interval: {interval}");

            var (without, with, savings) = GradientCheckpointing.EstimateMemorySavings(
                numLayers,
                perLayerBytes,
                interval);

            Console.WriteLine($"Memory without checkpointing: {without / (1024.0 * 1024.0):F2} MB");
            Console.WriteLine($"Memory with checkpointing: {with / (1024.0 * 1024.0):F2} MB");
            Console.WriteLine($"Memory savings: {savings:F1}%");
            Console.WriteLine();

            // Using CheckpointManager
            var checkpointMgr = new CheckpointManager(checkpointInterval: interval, enabled: true);
            Console.WriteLine($"CheckpointManager created with interval {checkpointMgr.CheckpointInterval}");
            Console.WriteLine();
        }

        private static void DemonstrateSlidingWindow()
        {
            Console.WriteLine("3. Sliding Window Processing\n");

            // Process a 32k token sequence with 4k windows
            var processor = new SlidingWindowProcessor(
                windowSize: 4096,
                stride: 2048); // 50% overlap

            Console.WriteLine($"Window Size: {processor.WindowSize}");
            Console.WriteLine($"Stride: {processor.Stride}");
            Console.WriteLine($"Overlap: {processor.OverlapSize}");
            Console.WriteLine();

            // Simulate a 32k token sequence
            var tokens = new int[32768];
            for (int i = 0; i < tokens.Length; i++)
            {
                tokens[i] = i % 1000;
            }

            int windowCount = processor.EstimateWindowCount(tokens.Length);
            Console.WriteLine($"Processing {tokens.Length:N0} tokens in {windowCount} windows");

            // Generate windows
            int count = 0;
            foreach (var window in processor.GetWindows(tokens))
            {
                count++;
                if (count <= 3 || count == windowCount)
                {
                    Console.WriteLine($"  Window {count}: {window.Length} tokens");
                }
                else if (count == 4)
                {
                    Console.WriteLine("  ...");
                }
            }

            Console.WriteLine($"Total windows generated: {count}");
            Console.WriteLine();
        }

        private static void DemonstrateKVCache()
        {
            Console.WriteLine("4. KV Cache (Memory Mapping)\n");

            // In-memory cache (for smaller contexts)
            using (var inMemCache = new KVCache(capacity: 1000))
            {
                Console.WriteLine($"Created in-memory KV cache");
                Console.WriteLine($"  Capacity: 1000 floats");
                Console.WriteLine($"  Uses memory mapping: {inMemCache.UsesMemoryMapping}");
                Console.WriteLine($"  Size: {inMemCache.SizeBytes} bytes");

                // Write and read
                inMemCache.Write(0, 1.5f);
                inMemCache.Write(999, 2.5f);

                float val1 = inMemCache.Read(0);
                float val2 = inMemCache.Read(999);

                Console.WriteLine($"  Wrote and read values: {val1}, {val2}");
            }

            Console.WriteLine();

            // Multi-layer cache for a transformer
            Console.WriteLine("Multi-layer KV Cache:");
            using (var mlCache = new MultiLayerKVCache(
                numLayers: 6,
                maxSeqLen: 4096,
                numHeads: 8,
                headDim: 64,
                useMemoryMapping: false))
            {
                Console.WriteLine($"  Created KV cache for 6 layers");
                Console.WriteLine($"  Max sequence length: 4096");
                Console.WriteLine($"  Heads: 8, Head dimension: 64");

                // Write to layer 0
                var keyCache = mlCache.GetKeyCache(0);
                keyCache.Write(0, 1.0f);

                Console.WriteLine($"  Written to layer 0 key cache");
            }

            Console.WriteLine();
        }

        private static void DemonstrateMixedPrecision()
        {
            Console.WriteLine("5. Mixed Precision (FP16/FP32)\n");

            // Convert between precisions
            var fp32Data = new float[] { 1.5f, 2.5f, 3.5f, 4.5f, 5.5f };
            var fp16Data = new Half[fp32Data.Length];
            var fp32Result = new float[fp32Data.Length];

            Console.WriteLine("Original FP32 values:");
            Console.WriteLine($"  {string.Join(", ", fp32Data)}");

            // Convert to FP16
            MixedPrecision.FloatToHalf(fp32Data, fp16Data);
            Console.WriteLine($"\nConverted to FP16 (Half):");
            Console.WriteLine($"  {string.Join(", ", fp16Data)}");

            // Convert back to FP32
            MixedPrecision.HalfToFloat(fp16Data, fp32Result);
            Console.WriteLine($"\nConverted back to FP32:");
            Console.WriteLine($"  {string.Join(", ", fp32Result)}");

            // Check for gradient overflow
            var gradients = new float[] { 1e10f, 2.5f, float.NaN, 4.5f };
            bool hasOverflow = MixedPrecision.HasGradientOverflow(gradients);
            Console.WriteLine($"\nGradient overflow detection: {hasOverflow}");

            Console.WriteLine("\nMemory savings with FP16:");
            Console.WriteLine($"  FP32: {fp32Data.Length * 4} bytes");
            Console.WriteLine($"  FP16: {fp16Data.Length * 2} bytes");
            Console.WriteLine($"  Savings: 50%");
            Console.WriteLine();
        }

        /// <summary>
        /// Complete example showing all optimizations working together
        /// </summary>
        public static void ComprehensiveExample()
        {
            Console.WriteLine("\n=== Comprehensive Example: 32k Tokens with 16GB RAM ===\n");

            // Step 1: Configure memory based on available RAM
            var memConfig = new MemoryConfiguration(
                memoryGB: 16,
                enableGradientCheckpointing: true,
                enableMixedPrecision: true,
                enableMemoryMapping: false,
                checkpointInterval: 2);

            Console.WriteLine("Configuration:");
            Console.WriteLine(memConfig.GetSummary());
            Console.WriteLine();

            // Step 2: Check if our model fits
            bool canFit = memConfig.CanFitInMemory(
                vocabSize: 50000,
                embeddingDim: 384,
                numLayers: 6,
                numHeads: 6,
                batchSize: 1,
                seqLength: 4096); // Process in 4k chunks

            Console.WriteLine($"Model can fit in memory: {canFit}");
            Console.WriteLine();

            // Step 3: Set up sliding window for 32k context
            var windowProcessor = new SlidingWindowProcessor(
                windowSize: 4096,
                stride: 2048);

            var fullContext = new int[32768]; // 32k tokens
            for (int i = 0; i < fullContext.Length; i++)
            {
                fullContext[i] = i % 10000;
            }

            Console.WriteLine($"Processing {fullContext.Length:N0} tokens using sliding windows");
            Console.WriteLine($"  Window size: {windowProcessor.WindowSize}");
            Console.WriteLine($"  Windows needed: {windowProcessor.EstimateWindowCount(fullContext.Length)}");
            Console.WriteLine();

            // Step 4: Process in windows (simulated)
            Console.WriteLine("Processing windows:");
            int processed = 0;
            foreach (var window in windowProcessor.GetWindows(fullContext))
            {
                processed++;
                // In real usage, each window would be passed through the transformer
                if (processed <= 2 || processed >= windowProcessor.EstimateWindowCount(fullContext.Length))
                {
                    Console.WriteLine($"  Window {processed}: {window.Length} tokens");
                }
            }

            Console.WriteLine($"\nSuccessfully processed {fullContext.Length:N0} tokens!");
            Console.WriteLine("With optimizations:");
            Console.WriteLine($"  ✓ Gradient checkpointing (interval: {memConfig.CheckpointInterval})");
            Console.WriteLine($"  ✓ Mixed precision (FP16/FP32)");
            Console.WriteLine($"  ✓ Sliding window attention");
            Console.WriteLine($"  Memory budget: 16GB");
        }
    }
}
