using System;
using System.Threading.Tasks;
using SmallMind.ModelRegistry;

namespace SmallMind.ConsoleApp.Commands
{
    /// <summary>
    /// CLI command to inspect model details.
    /// </summary>
    public sealed class ModelInspectCommand : ICommand
    {
        public string Name => "model inspect";
        public string Description => "Show detailed information about a model";

        public Task<int> ExecuteAsync(string[] args)
        {
            if (args.Length < 1 || HasFlag(args, "--help") || HasFlag(args, "-h"))
            {
                ShowUsage();
                return Task.FromResult(args.Length < 1 ? 1 : 0);
            }

            string modelId = args[0];
            string? cacheDir = GetArgValue(args, "--cache-dir");

            try
            {
                var registry = new ModelRegistry.ModelRegistry(cacheDir);
                var manifest = registry.GetManifest(modelId);

                if (manifest == null)
                {
                    Console.Error.WriteLine($"Model not found: {modelId}");
                    return Task.FromResult(1);
                }

                Console.WriteLine($"Model: {manifest.ModelId}");
                Console.WriteLine($"Display Name: {manifest.DisplayName}");
                Console.WriteLine($"Format: {manifest.Format}");
                Console.WriteLine($"Source: {manifest.Source}");
                Console.WriteLine($"Created: {manifest.CreatedUtc}");
                
                if (!string.IsNullOrWhiteSpace(manifest.Quantization))
                {
                    Console.WriteLine($"Quantization: {manifest.Quantization}");
                }
                
                if (!string.IsNullOrWhiteSpace(manifest.TokenizerId))
                {
                    Console.WriteLine($"Tokenizer ID: {manifest.TokenizerId}");
                }
                
                if (manifest.MaxContextTokens.HasValue)
                {
                    Console.WriteLine($"Max Context Tokens: {manifest.MaxContextTokens.Value}");
                }
                
                if (!string.IsNullOrWhiteSpace(manifest.Notes))
                {
                    Console.WriteLine($"Notes: {manifest.Notes}");
                }

                Console.WriteLine();
                Console.WriteLine("Files:");
                
                long totalSize = 0;
                foreach (var file in manifest.Files)
                {
                    totalSize += file.SizeBytes;
                    Console.WriteLine($"  - {file.Path}");
                    Console.WriteLine($"    Size: {FormatBytes(file.SizeBytes)}");
                    Console.WriteLine($"    SHA256: {file.Sha256}");
                }

                Console.WriteLine();
                Console.WriteLine($"Total Size: {FormatBytes(totalSize)}");

                return Task.FromResult(0);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                return Task.FromResult(1);
            }
        }

        public void ShowUsage()
        {
            Console.WriteLine("Show detailed information about a model");
            Console.WriteLine();
            Console.WriteLine("Usage: smallmind model inspect <model-id> [options]");
            Console.WriteLine();
            Console.WriteLine("Arguments:");
            Console.WriteLine("  <model-id>       Model ID to inspect");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  --cache-dir <path>  Override the cache directory");
            Console.WriteLine();
            Console.WriteLine("Example:");
            Console.WriteLine("  smallmind model inspect my-model");
        }

        private bool HasFlag(string[] args, string flag)
        {
            return Array.Exists(args, arg => arg.Equals(flag, StringComparison.OrdinalIgnoreCase));
        }

        private string? GetArgValue(string[] args, string key)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i].Equals(key, StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }
            return null;
        }

        private string FormatBytes(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int suffixIndex = 0;
            double size = bytes;

            while (size >= 1024 && suffixIndex < suffixes.Length - 1)
            {
                size /= 1024;
                suffixIndex++;
            }

            return $"{size:F2} {suffixes[suffixIndex]}";
        }
    }
}
