using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using SmallMind.Core;
using SmallMind.Core.Utilities;
using SmallMind.Transformers;
using SmallMind.Quantization.Tensors;
using SmallMind.Quantization.IO.Smq;

namespace SmallMind.ConsoleApp.Commands
{
    /// <summary>
    /// CLI command to quantize FP32 models to SMQ format.
    /// </summary>
    internal sealed class QuantizeCommand : ICommand
    {
        public string Name => "quantize";
        public string Description => "Convert FP32 model checkpoint to quantized SMQ format";

        public async Task<int> ExecuteAsync(string[] args)
        {
            if (args.Length < 2)
            {
                ShowUsage();
                return 1;
            }

            string inputPath = args[0];
            string outputPath = args[1];
            string scheme = GetArgValue(args, "--scheme", "Q8_0").ToUpper();
            int blockSize = int.Parse(GetArgValue(args, "--block-size", "64"));

            if (!File.Exists(inputPath))
            {
                System.Console.Error.WriteLine($"Error: Input file not found: {inputPath}");
                return 1;
            }

            if (!scheme.Equals("Q8_0") && !scheme.Equals("Q4_0"))
            {
                System.Console.Error.WriteLine($"Error: Unsupported quantization scheme: {scheme}");
                System.Console.Error.WriteLine("Supported schemes: Q8_0, Q4_0");
                return 1;
            }

            try
            {
                System.Console.WriteLine($"Loading FP32 model from: {inputPath}");
                var store = new BinaryCheckpointStore();
                var checkpoint = await store.LoadAsync(inputPath);

                System.Console.WriteLine($"Model loaded: {checkpoint.Parameters.Count} parameters");
                System.Console.WriteLine($"Quantization scheme: {scheme}");
                System.Console.WriteLine($"Block size: {blockSize}");
                System.Console.WriteLine();

                // Quantize all parameters
                var tensors = new Dictionary<string, object>();
                int tensorIndex = 0;

                foreach (var param in checkpoint.Parameters)
                {
                    string tensorName = $"param_{tensorIndex:D4}";
                    System.Console.Write($"Quantizing {tensorName} ({param.Shape.Length}D: {string.Join("x", param.Shape)})... ");

                    // Flatten to 2D for quantization
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
                        rows = param.Shape[0];
                        cols = param.Data.Length / rows;
                    }

                    if (scheme == "Q8_0")
                    {
                        var q8 = Q8Tensor.Quantize(param.Data, rows, cols, blockSize);
                        tensors[tensorName] = q8;
                        System.Console.WriteLine($"Q8 ({q8.Data.Length} bytes + {q8.Scales.Length * 4} bytes scales)");
                    }
                    else // Q4_0
                    {
                        var q4 = Q4Tensor.Quantize(param.Data, rows, cols, blockSize);
                        tensors[tensorName] = q4;
                        System.Console.WriteLine($"Q4 ({q4.Data.Length} bytes + {q4.Scales.Length * 4} bytes scales)");
                    }

                    tensorIndex++;
                }

                // Build metadata
                var metadata = new Dictionary<string, object>
                {
                    ["model_type"] = checkpoint.Metadata.ModelType ?? "TransformerModel",
                    ["vocab_size"] = checkpoint.Metadata.VocabSize,
                    ["block_size"] = checkpoint.Metadata.BlockSize,
                    ["embed_dim"] = checkpoint.Metadata.EmbedDim,
                    ["num_heads"] = checkpoint.Metadata.NumHeads,
                    ["num_layers"] = checkpoint.Metadata.NumLayers,
                    ["quantization_scheme"] = scheme,
                    ["quantization_block_size"] = blockSize,
                    ["created_utc"] = DateTime.UtcNow.ToString("o")
                };

                // Write SMQ file
                using (var outputStream = File.Create(outputPath))
                using (var writer = new SmqWriter(outputStream))
                {
                    writer.WriteModel(tensors, metadata);
                }

                // Write manifest
                var manifest = new SmqManifest
                {
                    FormatVersion = 1,
                    ModelName = Path.GetFileNameWithoutExtension(outputPath),
                    CreatedUtc = DateTime.UtcNow,
                    TensorCount = checkpoint.Parameters.Count,
                    QuantSchemes = new List<string> { scheme },
                    ModelDims = new ModelDimensions
                    {
                        NLayers = checkpoint.Metadata.NumLayers,
                        HiddenDim = checkpoint.Metadata.EmbedDim,
                        VocabSize = checkpoint.Metadata.VocabSize,
                        ContextLength = checkpoint.Metadata.BlockSize
                    }
                };

                string manifestPath = outputPath + ".manifest.json";
                string manifestJson = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(manifestPath, manifestJson);

                System.Console.WriteLine();
                System.Console.WriteLine($"✓ Quantization complete!");
                System.Console.WriteLine($"  Output: {outputPath}");
                System.Console.WriteLine($"  Manifest: {manifestPath}");

                long originalSize = new FileInfo(inputPath).Length;
                long quantizedSize = new FileInfo(outputPath).Length;
                double compressionRatio = (double)originalSize / quantizedSize;

                System.Console.WriteLine($"  Original size: {ByteSizeFormatter.FormatBytes(originalSize)}");
                System.Console.WriteLine($"  Quantized size: {ByteSizeFormatter.FormatBytes(quantizedSize)}");
                System.Console.WriteLine($"  Compression: {compressionRatio:F2}x");

                return 0;
            }
            catch (Exception ex)
            {
                System.Console.Error.WriteLine($"Error during quantization: {ex.Message}");
                System.Console.Error.WriteLine(ex.StackTrace);
                return 1;
            }
        }

        public void ShowUsage()
        {
            System.Console.WriteLine("Usage: smallmind quantize <input.json> <output.smq> [options]");
            System.Console.WriteLine();
            System.Console.WriteLine("Arguments:");
            System.Console.WriteLine("  <input.json>   Path to FP32 model checkpoint (JSON format)");
            System.Console.WriteLine("  <output.smq>   Path to output quantized model file");
            System.Console.WriteLine();
            System.Console.WriteLine("Options:");
            System.Console.WriteLine("  --scheme <Q8_0|Q4_0>   Quantization scheme (default: Q8_0)");
            System.Console.WriteLine("  --block-size <n>       Block size for quantization (default: 64)");
            System.Console.WriteLine();
            System.Console.WriteLine("Examples:");
            System.Console.WriteLine("  smallmind quantize model.json model.smq");
            System.Console.WriteLine("  smallmind quantize model.json model_q4.smq --scheme Q4_0 --block-size 32");
        }

        private static string GetArgValue(string[] args, string name, string defaultValue)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase))
                    return args[i + 1];
            }
            return defaultValue;
        }
    }
}
