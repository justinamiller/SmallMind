using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using SmallMind.Core;
using SmallMind.Runtime;
using SmallMind.Quantization.Tensors;
using SmallMind.Quantization.IO.Smq;

namespace CreateBenchmarkModel
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Creating minimal .smq benchmark model...\n");
            
            string checkpointPath = args.Length > 0 ? args[0] : "../../examples/MinimalGenerate/demo-model.smnd";
            string outputPath = args.Length > 1 ? args[1] : "benchmark-model.smq";
            
            if (!File.Exists(checkpointPath))
            {
                Console.WriteLine($"Error: Checkpoint not found: {checkpointPath}");
                Console.WriteLine("Please run MinimalGenerate first to create demo-model.smnd");
                Environment.Exit(1);
            }
            
            // Load checkpoint
            Console.WriteLine($"Loading checkpoint: {checkpointPath}");
            var store = new BinaryCheckpointStore();
            var checkpoint = await store.LoadAsync(checkpointPath);
            Console.WriteLine($"✓ Loaded {checkpoint.Parameters.Count} parameters\n");
            
            // Quantize all tensors to Q8_0
            Console.WriteLine("Quantizing tensors to Q8_0...");
            var quantizedTensors = new Dictionary<string, object>();
            int tensorIdx = 0;
            
            foreach (var param in checkpoint.Parameters)
            {
                string tensorName = $"tensor_{tensorIdx:D3}";
                
                // Determine tensor shape
                int rows, cols;
                if (param.Shape.Length == 1)
                {
                    rows = 1;
                    cols = param.Shape[0];
                }
                else if (param.Shape.Length == 2)
                {
                    rows = param.Shape[0];
                    cols = param.Shape[1];
                }
                else
                {
                    // Flatten higher dimensions to 2D
                    rows = param.Shape[0];
                    cols = param.Data.Length / rows;
                }
                
                // Quantize using Q8
                var q8Tensor = Q8Tensor.Quantize(param.Data, rows, cols, blockSize: 32);
                quantizedTensors[tensorName] = q8Tensor;
                
                Console.WriteLine($"  {tensorName}: [{rows} x {cols}] -> Q8");
                tensorIdx++;
            }
            
            // Prepare metadata - use string values for compatibility with JsonElement serialization
            var metadata = new Dictionary<string, object>
            {
                { "model_name", "benchmark_tiny" },
                { "format_version", "1" },
                { "vocab_size", checkpoint.Metadata.VocabSize.ToString() },
                { "block_size", checkpoint.Metadata.BlockSize.ToString() },
                { "embed_dim", checkpoint.Metadata.EmbedDim.ToString() },
                { "num_layers", checkpoint.Metadata.NumLayers.ToString() },
                { "num_heads", checkpoint.Metadata.NumHeads.ToString() },
                { "created_utc", DateTime.UtcNow.ToString("O") }
            };
            
            // Write SMQ file
            Console.WriteLine($"\nWriting SMQ model to: {outputPath}");
            using (var outputStream = File.Create(outputPath))
            using (var writer = new SmqWriter(outputStream))
            {
                long bytesWritten = writer.WriteModel(quantizedTensors, metadata);
                Console.WriteLine($"✓ Wrote {bytesWritten} bytes");
            }
            
            var fileInfo = new FileInfo(outputPath);
            Console.WriteLine($"✓ Model size: {fileInfo.Length / 1024.0:F2} KB");
            Console.WriteLine($"\n✓ Benchmark model ready: {outputPath}");
        }
    }
}
