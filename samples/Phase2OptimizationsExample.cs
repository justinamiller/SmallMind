using System;
using System.IO;
using SmallMind.Core;
using SmallMind.Text;

namespace SmallMind.Examples
{
    /// <summary>
    /// Example demonstrating Phase 2 training optimizations
    /// </summary>
    public class Phase2OptimizationsExample
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("╔══════════════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║              SmallMind - Phase 2 Optimizations Example                ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════════════════════════╝\n");

            // Sample training data
            string sampleData = @"The quick brown fox jumps over the lazy dog.
A journey of a thousand miles begins with a single step.
To be or not to be, that is the question.
All that glitters is not gold.
Knowledge is power.
Time flies like an arrow.
Practice makes perfect.
Actions speak louder than words.";

            // Initialize tokenizer
            var tokenizer = new Tokenizer();
            
            // Initialize a small model for demonstration
            Console.WriteLine("Initializing transformer model...");
            var model = new TransformerModel(
                vocabSize: tokenizer.VocabSize,
                blockSize: 64,      // Smaller block size for demo
                nEmbd: 64,          // Smaller embedding dimension
                nLayer: 2,          // Just 2 layers
                nHead: 2,           // 2 attention heads
                dropout: 0.1,
                seed: 42
            );

            // Initialize training
            var training = new Training(
                model, 
                tokenizer, 
                sampleData,
                blockSize: 64,
                batchSize: 4,       // Small batch size
                seed: 42
            );

            // Example 1: Basic training (no optimizations)
            Console.WriteLine("\n═══════════════════════════════════════════════════════════════════════════");
            Console.WriteLine("Example 1: Standard Training (Baseline)");
            Console.WriteLine("═══════════════════════════════════════════════════════════════════════════\n");
            
            var baselineConfig = new TrainingConfig
            {
                UseMixedPrecision = false,
                UseGradientCheckpointing = false,
                EnableDiagnostics = true  // Enable to see performance
            };

            training.TrainOptimized(
                steps: 20,
                learningRate: 0.001,
                logEvery: 5,
                saveEvery: 100,
                checkpointDir: "checkpoints/baseline",
                config: baselineConfig,
                gradAccumSteps: 1,
                warmupSteps: 5,
                valEvery: 0  // Skip validation for demo
            );

            // Example 2: Mixed precision training
            Console.WriteLine("\n═══════════════════════════════════════════════════════════════════════════");
            Console.WriteLine("Example 2: Mixed Precision Training (FP16/FP32)");
            Console.WriteLine("═══════════════════════════════════════════════════════════════════════════\n");
            
            // Reinitialize model for fair comparison
            model = new TransformerModel(
                vocabSize: tokenizer.VocabSize,
                blockSize: 64,
                nEmbd: 64,
                nLayer: 2,
                nHead: 2,
                dropout: 0.1,
                seed: 42
            );
            
            training = new Training(model, tokenizer, sampleData, blockSize: 64, batchSize: 4, seed: 42);

            var mixedPrecisionConfig = new TrainingConfig
            {
                UseMixedPrecision = true,  // Enable mixed precision
                UseGradientCheckpointing = false,
                EnableDiagnostics = true,
                CheckGradientHealth = true,  // Monitor gradient health
                DiagnosticInterval = 10
            };

            training.TrainOptimized(
                steps: 20,
                learningRate: 0.001,
                logEvery: 5,
                saveEvery: 100,
                checkpointDir: "checkpoints/mixed_precision",
                config: mixedPrecisionConfig,
                gradAccumSteps: 1,
                warmupSteps: 5,
                valEvery: 0
            );

            // Example 3: All optimizations enabled
            Console.WriteLine("\n═══════════════════════════════════════════════════════════════════════════");
            Console.WriteLine("Example 3: All Optimizations Enabled");
            Console.WriteLine("═══════════════════════════════════════════════════════════════════════════\n");
            
            // Reinitialize model
            model = new TransformerModel(
                vocabSize: tokenizer.VocabSize,
                blockSize: 64,
                nEmbd: 64,
                nLayer: 2,
                nHead: 2,
                dropout: 0.1,
                seed: 42
            );
            
            training = new Training(model, tokenizer, sampleData, blockSize: 64, batchSize: 4, seed: 42);

            var fullOptimizationConfig = new TrainingConfig
            {
                UseMixedPrecision = true,           // FP16/FP32
                UseGradientCheckpointing = true,    // Memory optimization
                CheckpointStrategy = CheckpointStrategy.SqrtLayers,  // Balanced strategy
                EnableDiagnostics = true,           // Performance profiling
                CheckGradientHealth = true,         // Health monitoring
                DiagnosticInterval = 10
            };

            training.TrainOptimized(
                steps: 20,
                learningRate: 0.001,
                logEvery: 5,
                saveEvery: 100,
                checkpointDir: "checkpoints/full_optimization",
                config: fullOptimizationConfig,
                gradAccumSteps: 2,  // Gradient accumulation
                warmupSteps: 5,
                valEvery: 0
            );

            Console.WriteLine("\n╔══════════════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                         Examples Completed!                           ║");
            Console.WriteLine("╠══════════════════════════════════════════════════════════════════════════╣");
            Console.WriteLine("║ Compare the performance reports above to see the impact of:          ║");
            Console.WriteLine("║   • Mixed Precision (FP16/FP32)                                       ║");
            Console.WriteLine("║   • Gradient Checkpointing                                            ║");
            Console.WriteLine("║   • Training Diagnostics                                              ║");
            Console.WriteLine("║   • Gradient Health Monitoring                                        ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════════════════════════╝\n");

            // Demonstrate individual components
            Console.WriteLine("\n═══════════════════════════════════════════════════════════════════════════");
            Console.WriteLine("Bonus: Individual Component Examples");
            Console.WriteLine("═══════════════════════════════════════════════════════════════════════════\n");

            // Matrix operations example
            Console.WriteLine("1. Optimized Matrix Operations:");
            float[] A = { 1, 2, 3, 4, 5, 6 }; // 2x3
            float[] B = { 1, 2, 3, 4, 5, 6 }; // 2x3
            float[] C = new float[4]; // 2x2
            MatrixOps.MatMulTransposeB(A, B, C, M: 2, K: 3, N: 2);
            Console.WriteLine($"   A × B^T = [{C[0]:F1}, {C[1]:F1}; {C[2]:F1}, {C[3]:F1}]");

            // Memory pool example
            Console.WriteLine("\n2. Memory Pool:");
            var pool = TensorPool.Shared;
            var array1 = pool.Rent(100);
            Console.WriteLine($"   Rented array of size: {array1.Length}");
            pool.Return(array1);
            var array2 = pool.Rent(100);
            Console.WriteLine($"   Reused same array: {(object.ReferenceEquals(array1, array2) ? "Yes" : "No")}");

            // Gradient checkpointing example
            Console.WriteLine("\n3. Gradient Checkpointing:");
            var (withoutCP, withCP, savings) = GradientCheckpointing.EstimateMemorySavings(
                numLayers: 10, 
                perLayerBytes: 1000000, 
                checkpointInterval: 2
            );
            Console.WriteLine($"   Memory without checkpointing: {withoutCP / 1024 / 1024}MB");
            Console.WriteLine($"   Memory with checkpointing: {withCP / 1024 / 1024}MB");
            Console.WriteLine($"   Savings: {savings:F1}%");

            // Mixed precision example
            Console.WriteLine("\n4. Mixed Precision Conversion:");
            float[] floats = { 1.5f, 2.25f, -3.5f };
            Half[] halves = new Half[floats.Length];
            MixedPrecision.FloatToHalf(floats, halves);
            Console.WriteLine($"   FP32 -> FP16: [{floats[0]}f, {floats[1]}f, {floats[2]}f]");
            Console.WriteLine($"                 [{(float)halves[0]:F2}f, {(float)halves[1]:F2}f, {(float)halves[2]:F2}f]");

            Console.WriteLine("\n✓ All examples completed successfully!\n");
        }
    }
}
