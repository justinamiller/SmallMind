using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SmallMind.Benchmarks.Core.Models;
using SmallMind.Benchmarks.Options;

namespace SmallMind.Benchmarks.Commands
{
    internal sealed class DownloadCommand
    {
        public async Task<int> ExecuteAsync(string[] args)
        {
            var parser = new CommandLineParser(args);

            if (parser.HasOption("help") || parser.HasOption("h"))
            {
                ShowUsage();
                return 0;
            }

            try
            {
                var config = ParseConfiguration(parser);
                ValidateConfiguration(config);

                Console.WriteLine("SmallMind Benchmarks - Download Models");
                Console.WriteLine("=".PadRight(80, '='));
                Console.WriteLine();

                // Load manifest
                var manifest = ModelDownloader.LoadManifest(config.ManifestPath);
                Console.WriteLine($"Loaded manifest: {config.ManifestPath}");
                Console.WriteLine($"Total models: {manifest.Models.Length}");
                Console.WriteLine();

                // Filter models
                var modelsToDownload = manifest.Models.AsEnumerable();

                if (config.CiOnly)
                {
                    modelsToDownload = modelsToDownload.Where(m => m.CI);
                    Console.WriteLine("Filtering: CI models only");
                }

                if (!string.IsNullOrWhiteSpace(config.ModelName))
                {
                    modelsToDownload = modelsToDownload.Where(m =>
                        m.Name.Equals(config.ModelName, StringComparison.OrdinalIgnoreCase) ||
                        m.DisplayName.Equals(config.ModelName, StringComparison.OrdinalIgnoreCase));
                    Console.WriteLine($"Filtering: Model name = {config.ModelName}");
                }

                var models = modelsToDownload.ToArray();

                if (models.Length == 0)
                {
                    Console.WriteLine("No models match the specified criteria");
                    return 1;
                }

                Console.WriteLine($"Models to download: {models.Length}");
                Console.WriteLine();

                // Display models
                Console.WriteLine("Models:");
                foreach (var model in models)
                {
                    Console.WriteLine($"  - {model.DisplayName} ({FormatBytes(model.SizeBytes)}) {(model.CI ? "[CI]" : "")}");
                }
                Console.WriteLine();

                // Download models
                var downloader = new ModelDownloader(config.CacheDirectory);
                int successCount = 0;
                int failCount = 0;

                for (int i = 0; i < models.Length; i++)
                {
                    var model = models[i];
                    Console.WriteLine($"[{i + 1}/{models.Length}] {model.DisplayName}");

                    try
                    {
                        var progress = new Progress<DownloadProgress>(p =>
                        {
                            // Progress is printed by ModelDownloader
                        });

                        string modelPath = await downloader.DownloadModelAsync(model, progress, CancellationToken.None);
                        Console.WriteLine($"✅ Success: {modelPath}");
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"❌ Failed: {ex.Message}");
                        failCount++;

                        if (!config.ContinueOnError)
                        {
                            return 1;
                        }
                    }

                    Console.WriteLine();
                }

                Console.WriteLine("=".PadRight(80, '='));
                Console.WriteLine($"Download complete: {successCount} succeeded, {failCount} failed");

                return failCount > 0 ? 1 : 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                return 1;
            }
        }

        private DownloadConfiguration ParseConfiguration(CommandLineParser parser)
        {
            return new DownloadConfiguration
            {
                ManifestPath = parser.GetOption("manifest", "../models/models.manifest.json"),
                ModelName = parser.GetOption("model"),
                CiOnly = parser.GetOptionBool("ci-only"),
                CacheDirectory = parser.GetOption("cache"),
                ContinueOnError = parser.GetOptionBool("continue-on-error", true)
            };
        }

        private void ValidateConfiguration(DownloadConfiguration config)
        {
            if (!File.Exists(config.ManifestPath))
            {
                throw new InvalidOperationException($"Manifest not found: {config.ManifestPath}");
            }
        }

        private string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;

            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }

            return $"{len:F2} {sizes[order]}";
        }

        private void ShowUsage()
        {
            Console.WriteLine("Usage: SmallMind.Benchmarks download [options]");
            Console.WriteLine();
            Console.WriteLine("Download models from manifest");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  --manifest <path>    Path to manifest (default: ../models/models.manifest.json)");
            Console.WriteLine("  --model <name>       Specific model to download (or all if not specified)");
            Console.WriteLine("  --ci-only            Download only CI models");
            Console.WriteLine("  --cache <dir>        Model cache directory");
            Console.WriteLine("  --continue-on-error  Continue downloading remaining models on error (default: true)");
            Console.WriteLine("  --help, -h           Show this help message");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  # Download all CI models");
            Console.WriteLine("  SmallMind.Benchmarks download --ci-only");
            Console.WriteLine();
            Console.WriteLine("  # Download specific model");
            Console.WriteLine("  SmallMind.Benchmarks download --model tinyllama-1.1b-q4");
            Console.WriteLine();
            Console.WriteLine("  # Download all models");
            Console.WriteLine("  SmallMind.Benchmarks download");
        }

        private sealed class DownloadConfiguration
        {
            public string ManifestPath { get; set; } = string.Empty;
            public string? ModelName { get; set; }
            public bool CiOnly { get; set; }
            public string? CacheDirectory { get; set; }
            public bool ContinueOnError { get; set; }
        }
    }
}
