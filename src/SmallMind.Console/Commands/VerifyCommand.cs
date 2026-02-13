using System.Text.Json;
using SmallMind.Quantization.IO.Smq;

namespace SmallMind.ConsoleApp.Commands
{
    /// <summary>
    /// CLI command to verify SMQ model integrity.
    /// </summary>
    internal sealed class VerifyCommand : ICommand
    {
        public string Name => "verify";
        public string Description => "Validate SMQ model integrity";

        public async Task<int> ExecuteAsync(string[] args)
        {
            if (args.Length < 1)
            {
                ShowUsage();
                return 1;
            }

            string smqPath = args[0];
            bool verbose = args.Contains("--verbose") || args.Contains("-v");

            if (!File.Exists(smqPath))
            {
                System.Console.Error.WriteLine($"Error: File not found: {smqPath}");
                return 1;
            }

            try
            {
                System.Console.WriteLine($"Verifying SMQ file: {smqPath}");
                System.Console.WriteLine();

                // Validate the file
                using var stream = File.OpenRead(smqPath);
                var validationErrors = SmqValidator.Validate(stream);

                var errors = validationErrors.Where(e => e.Severity == SmqValidator.ValidationSeverity.Error).ToList();
                var warnings = validationErrors.Where(e => e.Severity == SmqValidator.ValidationSeverity.Warning).ToList();

                if (errors.Count == 0)
                {
                    System.Console.ForegroundColor = ConsoleColor.Green;
                    System.Console.WriteLine("✓ Validation PASSED");
                    System.Console.ResetColor();
                    System.Console.WriteLine();

                    if (verbose)
                    {
                        System.Console.WriteLine("Validation Details:");
                        System.Console.WriteLine($"  Magic header: OK");
                        System.Console.WriteLine($"  Format version: OK");
                        System.Console.WriteLine($"  Metadata: OK");
                        System.Console.WriteLine($"  Tensor directory: OK");
                        System.Console.WriteLine($"  All tensors loadable: OK");
                    }

                    if (warnings.Count > 0)
                    {
                        System.Console.WriteLine();
                        System.Console.ForegroundColor = ConsoleColor.Yellow;
                        System.Console.WriteLine("Warnings:");
                        System.Console.ResetColor();
                        foreach (var warning in warnings)
                        {
                            System.Console.WriteLine($"  - {warning.Message}");
                        }
                    }

                    // Check manifest
                    string manifestPath = smqPath + ".manifest.json";
                    if (File.Exists(manifestPath))
                    {
                        System.Console.WriteLine();
                        System.Console.WriteLine($"Manifest file: Found");

                        if (verbose)
                        {
                            try
                            {
                                string manifestJson = File.ReadAllText(manifestPath);
                                var manifest = JsonSerializer.Deserialize<SmqManifest>(manifestJson);
                                if (manifest != null)
                                {
                                    System.Console.WriteLine($"  Model name: {manifest.ModelName}");
                                    System.Console.WriteLine($"  Tensor count: {manifest.TensorCount}");
                                    System.Console.WriteLine($"  Created: {manifest.CreatedUtc:o}");
                                    if (manifest.QuantSchemes != null)
                                    {
                                        System.Console.WriteLine($"  Quant schemes: {string.Join(", ", manifest.QuantSchemes)}");
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                System.Console.ForegroundColor = ConsoleColor.Yellow;
                                System.Console.WriteLine($"  Warning: Could not parse manifest: {ex.Message}");
                                System.Console.ResetColor();
                            }
                        }
                    }
                    else
                    {
                        System.Console.WriteLine();
                        System.Console.ForegroundColor = ConsoleColor.Yellow;
                        System.Console.WriteLine($"Warning: Manifest file not found (optional)");
                        System.Console.ResetColor();
                    }

                    return 0;
                }
                else
                {
                    System.Console.ForegroundColor = ConsoleColor.Red;
                    System.Console.WriteLine("✗ Validation FAILED");
                    System.Console.ResetColor();
                    System.Console.WriteLine();

                    System.Console.WriteLine($"Errors ({errors.Count}):");
                    foreach (var error in errors)
                    {
                        System.Console.WriteLine($"  - {error.Message}");
                    }

                    if (warnings.Count > 0)
                    {
                        System.Console.WriteLine();
                        System.Console.ForegroundColor = ConsoleColor.Yellow;
                        System.Console.WriteLine($"Warnings ({warnings.Count}):");
                        System.Console.ResetColor();
                        foreach (var warning in warnings)
                        {
                            System.Console.WriteLine($"  - {warning.Message}");
                        }
                    }

                    return 1;
                }
            }
            catch (Exception ex)
            {
                System.Console.ForegroundColor = ConsoleColor.Red;
                System.Console.Error.WriteLine($"Error during verification: {ex.Message}");
                System.Console.ResetColor();

                if (verbose)
                {
                    System.Console.Error.WriteLine(ex.StackTrace);
                }

                return 1;
            }
        }

        public void ShowUsage()
        {
            System.Console.WriteLine("Usage: smallmind verify <model.smq> [options]");
            System.Console.WriteLine();
            System.Console.WriteLine("Arguments:");
            System.Console.WriteLine("  <model.smq>    Path to SMQ model file");
            System.Console.WriteLine();
            System.Console.WriteLine("Options:");
            System.Console.WriteLine("  -v, --verbose  Show detailed validation information");
            System.Console.WriteLine();
            System.Console.WriteLine("Description:");
            System.Console.WriteLine("  Validates SMQ file integrity including:");
            System.Console.WriteLine("  - Magic header and format version");
            System.Console.WriteLine("  - Metadata structure");
            System.Console.WriteLine("  - Tensor directory integrity");
            System.Console.WriteLine("  - All tensors are loadable");
            System.Console.WriteLine("  - Manifest file (if present)");
            System.Console.WriteLine();
            System.Console.WriteLine("Examples:");
            System.Console.WriteLine("  smallmind verify model.smq");
            System.Console.WriteLine("  smallmind verify model.smq --verbose");
        }
    }
}
