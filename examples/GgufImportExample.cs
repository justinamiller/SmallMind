using System;
using System.IO;
using SmallMind.Quantization.IO.Gguf;

namespace SmallMind.Quantization.Examples
{
    /// <summary>
    /// Example: Import a GGUF model file to SmallMind's SMQ format.
    /// </summary>
    public class GgufImportExample
    {
        public static void Main(string[] args)
        {
            // Example 1: Simple import
            SimpleImport();

            // Example 2: Inspect GGUF before import
            InspectBeforeImport();
        }

        /// <summary>
        /// Simple one-line import from GGUF to SMQ.
        /// </summary>
        static void SimpleImport()
        {
            string ggufPath = "model-q8_0.gguf";
            string smqPath = "model.smq";

            try
            {
                var importer = new GgufImporter();
                importer.ImportToSmq(ggufPath, smqPath);
                Console.WriteLine($"Successfully imported {ggufPath} to {smqPath}");
            }
            catch (FileNotFoundException ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
            catch (NotSupportedException ex)
            {
                // This exception lists all unsupported tensor types
                Console.WriteLine("Import failed due to unsupported tensor types:");
                Console.WriteLine(ex.Message);
            }
        }

        /// <summary>
        /// Inspect GGUF metadata and tensors before importing.
        /// </summary>
        static void InspectBeforeImport()
        {
            string ggufPath = "model-q4_0.gguf";

            using var stream = File.OpenRead(ggufPath);
            using var reader = new GgufReader(stream);

            // Read model info
            var modelInfo = reader.ReadModelInfo();

            // Display basic info
            Console.WriteLine($"GGUF Version: {modelInfo.Version}");
            Console.WriteLine($"Alignment: {modelInfo.Alignment} bytes");
            Console.WriteLine($"Tensor count: {modelInfo.Tensors.Count}");
            Console.WriteLine($"Metadata keys: {modelInfo.Metadata.Count}");
            Console.WriteLine();

            // Display metadata
            Console.WriteLine("=== Metadata ===");
            foreach (var kvp in modelInfo.Metadata)
            {
                Console.WriteLine($"  {kvp.Key}: {kvp.Value}");
            }
            Console.WriteLine();

            // Display tensors
            Console.WriteLine("=== Tensors ===");
            ulong totalSize = 0;
            foreach (var tensor in modelInfo.Tensors)
            {
                string dims = string.Join(" x ", tensor.Dimensions);
                Console.WriteLine($"  {tensor.Name}:");
                Console.WriteLine($"    Type: {tensor.Type}");
                Console.WriteLine($"    Dims: {dims}");
                Console.WriteLine($"    Size: {tensor.Size:N0} bytes");
                totalSize += tensor.Size;
            }
            Console.WriteLine();
            Console.WriteLine($"Total tensor data: {totalSize:N0} bytes ({totalSize / 1024.0 / 1024.0:F2} MB)");

            // Check if importable
            bool canImport = true;
            foreach (var tensor in modelInfo.Tensors)
            {
                if (tensor.Type != GgufTensorType.Q8_0 && tensor.Type != GgufTensorType.Q4_0)
                {
                    Console.WriteLine($"WARNING: Tensor '{tensor.Name}' has unsupported type: {tensor.Type}");
                    canImport = false;
                }
            }

            if (canImport)
            {
                Console.WriteLine("\n✓ All tensors are compatible with SMQ format (Q8_0 or Q4_0)");
                Console.WriteLine("Ready to import!");
            }
            else
            {
                Console.WriteLine("\n✗ Some tensors are not compatible with SMQ format");
                Console.WriteLine("Supported types: Q8_0, Q4_0");
            }
        }

        /// <summary>
        /// Advanced: Read and process tensor data.
        /// </summary>
        static void ProcessTensorData()
        {
            string ggufPath = "model.gguf";

            using var stream = File.OpenRead(ggufPath);
            using var reader = new GgufReader(stream);
            var modelInfo = reader.ReadModelInfo();

            // Find a specific tensor
            var embedTensor = modelInfo.Tensors.Find(t => t.Name.Contains("embed"));
            if (embedTensor != null)
            {
                Console.WriteLine($"Reading tensor: {embedTensor.Name}");
                Console.WriteLine($"  Offset: 0x{embedTensor.Offset:X}");
                Console.WriteLine($"  Size: {embedTensor.Size} bytes");

                // Read raw tensor data
                byte[] data = reader.ReadTensorData(embedTensor.Offset, embedTensor.Size);
                Console.WriteLine($"  Read {data.Length} bytes");

                // Process data based on type
                switch (embedTensor.Type)
                {
                    case GgufTensorType.Q8_0:
                        Console.WriteLine("  Type: Q8_0 (8-bit quantized)");
                        // Parse Q8_0 blocks: each block is 2 bytes (fp16 scale) + 32 bytes (int8 data)
                        int q8BlockSize = 32;
                        int q8BytesPerBlock = 2 + q8BlockSize;
                        int numBlocks = (int)embedTensor.Size / q8BytesPerBlock;
                        Console.WriteLine($"  Blocks: {numBlocks}");
                        break;

                    case GgufTensorType.Q4_0:
                        Console.WriteLine("  Type: Q4_0 (4-bit quantized)");
                        // Parse Q4_0 blocks: each block is 2 bytes (fp16 scale) + 16 bytes (packed 4-bit data)
                        int q4BlockSize = 32;
                        int q4BytesPerBlock = 2 + (q4BlockSize / 2);
                        int q4NumBlocks = (int)embedTensor.Size / q4BytesPerBlock;
                        Console.WriteLine($"  Blocks: {q4NumBlocks}");
                        break;

                    default:
                        Console.WriteLine($"  Type: {embedTensor.Type} (not supported)");
                        break;
                }
            }
        }
    }
}
