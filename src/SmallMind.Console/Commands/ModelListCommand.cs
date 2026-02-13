using SmallMind.Core.Utilities;

namespace SmallMind.ConsoleApp.Commands
{
    /// <summary>
    /// CLI command to list registered models.
    /// </summary>
    internal sealed class ModelListCommand : ICommand
    {
        public string Name => "model list";
        public string Description => "List all registered models";

        public Task<int> ExecuteAsync(string[] args)
        {
            if (HasFlag(args, "--help") || HasFlag(args, "-h"))
            {
                ShowUsage();
                return Task.FromResult(0);
            }

            string? cacheDir = GetArgValue(args, "--cache-dir");

            try
            {
                var registry = new ModelRegistry.ModelRegistry(cacheDir);
                var models = registry.ListModels();

                if (models.Count == 0)
                {
                    Console.WriteLine("No models registered.");
                    Console.WriteLine($"Cache directory: {registry.CacheRoot}");
                    return Task.FromResult(0);
                }

                Console.WriteLine($"Registered models ({models.Count}):");
                Console.WriteLine($"Cache directory: {registry.CacheRoot}");
                Console.WriteLine();

                // Print header
                Console.WriteLine($"{"ID",-30} {"Name",-30} {"Format",-10} {"Size",-15} {"Created"}");
                Console.WriteLine(new string('-', 110));

                foreach (var model in models)
                {
                    long totalSize = 0;
                    foreach (var file in model.Files)
                    {
                        totalSize += file.SizeBytes;
                    }

                    string sizeStr = ByteSizeFormatter.FormatBytes(totalSize);
                    string createdStr = ParseAndFormatDate(model.CreatedUtc);

                    Console.WriteLine($"{Truncate(model.ModelId, 30),-30} {Truncate(model.DisplayName, 30),-30} {model.Format,-10} {sizeStr,-15} {createdStr}");
                }

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
            Console.WriteLine("List all registered models");
            Console.WriteLine();
            Console.WriteLine("Usage: smallmind model list [options]");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  --cache-dir <path>  Override the cache directory");
            Console.WriteLine();
            Console.WriteLine("Example:");
            Console.WriteLine("  smallmind model list");
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

        private string ParseAndFormatDate(string dateStr)
        {
            if (DateTime.TryParse(dateStr, out DateTime dt))
            {
                return dt.ToString("yyyy-MM-dd");
            }
            return dateStr;
        }

        private string Truncate(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            if (text.Length <= maxLength)
                return text;

            return text.Substring(0, maxLength - 3) + "...";
        }
    }
}
