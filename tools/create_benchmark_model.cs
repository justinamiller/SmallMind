using System;
using System.IO;
using System.Threading.Tasks;
using SmallMind.Core;
using SmallMind.Transformers;
using SmallMind.Quantization;
using SmallMind.Quantization.IO.Smq;
using SmallMind.Runtime;

namespace CreateBenchmarkModel
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Creating minimal benchmark model...\n");
            
            string outputPath = args.Length > 0 ? args[0] : "benchmark-model.smq";
            
            // Create a very tiny model for benchmarking
            Console.WriteLine("Initializing tiny transformer model...");
            var model = new TransformerModel(
                vocabSize: 100,       // Very small vocab
                blockSize: 64,        // Small context
                nEmbed: 32,           // Small embedding
                nLayer: 2,            // Just 2 layers
                nHead: 2,             // 2 attention heads
                dropout: 0.0,         // No dropout for inference
                seed: 42
            );
            
            Console.WriteLine($"Model created with {model.Parameters.Count} parameters\n");
            
            // Convert to checkpoint
            var checkpoint = model.ToCheckpoint();
            
            // Save as .smnd first
            var checkpointPath = Path.ChangeExtension(outputPath, ".smnd");
            var store = new BinaryCheckpointStore();
            await store.SaveAsync(checkpoint, checkpointPath);
            Console.WriteLine($"✓ Saved checkpoint: {checkpointPath}");
            
            // Now quantize to .smq format
            Console.WriteLine($"\nQuantizing to .smq format...");
            
            try
            {
                using var inputStream = File.OpenRead(checkpointPath);
                using var outputStream = File.Create(outputPath);
                
                // Load checkpoint
                var loadedCheckpoint = await store.LoadAsync(checkpointPath, default);
                
                // Quantize and write
                using (var writer = new SmqWriter(outputStream))
                {
                    writer.BeginModel(
                        modelType: "transformer",
                        vocabSize: 100,
                        contextLength: 64,
                        embeddingDim: 32,
                        numLayers: 2,
                        numHeads: 2
                    );
                    
                    foreach (var kvp in loadedCheckpoint.Parameters)
                    {
                        var name = kvp.Key;
                        var tensor = kvp.Value;
                        
                        // Use Q8_0 quantization for all weights
                        var quantized = Quantizer.Quantize(tensor.Data, QuantizationScheme.Q8_0);
                        
                        writer.WriteTensor(
                            name: name,
                            shape: tensor.Shape,
                            scheme: QuantizationScheme.Q8_0,
                            quantizedData: quantized.Data,
                            quantizationMetadata: quantized.Metadata
                        );
                    }
                    
                    writer.EndModel();
                }
                
                Console.WriteLine($"✓ Saved quantized model: {outputPath}");
                Console.WriteLine($"  Size: {new FileInfo(outputPath).Length / 1024.0:F2} KB");
                
                // Clean up checkpoint
                File.Delete(checkpointPath);
                Console.WriteLine($"\n✓ Benchmark model ready: {outputPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n✗ Error during quantization: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                Environment.Exit(1);
            }
        }
    }
}
