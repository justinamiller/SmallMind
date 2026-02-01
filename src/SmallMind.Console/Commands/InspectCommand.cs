using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SmallMind.Quantization.IO.Smq;

namespace SmallMind.ConsoleApp.Commands
{
    /// <summary>
    /// CLI command to inspect SMQ model details.
    /// </summary>
    public sealed class InspectCommand : ICommand
    {
        public string Name => "inspect";
        public string Description => "Display SMQ model information";

        public async Task<int> ExecuteAsync(string[] args)
        {
            if (args.Length < 1)
            {
                ShowUsage();
                return 1;
            }

            string smqPath = args[0];
            bool verbose = args.Contains("--verbose") || args.Contains("-v");
            bool showTensors = args.Contains("--tensors") || args.Contains("-t");

            if (!File.Exists(smqPath))
            {
                System.Console.Error.WriteLine($"Error: File not found: {smqPath}");
                return 1;
            }

            try
            {
                using var stream = File.OpenRead(smqPath);
                using var reader = new SmqReader(stream);
                
                reader.ReadHeader();

                System.Console.WriteLine($"=== SMQ Model: {Path.GetFileName(smqPath)} ===");
                System.Console.WriteLine();

                // File info
                var fileInfo = new FileInfo(smqPath);
                System.Console.WriteLine("File Information:");
                System.Console.WriteLine($"  Size: {FormatBytes(fileInfo.Length)}");
                System.Console.WriteLine($"  Created: {fileInfo.CreationTime}");
                System.Console.WriteLine();

                // Metadata
                var metadata = reader.GetMetadata();
                if (metadata != null && metadata.Count > 0)
                {
                    System.Console.WriteLine("Model Metadata:");
                    foreach (var kvp in metadata.OrderBy(x => x.Key))
                    {
                        System.Console.WriteLine($"  {kvp.Key}: {kvp.Value}");
                    }
                    System.Console.WriteLine();
                }

                // Tensor summary
                var tensorNames = reader.GetTensorNames().ToArray();
                System.Console.WriteLine($"Tensors: {tensorNames.Length} total");

                if (showTensors)
                {
                    System.Console.WriteLine();
                    System.Console.WriteLine("Tensor Details:");
                    System.Console.WriteLine($"{"Name",-40} {"Type",-10} {"Shape",-20} {"Size",15}");
                    System.Console.WriteLine(new string('-', 90));

                    foreach (var name in tensorNames.OrderBy(x => x))
                    {
                        var tensor = reader.LoadTensor(name);
                        
                        string type, shape, size;
                        
                        if (tensor is SmallMind.Quantization.Tensors.Q8Tensor q8)
                        {
                            type = "Q8_0";
                            shape = $"{q8.Rows}×{q8.Cols}";
                            size = FormatBytes(q8.Data.Length + q8.Scales.Length * 4);
                        }
                        else if (tensor is SmallMind.Quantization.Tensors.Q4Tensor q4)
                        {
                            type = "Q4_0";
                            shape = $"{q4.Rows}×{q4.Cols}";
                            size = FormatBytes(q4.Data.Length + q4.Scales.Length * 4);
                        }
                        else
                        {
                            type = tensor.GetType().Name;
                            shape = "unknown";
                            size = "unknown";
                        }

                        System.Console.WriteLine($"{name,-40} {type,-10} {shape,-20} {size,15}");
                    }
                }

                // Manifest info
                string manifestPath = smqPath + ".manifest.json";
                if (File.Exists(manifestPath))
                {
                    System.Console.WriteLine();
                    System.Console.WriteLine($"Manifest: {Path.GetFileName(manifestPath)}");
                    
                    if (verbose)
                    {
                        var manifestContent = File.ReadAllText(manifestPath);
                        System.Console.WriteLine();
                        System.Console.WriteLine("Manifest Content:");
                        System.Console.WriteLine(manifestContent);
                    }
                }

                return 0;
            }
            catch (Exception ex)
            {
                System.Console.Error.WriteLine($"Error inspecting SMQ file: {ex.Message}");
                if (args.Contains("--verbose"))
                {
                    System.Console.Error.WriteLine(ex.StackTrace);
                }
                return 1;
            }
        }

        public void ShowUsage()
        {
            System.Console.WriteLine("Usage: smallmind inspect <model.smq> [options]");
            System.Console.WriteLine();
            System.Console.WriteLine("Arguments:");
            System.Console.WriteLine("  <model.smq>    Path to SMQ model file");
            System.Console.WriteLine();
            System.Console.WriteLine("Options:");
            System.Console.WriteLine("  -v, --verbose  Show detailed information including manifest");
            System.Console.WriteLine("  -t, --tensors  List all tensors with details");
            System.Console.WriteLine();
            System.Console.WriteLine("Examples:");
            System.Console.WriteLine("  smallmind inspect model.smq");
            System.Console.WriteLine("  smallmind inspect model.smq --tensors");
            System.Console.WriteLine("  smallmind inspect model.smq -v -t");
        }

        private static string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            return $"{len:F2} {sizes[order]}";
        }
    }
}
