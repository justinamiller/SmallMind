namespace SmallMind.ConsoleApp.Commands
{
    /// <summary>
    /// CLI command to add a model to the registry.
    /// </summary>
    internal sealed class ModelAddCommand : ICommand
    {
        public string Name => "model add";
        public string Description => "Add a model to the registry from a file or URL";

        public async Task<int> ExecuteAsync(string[] args)
        {
            if (args.Length < 1 || HasFlag(args, "--help") || HasFlag(args, "-h"))
            {
                ShowUsage();
                return args.Length < 1 ? 1 : 0;
            }

            string source = args[0];
            string? modelId = GetArgValue(args, "--id");
            string? displayName = GetArgValue(args, "--name");
            string? cacheDir = GetArgValue(args, "--cache-dir");

            try
            {
                var registry = new ModelRegistry.ModelRegistry(cacheDir);

                Console.WriteLine($"Adding model from: {source}");
                Console.WriteLine($"Cache directory: {registry.CacheRoot}");
                Console.WriteLine();

                string registeredId = await registry.AddModelAsync(source, modelId, displayName);

                Console.WriteLine($"Model registered successfully!");
                Console.WriteLine($"Model ID: {registeredId}");

                // Show verification result
                var verification = registry.VerifyModel(registeredId);
                if (verification.IsValid)
                {
                    Console.WriteLine("Verification: PASSED");
                }
                else
                {
                    Console.WriteLine("Verification: FAILED");
                    foreach (var error in verification.Errors)
                    {
                        Console.WriteLine($"  - {error}");
                    }
                }

                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                return 1;
            }
        }

        public void ShowUsage()
        {
            Console.WriteLine("Add a model to the registry");
            Console.WriteLine();
            Console.WriteLine("Usage: smallmind model add <path-or-url> [options]");
            Console.WriteLine();
            Console.WriteLine("Arguments:");
            Console.WriteLine("  <path-or-url>    Local file path or HTTP(S) URL to the model file");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  --id <id>        Custom model ID (default: auto-generated from filename)");
            Console.WriteLine("  --name <name>    Display name for the model (default: same as ID)");
            Console.WriteLine("  --cache-dir <path>  Override the cache directory");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  smallmind model add ./my-model.gguf");
            Console.WriteLine("  smallmind model add ./my-model.gguf --id my-model --name \"My Custom Model\"");
            Console.WriteLine("  smallmind model add https://example.com/model.gguf --id remote-model");
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
