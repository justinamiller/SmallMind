using System;
using System.IO;
using SmallMind.Core.Core;
using SmallMind.Core.Exceptions;

namespace SmallMind.Examples.ParameterLimits
{
    /// <summary>
    /// Demonstrates C# parameter limitations and how SmallMind handles them.
    /// 
    /// This example shows:
    /// 1. How to calculate parameter counts
    /// 2. How to validate model configurations
    /// 3. What happens when you exceed limits
    /// 4. Safe vs unsafe configurations
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== SmallMind Parameter Limits Demo ===\n");

            // 1. Show the fundamental C# limit
            DemonstrateCSharpLimit();

            // 2. Safe configurations
            DemonstrateSafeConfigurations();

            // 3. Unsafe configurations
            DemonstrateUnsafeConfigurations();

            // 4. Parameter calculation and memory estimation
            DemonstrateMemoryEstimation();

            // 5. Validation workflow
            DemonstrateValidationWorkflow();

            // 6. NEW: Chunked tensor support
            DemonstrateChunkedTensorSupport();

            // 7. NEW: Memory-mapped tensor support
            DemonstrateMemoryMappedSupport();

            Console.WriteLine("\n=== Demo Complete ===");
            Console.WriteLine("\nFor more information, see:");
            Console.WriteLine("  - docs/FAQ.md");
            Console.WriteLine("  - docs/CSHARP_LIMITATIONS.md");
            Console.WriteLine("  - docs/LARGE_MODEL_SUPPORT.md");
        }

        static void DemonstrateChunkedTensorSupport()
        {
            Console.WriteLine("6. CHUNKED TENSOR SUPPORT (NEW!)");
            Console.WriteLine("   ------------------------------");
            Console.WriteLine("   SmallMind now supports chunked tensors for models exceeding int.MaxValue\n");

            // Configuration that previously would have failed
            var largeConfig = new
            {
                Name = "Large Embedding Model",
                VocabSize = 100000,
                EmbeddingDim = 30000,
                NumLayers = 24,
                NumHeads = 32,
                BlockSize = 2048
            };

            long tensorSize = (long)largeConfig.VocabSize * largeConfig.EmbeddingDim;

            Console.WriteLine($"   {largeConfig.Name}:");
            Console.WriteLine($"     Vocabulary Size: {largeConfig.VocabSize:N0}");
            Console.WriteLine($"     Embedding Dimension: {largeConfig.EmbeddingDim:N0}");
            Console.WriteLine($"     Embedding Table: {largeConfig.VocabSize:N0} × {largeConfig.EmbeddingDim:N0} = {tensorSize:N0}");
            Console.WriteLine($"     int.MaxValue: {int.MaxValue:N0}");
            Console.WriteLine($"     Exceeds limit by: {tensorSize - int.MaxValue:N0} elements\n");

            Console.WriteLine("   Creating chunked embedding table...");
            try
            {
                // Create a chunked tensor that exceeds int.MaxValue
                var chunkedTensor = Tensor.CreateChunked(
                    new int[] { largeConfig.VocabSize, largeConfig.EmbeddingDim },
                    requiresGrad: true
                );

                Console.WriteLine($"     ✓ Successfully created chunked tensor!");
                Console.WriteLine($"     Tensor shape: [{largeConfig.VocabSize:N0}, {largeConfig.EmbeddingDim:N0}]");
                Console.WriteLine($"     Total elements: {chunkedTensor.TotalElements:N0}");
                Console.WriteLine($"     Is chunked: {chunkedTensor.IsChunked}");
                
                var buffer = chunkedTensor.GetChunkedBuffer();
                Console.WriteLine($"     Stored as {buffer.ChunkCount} chunks of {buffer.ChunkSize:N0} elements each");
                Console.WriteLine($"     Memory usage: ~{buffer.GetMemoryUsageBytes() / (1024.0 * 1024 * 1024):F2} GB\n");

                Console.WriteLine("   Testing embedding lookup with chunked storage...");
                var random = new Random(42);
                chunkedTensor.InitializeRandom(random, 0.02f);
                
                // Simulate embedding lookup
                int testTokenId = 50000;
                long embeddingOffset = (long)testTokenId * largeConfig.EmbeddingDim;
                var embeddingVector = new float[100]; // First 100 dims
                chunkedTensor.CopyTo(embeddingOffset, embeddingVector, 100);
                
                Console.WriteLine($"     ✓ Successfully looked up embedding for token {testTokenId}");
                Console.WriteLine($"     First 5 values: [{embeddingVector[0]:F4}, {embeddingVector[1]:F4}, {embeddingVector[2]:F4}, {embeddingVector[3]:F4}, {embeddingVector[4]:F4}]");
                Console.WriteLine($"     Chunked tensor operations work correctly!\n");
                
            }
            catch (Exception ex)
            {
                Console.WriteLine($"     ✗ Error: {ex.Message}\n");
            }

            Console.WriteLine("   Key Benefits:");
            Console.WriteLine("     • Bypass CLR int.MaxValue array indexing limit");
            Console.WriteLine("     • Support embedding tables with vocab_size × embedding_dim > 2.1B");
            Console.WriteLine("     • Automatic chunking when needed");
            Console.WriteLine("     • Transparent API - same operations work on dense and chunked tensors");
            Console.WriteLine("     • Minimal performance overhead with span-based access\n");

            Console.WriteLine("   Current Limitations:");
            Console.WriteLine("     • MatMul with chunked weights not yet optimized (use for embeddings)");
            Console.WriteLine("     • Chunked tensors cannot be used with legacy code expecting Data array");
            Console.WriteLine("     • Some operations may require copying data\n");

            Console.WriteLine("7. MEMORY-MAPPED TENSORS (DISK STREAMING)");
            Console.WriteLine("   -----------------------------------------");
            Console.WriteLine("   For models that exceed available RAM, stream weights from disk\n");

            var diskModelConfig = new
            {
                Name = "Disk-Streamed Model",
                VocabSize = 200000,
                EmbeddingDim = 25000
            };

            long diskTensorSize = (long)diskModelConfig.VocabSize * diskModelConfig.EmbeddingDim;
            long memorySizeGB = diskTensorSize * sizeof(float) / (1024L * 1024 * 1024);

            Console.WriteLine($"   {diskModelConfig.Name}:");
            Console.WriteLine($"     Vocabulary Size: {diskModelConfig.VocabSize:N0}");
            Console.WriteLine($"     Embedding Dimension: {diskModelConfig.EmbeddingDim:N0}");
            Console.WriteLine($"     Embedding Table: {diskModelConfig.VocabSize:N0} × {diskModelConfig.EmbeddingDim:N0} = {diskTensorSize:N0}");
            Console.WriteLine($"     Memory required: ~{memorySizeGB} GB\n");

            Console.WriteLine("   Creating memory-mapped tensor (streams from disk)...");
            try
            {
                string tempFile = Path.Combine(Path.GetTempPath(), "demo_embedding.bin");
                
                try
                {
                    // Create a small memory-mapped file for demonstration
                    // (Creating 20GB file would be too slow for demo)
                    int demoVocab = 10000;
                    int demoEmbed = 5000;
                    
                    using (var mmTensor = Tensor.CreateMemoryMappedFile(
                        tempFile,
                        new int[] { demoVocab, demoEmbed }))
                    {
                        Console.WriteLine($"     ✓ Created memory-mapped tensor file");
                        Console.WriteLine($"     File: {tempFile}");
                        Console.WriteLine($"     Size: {new FileInfo(tempFile).Length / (1024.0 * 1024):F2} MB");
                        Console.WriteLine($"     Is memory-mapped: {mmTensor.IsMemoryMapped}");
                        Console.WriteLine($"     Total elements: {mmTensor.TotalElements:N0}\n");

                        Console.WriteLine("   Writing test data to disk...");
                        var testData = new float[100];
                        for (int i = 0; i < 100; i++)
                            testData[i] = i * 0.01f;
                        mmTensor.CopyFrom(testData, 1000);
                        Console.WriteLine("     ✓ Data written to memory-mapped file\n");

                        Console.WriteLine("   Reading back from disk (on-demand)...");
                        var readData = new float[100];
                        mmTensor.CopyTo(1000, readData, 100);
                        Console.WriteLine($"     ✓ Data read successfully");
                        Console.WriteLine($"     First 5 values: [{readData[0]:F4}, {readData[1]:F4}, {readData[2]:F4}, {readData[3]:F4}, {readData[4]:F4}]\n");
                    }

                    // Clean up
                    if (File.Exists(tempFile))
                        File.Delete(tempFile);
                }
                catch
                {
                    // Clean up on error
                    if (File.Exists(tempFile))
                        File.Delete(tempFile);
                    throw;
                }

                Console.WriteLine("   Key Benefits:");
                Console.WriteLine("     • Models larger than available RAM");
                Console.WriteLine("     • OS manages paging automatically");
                Console.WriteLine("     • Share model weights across processes");
                Console.WriteLine("     • Perfect for inference-only scenarios");
                Console.WriteLine("     • No need to load entire model into memory\n");

                Console.WriteLine("   Trade-offs:");
                Console.WriteLine("     • Much slower than in-memory (disk I/O)");
                Console.WriteLine("     • Not suitable for training (too slow)");
                Console.WriteLine("     • Requires fast SSD for acceptable performance");
                Console.WriteLine("     • Read-only by default\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"     ✗ Error: {ex.Message}\n");
            }
        }

        static void DemonstrateCSharpLimit()
        {
            Console.WriteLine("1. C# ARRAY INDEXING LIMIT");
            Console.WriteLine("   -------------------------");
            Console.WriteLine($"   int.MaxValue = {int.MaxValue:N0}");
            Console.WriteLine($"   This is the maximum number of elements in any C# array");
            Console.WriteLine($"   Equivalent to ~2.15 billion elements\n");
        }

        static void DemonstrateSafeConfigurations()
        {
            Console.WriteLine("2. SAFE MODEL CONFIGURATIONS");
            Console.WriteLine("   --------------------------");

            // GPT-2 Small
            var config1 = new
            {
                Name = "GPT-2 Small",
                VocabSize = 50257,
                EmbeddingDim = 768,
                NumLayers = 12,
                NumHeads = 12,
                BlockSize = 1024
            };

            long params1 = LargeModelSupport.CalculateParameterCount(
                config1.VocabSize, config1.BlockSize, config1.EmbeddingDim, 
                config1.NumLayers, config1.NumHeads);
            long tensorSize1 = (long)config1.VocabSize * config1.EmbeddingDim;

            Console.WriteLine($"   {config1.Name}:");
            Console.WriteLine($"     Total Parameters: {LargeModelSupport.FormatParameters(params1)}");
            Console.WriteLine($"     Largest Tensor: {config1.VocabSize:N0} × {config1.EmbeddingDim:N0} = {tensorSize1:N0}");
            Console.WriteLine($"     Status: ✓ SAFE (tensor size {tensorSize1:N0} < {int.MaxValue:N0})\n");

            // LLaMA-style 1B
            var config2 = new
            {
                Name = "LLaMA-style 1B",
                VocabSize = 32000,
                EmbeddingDim = 2048,
                NumLayers = 22,
                NumHeads = 16,
                BlockSize = 2048
            };

            long params2 = LargeModelSupport.CalculateParameterCount(
                config2.VocabSize, config2.BlockSize, config2.EmbeddingDim,
                config2.NumLayers, config2.NumHeads);
            long tensorSize2 = (long)config2.VocabSize * config2.EmbeddingDim;

            Console.WriteLine($"   {config2.Name}:");
            Console.WriteLine($"     Total Parameters: {LargeModelSupport.FormatParameters(params2)}");
            Console.WriteLine($"     Largest Tensor: {config2.VocabSize:N0} × {config2.EmbeddingDim:N0} = {tensorSize2:N0}");
            Console.WriteLine($"     Status: ✓ SAFE (tensor size {tensorSize2:N0} < {int.MaxValue:N0})");
            Console.WriteLine($"     Recommendation: {LargeModelSupport.GetRecommendation(params2)}\n");

            // Near-limit configuration
            var config3 = new
            {
                Name = "Near-Limit Model",
                VocabSize = 50000,
                EmbeddingDim = 40000,
                NumLayers = 24,
                NumHeads = 32,
                BlockSize = 2048
            };

            long params3 = LargeModelSupport.CalculateParameterCount(
                config3.VocabSize, config3.BlockSize, config3.EmbeddingDim,
                config3.NumLayers, config3.NumHeads);
            long tensorSize3 = (long)config3.VocabSize * config3.EmbeddingDim;

            Console.WriteLine($"   {config3.Name}:");
            Console.WriteLine($"     Total Parameters: {LargeModelSupport.FormatParameters(params3)}");
            Console.WriteLine($"     Largest Tensor: {config3.VocabSize:N0} × {config3.EmbeddingDim:N0} = {tensorSize3:N0}");
            Console.WriteLine($"     Status: ⚠ SAFE but close to limit ({(double)tensorSize3 / int.MaxValue * 100:F1}% of max)");
            Console.WriteLine($"     Recommendation: {LargeModelSupport.GetRecommendation(params3)}\n");
        }

        static void DemonstrateUnsafeConfigurations()
        {
            Console.WriteLine("3. UNSAFE MODEL CONFIGURATIONS (WILL FAIL)");
            Console.WriteLine("   ----------------------------------------");

            // Configuration that exceeds limit
            var badConfig = new
            {
                Name = "Oversized Model",
                VocabSize = 100000,
                EmbeddingDim = 30000,
                NumLayers = 24,
                NumHeads = 32,
                BlockSize = 2048
            };

            long tensorSize = (long)badConfig.VocabSize * badConfig.EmbeddingDim;

            Console.WriteLine($"   {badConfig.Name}:");
            Console.WriteLine($"     Vocabulary Size: {badConfig.VocabSize:N0}");
            Console.WriteLine($"     Embedding Dimension: {badConfig.EmbeddingDim:N0}");
            Console.WriteLine($"     Largest Tensor: {badConfig.VocabSize:N0} × {badConfig.EmbeddingDim:N0} = {tensorSize:N0}");
            Console.WriteLine($"     int.MaxValue: {int.MaxValue:N0}");
            Console.WriteLine($"     Status: ✗ UNSAFE ({tensorSize:N0} > {int.MaxValue:N0})");

            try
            {
                // This will throw ValidationException
                LargeModelSupport.ValidateConfiguration(
                    badConfig.VocabSize,
                    badConfig.BlockSize,
                    badConfig.EmbeddingDim,
                    badConfig.NumLayers,
                    badConfig.NumHeads);

                Console.WriteLine("     Unexpectedly passed validation!");
            }
            catch (ValidationException ex)
            {
                Console.WriteLine($"     Exception (as expected): {ex.Message}\n");
            }

            // Demonstrate tensor overflow protection
            Console.WriteLine("   Attempting to create oversized tensor:");
            try
            {
                int[] shape = new int[] { badConfig.VocabSize, badConfig.EmbeddingDim };
                int size = Tensor.ShapeToSize(shape);
                Console.WriteLine($"     Unexpectedly succeeded with size: {size}");
            }
            catch (ValidationException ex)
            {
                Console.WriteLine($"     ✓ Caught overflow: {ex.Message.Split('.')[0]}...\n");
            }
        }

        static void DemonstrateMemoryEstimation()
        {
            Console.WriteLine("4. MEMORY ESTIMATION");
            Console.WriteLine("   ------------------");

            var config = new
            {
                VocabSize = 32000,
                EmbeddingDim = 2048,
                NumLayers = 22,
                NumHeads = 16,
                BlockSize = 2048
            };

            long paramCount = LargeModelSupport.CalculateParameterCount(
                config.VocabSize, config.BlockSize, config.EmbeddingDim,
                config.NumLayers, config.NumHeads);

            Console.WriteLine($"   Model Configuration: {LargeModelSupport.FormatParameters(paramCount)} parameters\n");

            Console.WriteLine("   Inference Memory Requirements:");
            long fp32 = LargeModelSupport.EstimateMemoryBytes(paramCount, 4.0);
            long q8 = LargeModelSupport.EstimateMemoryBytes(paramCount, 1.0);
            long q4 = LargeModelSupport.EstimateMemoryBytes(paramCount, 0.5);

            Console.WriteLine($"     FP32 (no quantization): {LargeModelSupport.FormatBytes(fp32)}");
            Console.WriteLine($"     Q8 (8-bit quantization): {LargeModelSupport.FormatBytes(q8)}");
            Console.WriteLine($"     Q4 (4-bit quantization): {LargeModelSupport.FormatBytes(q4)}\n");

            Console.WriteLine("   Training Memory Requirements (FP32 + Gradients + Adam):");
            long trainingMem = LargeModelSupport.EstimateMemoryBytes(paramCount, 4.0, 
                includeGradients: true, includeOptimizer: true);
            Console.WriteLine($"     Total: {LargeModelSupport.FormatBytes(trainingMem)}");
            Console.WriteLine($"     Breakdown:");
            Console.WriteLine($"       - Model weights (FP32): {LargeModelSupport.FormatBytes(fp32)}");
            Console.WriteLine($"       - Gradients (FP32): {LargeModelSupport.FormatBytes(fp32)}");
            Console.WriteLine($"       - Adam optimizer (2×FP32): {LargeModelSupport.FormatBytes(fp32 * 2)}\n");
        }

        static void DemonstrateValidationWorkflow()
        {
            Console.WriteLine("5. RECOMMENDED VALIDATION WORKFLOW");
            Console.WriteLine("   --------------------------------");

            var config = new
            {
                VocabSize = 50000,
                EmbeddingDim = 4096,
                NumLayers = 24,
                NumHeads = 32,
                BlockSize = 2048
            };

            Console.WriteLine("   Step 1: Calculate parameter count");
            long paramCount = LargeModelSupport.CalculateParameterCount(
                config.VocabSize, config.BlockSize, config.EmbeddingDim,
                config.NumLayers, config.NumHeads);
            Console.WriteLine($"     ✓ Total parameters: {LargeModelSupport.FormatParameters(paramCount)}\n");

            Console.WriteLine("   Step 2: Check largest tensor size");
            long maxTensorSize = (long)config.VocabSize * config.EmbeddingDim;
            Console.WriteLine($"     Vocabulary × Embedding: {config.VocabSize:N0} × {config.EmbeddingDim:N0} = {maxTensorSize:N0}");
            Console.WriteLine($"     Limit: {int.MaxValue:N0}");
            Console.WriteLine($"     ✓ Within limit: {maxTensorSize < int.MaxValue}\n");

            Console.WriteLine("   Step 3: Get recommendations");
            string recommendation = LargeModelSupport.GetRecommendation(paramCount);
            Console.WriteLine($"     {recommendation}\n");

            Console.WriteLine("   Step 4: Validate configuration");
            try
            {
                long availableMemory = 16L * 1024 * 1024 * 1024; // 16GB
                LargeModelSupport.ValidateConfiguration(
                    config.VocabSize,
                    config.BlockSize,
                    config.EmbeddingDim,
                    config.NumLayers,
                    config.NumHeads,
                    availableMemory,
                    quantizationBits: 8);

                Console.WriteLine("     ✓ Configuration validated successfully");
                Console.WriteLine($"     Safe to load with Q8 quantization on system with 16GB RAM\n");
            }
            catch (ValidationException ex)
            {
                Console.WriteLine($"     ✗ Validation failed: {ex.Message}\n");
            }
        }

        static void DemonstrateMemoryMappedSupport()
        {
            Console.WriteLine("7. MEMORY-MAPPED TENSORS (DISK STREAMING)");
            Console.WriteLine("   -----------------------------------------");
            Console.WriteLine("   For models that exceed available RAM, stream weights from disk\n");

            var diskModelConfig = new
            {
                Name = "Disk-Streamed Model",
                VocabSize = 200000,
                EmbeddingDim = 25000
            };

            long diskTensorSize = (long)diskModelConfig.VocabSize * diskModelConfig.EmbeddingDim;
            long memorySizeGB = diskTensorSize * sizeof(float) / (1024L * 1024 * 1024);

            Console.WriteLine($"   {diskModelConfig.Name}:");
            Console.WriteLine($"     Vocabulary Size: {diskModelConfig.VocabSize:N0}");
            Console.WriteLine($"     Embedding Dimension: {diskModelConfig.EmbeddingDim:N0}");
            Console.WriteLine($"     Embedding Table: {diskModelConfig.VocabSize:N0} × {diskModelConfig.EmbeddingDim:N0} = {diskTensorSize:N0}");
            Console.WriteLine($"     Memory required: ~{memorySizeGB} GB\n");

            Console.WriteLine("   Creating memory-mapped tensor (streams from disk)...");
            try
            {
                string tempFile = Path.Combine(Path.GetTempPath(), "demo_embedding.bin");
                
                try
                {
                    // Create a small memory-mapped file for demonstration
                    // (Creating 20GB file would be too slow for demo)
                    int demoVocab = 10000;
                    int demoEmbed = 5000;
                    
                    using (var mmTensor = Tensor.CreateMemoryMappedFile(
                        tempFile,
                        new int[] { demoVocab, demoEmbed }))
                    {
                        Console.WriteLine($"     ✓ Created memory-mapped tensor file");
                        Console.WriteLine($"     File: {tempFile}");
                        Console.WriteLine($"     Size: {new FileInfo(tempFile).Length / (1024.0 * 1024):F2} MB");
                        Console.WriteLine($"     Is memory-mapped: {mmTensor.IsMemoryMapped}");
                        Console.WriteLine($"     Total elements: {mmTensor.TotalElements:N0}\n");

                        Console.WriteLine("   Writing test data to disk...");
                        var testData = new float[100];
                        for (int i = 0; i < 100; i++)
                            testData[i] = i * 0.01f;
                        mmTensor.CopyFrom(testData, 1000);
                        Console.WriteLine("     ✓ Data written to memory-mapped file\n");

                        Console.WriteLine("   Reading back from disk (on-demand)...");
                        var readData = new float[100];
                        mmTensor.CopyTo(1000, readData, 100);
                        Console.WriteLine($"     ✓ Data read successfully");
                        Console.WriteLine($"     First 5 values: [{readData[0]:F4}, {readData[1]:F4}, {readData[2]:F4}, {readData[3]:F4}, {readData[4]:F4}]\n");
                    }

                    // Clean up
                    if (File.Exists(tempFile))
                        File.Delete(tempFile);
                }
                catch
                {
                    // Clean up on error
                    if (File.Exists(tempFile))
                        File.Delete(tempFile);
                    throw;
                }

                Console.WriteLine("   Key Benefits:");
                Console.WriteLine("     • Models larger than available RAM");
                Console.WriteLine("     • OS manages paging automatically");
                Console.WriteLine("     • Share model weights across processes");
                Console.WriteLine("     • Perfect for inference-only scenarios");
                Console.WriteLine("     • No need to load entire model into memory\n");

                Console.WriteLine("   Trade-offs:");
                Console.WriteLine("     • Much slower than in-memory (disk I/O)");
                Console.WriteLine("     • Not suitable for training (too slow)");
                Console.WriteLine("     • Requires fast SSD for acceptable performance");
                Console.WriteLine("     • Read-only by default\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"     ✗ Error: {ex.Message}\n");
            }
        }
    }
}
