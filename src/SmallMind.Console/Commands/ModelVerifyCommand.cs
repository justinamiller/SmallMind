using System;
using System.Threading.Tasks;
using SmallMind.ModelRegistry;

namespace SmallMind.ConsoleApp.Commands
{
    /// <summary>
    /// CLI command to verify model integrity.
    /// </summary>
    public sealed class ModelVerifyCommand : ICommand
    {
        public string Name => "model verify";
        public string Description => "Verify model integrity (files, sizes, checksums)";

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
                
                Console.WriteLine($"Verifying model: {modelId}");
                Console.WriteLine($"Cache directory: {registry.CacheRoot}");
                Console.WriteLine();

                var result = registry.VerifyModel(modelId);

                if (result.IsValid)
                {
                    Console.WriteLine("✓ Verification PASSED");
                    Console.WriteLine("All files are present and checksums match.");
                    return Task.FromResult(0);
                }
                else
                {
                    Console.WriteLine("✗ Verification FAILED");
                    Console.WriteLine();
                    Console.WriteLine("Errors:");
                    foreach (var error in result.Errors)
                    {
                        Console.WriteLine($"  - {error}");
                    }
                    return Task.FromResult(1);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                return Task.FromResult(1);
            }
        }

        public void ShowUsage()
        {
            Console.WriteLine("Verify model integrity");
            Console.WriteLine();
            Console.WriteLine("Usage: smallmind model verify <model-id> [options]");
            Console.WriteLine();
            Console.WriteLine("Arguments:");
            Console.WriteLine("  <model-id>       Model ID to verify");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  --cache-dir <path>  Override the cache directory");
            Console.WriteLine();
            Console.WriteLine("Example:");
            Console.WriteLine("  smallmind model verify my-model");
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
    }
}
