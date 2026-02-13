using SmallMind.Core.Utilities;
using SmallMind.Quantization.IO.Gguf;

namespace SmallMind.ConsoleApp.Commands
{
    /// <summary>
    /// CLI command to import GGUF models to SMQ format.
    /// </summary>
    internal sealed class ImportGgufCommand : ICommand
    {
        public string Name => "import-gguf";
        public string Description => "Import GGUF model to SMQ format";

        public async Task<int> ExecuteAsync(string[] args)
        {
            if (args.Length < 2)
            {
                ShowUsage();
                return 1;
            }

            string inputPath = args[0];
            string outputPath = args[1];

            if (!File.Exists(inputPath))
            {
                System.Console.Error.WriteLine($"Error: Input file not found: {inputPath}");
                return 1;
            }

            try
            {
                System.Console.WriteLine($"Importing GGUF model from: {inputPath}");
                System.Console.WriteLine($"Output SMQ file: {outputPath}");
                System.Console.WriteLine();

                var importer = new GgufImporter();
                importer.ImportToSmq(inputPath, outputPath);

                System.Console.WriteLine();
                System.Console.WriteLine($"✓ Import complete!");
                System.Console.WriteLine($"  Output: {outputPath}");
                System.Console.WriteLine($"  Manifest: {outputPath}.manifest.json");

                long ggufSize = new FileInfo(inputPath).Length;
                long smqSize = new FileInfo(outputPath).Length;

                System.Console.WriteLine($"  GGUF size: {ByteSizeFormatter.FormatBytes(ggufSize)}");
                System.Console.WriteLine($"  SMQ size: {ByteSizeFormatter.FormatBytes(smqSize)}");

                return 0;
            }
            catch (NotSupportedException ex)
            {
                System.Console.Error.WriteLine($"Error: {ex.Message}");
                System.Console.Error.WriteLine();
                System.Console.Error.WriteLine("Note: SmallMind only supports Q8_0 and Q4_0 quantization schemes.");
                System.Console.Error.WriteLine("Models using other quantization types (K-quants, Q4_1, Q5_0, etc.) are not supported.");
                return 1;
            }
            catch (Exception ex)
            {
                System.Console.Error.WriteLine($"Error during import: {ex.Message}");
                System.Console.Error.WriteLine(ex.StackTrace);
                return 1;
            }
        }

        public void ShowUsage()
        {
            System.Console.WriteLine("Usage: smallmind import-gguf <input.gguf> <output.smq>");
            System.Console.WriteLine();
            System.Console.WriteLine("Arguments:");
            System.Console.WriteLine("  <input.gguf>   Path to GGUF model file");
            System.Console.WriteLine("  <output.smq>   Path to output SMQ file");
            System.Console.WriteLine();
            System.Console.WriteLine("Supported GGUF quantization types:");
            System.Console.WriteLine("  - Q8_0 (8-bit symmetric)");
            System.Console.WriteLine("  - Q4_0 (4-bit symmetric)");
            System.Console.WriteLine();
            System.Console.WriteLine("Notes:");
            System.Console.WriteLine("  - GGUF uses 32-element blocks; SMQ uses 64-element blocks");
            System.Console.WriteLine("  - Tensors will be re-quantized during import for block size compatibility");
            System.Console.WriteLine("  - K-quants and other quantization types are not supported");
            System.Console.WriteLine();
            System.Console.WriteLine("Example:");
            System.Console.WriteLine("  smallmind import-gguf model.gguf model.smq");
        }
    }
}
