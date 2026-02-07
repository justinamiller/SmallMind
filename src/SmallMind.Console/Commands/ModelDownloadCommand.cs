using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace SmallMind.ConsoleApp.Commands
{
    /// <summary>
    /// CLI command to download GGUF models from HuggingFace.
    /// </summary>
    public sealed class ModelDownloadCommand : ICommand
    {
        public string Name => "model download";
        public string Description => "Download GGUF model from HuggingFace";

        public async Task<int> ExecuteAsync(string[] args)
        {
            if (args.Length < 1)
            {
                ShowUsage();
                return 1;
            }

            string modelId = args[0];
            string? filename = args.Length > 1 ? args[1] : null;
            string? outputPath = args.Length > 2 ? args[2] : null;

            try
            {
                // Parse model ID (format: username/model-name or username/model-name/filename)
                string[] parts = modelId.Split('/');
                if (parts.Length < 2)
                {
                    System.Console.Error.WriteLine("Error: Invalid model ID format.");
                    System.Console.Error.WriteLine("Expected format: username/model-name or username/model-name/filename");
                    return 1;
                }

                string owner = parts[0];
                string repo = parts[1];
                
                // If filename not provided as separate arg, check if it's in model ID
                if (filename == null && parts.Length > 2)
                {
                    filename = parts[2];
                }

                // If still no filename, we need to list files or use default
                if (filename == null)
                {
                    System.Console.Error.WriteLine("Error: Filename required.");
                    System.Console.Error.WriteLine("Provide as: username/model-name/filename.gguf");
                    System.Console.Error.WriteLine("Or: smallmind model download username/model-name filename.gguf");
                    return 1;
                }

                // Default output path
                if (outputPath == null)
                {
                    outputPath = Path.Combine("models", filename);
                }

                // Ensure output directory exists
                string? outputDir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
                {
                    Directory.CreateDirectory(outputDir);
                }

                // Construct HuggingFace URL
                string url = $"https://huggingface.co/{owner}/{repo}/resolve/main/{filename}";

                System.Console.WriteLine($"Downloading from HuggingFace:");
                System.Console.WriteLine($"  Model: {owner}/{repo}");
                System.Console.WriteLine($"  File: {filename}");
                System.Console.WriteLine($"  URL: {url}");
                System.Console.WriteLine($"  Output: {outputPath}");
                System.Console.WriteLine();

                // Download the file
                await DownloadFileAsync(url, outputPath);

                System.Console.WriteLine();
                System.Console.WriteLine($"✓ Download complete!");
                System.Console.WriteLine($"  Saved to: {outputPath}");
                
                long fileSize = new FileInfo(outputPath).Length;
                System.Console.WriteLine($"  Size: {FormatBytes(fileSize)}");
                System.Console.WriteLine();
                System.Console.WriteLine("Next steps:");
                System.Console.WriteLine($"  1. Import to SMQ format:");
                System.Console.WriteLine($"     smallmind import-gguf \"{outputPath}\" \"{outputPath}.smq\"");
                System.Console.WriteLine($"  2. Run inference:");
                System.Console.WriteLine($"     smallmind generate \"{outputPath}.smq\" \"Your prompt here\"");

                return 0;
            }
            catch (HttpRequestException ex)
            {
                System.Console.Error.WriteLine($"Error downloading file: {ex.Message}");
                System.Console.Error.WriteLine();
                System.Console.Error.WriteLine("Common issues:");
                System.Console.Error.WriteLine("  - Check model ID is correct (username/model-name)");
                System.Console.Error.WriteLine("  - Check filename exists in the repository");
                System.Console.Error.WriteLine("  - Check internet connection");
                return 1;
            }
            catch (Exception ex)
            {
                System.Console.Error.WriteLine($"Error: {ex.Message}");
                System.Console.Error.WriteLine(ex.StackTrace);
                return 1;
            }
        }

        private async Task DownloadFileAsync(string url, string outputPath)
        {
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromMinutes(30); // Long timeout for large models
            
            // Add user agent (HuggingFace appreciates this)
            httpClient.DefaultRequestHeaders.Add("User-Agent", "SmallMind/1.0");

            // Get file size first (if available)
            using var headResponse = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, url));
            headResponse.EnsureSuccessStatusCode();
            
            long? totalBytes = headResponse.Content.Headers.ContentLength;

            // Download with progress
            using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            using var contentStream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

            var buffer = new byte[8192];
            long totalRead = 0;
            int lastPercent = -1;
            var startTime = DateTime.UtcNow;

            while (true)
            {
                int bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead == 0) break;

                await fileStream.WriteAsync(buffer, 0, bytesRead);
                totalRead += bytesRead;

                // Show progress
                if (totalBytes.HasValue)
                {
                    int percent = (int)(totalRead * 100 / totalBytes.Value);
                    if (percent != lastPercent && percent % 5 == 0)
                    {
                        var elapsed = DateTime.UtcNow - startTime;
                        double mbPerSec = totalRead / 1024.0 / 1024.0 / Math.Max(1, elapsed.TotalSeconds);
                        
                        System.Console.Write($"\r  Progress: {percent}% ({FormatBytes(totalRead)} / {FormatBytes(totalBytes.Value)}) - {mbPerSec:F2} MB/s");
                        lastPercent = percent;
                    }
                }
                else
                {
                    // No total size, just show bytes downloaded
                    if (totalRead % (1024 * 1024 * 10) == 0) // Every 10 MB
                    {
                        System.Console.Write($"\r  Downloaded: {FormatBytes(totalRead)}");
                    }
                }
            }

            System.Console.WriteLine(); // New line after progress
        }

        public void ShowUsage()
        {
            System.Console.WriteLine("Usage: smallmind model download <model-id> [filename] [output]");
            System.Console.WriteLine();
            System.Console.WriteLine("Arguments:");
            System.Console.WriteLine("  <model-id>   HuggingFace model ID (format: username/model-name or username/model-name/file.gguf)");
            System.Console.WriteLine("  [filename]   GGUF filename to download (if not in model-id)");
            System.Console.WriteLine("  [output]     Output path (default: models/<filename>)");
            System.Console.WriteLine();
            System.Console.WriteLine("Examples:");
            System.Console.WriteLine("  # Download specific file from repository:");
            System.Console.WriteLine("  smallmind model download TheBloke/TinyLlama-1.1B-Chat-v1.0-GGUF tinyllama-1.1b-chat-v1.0.Q4_0.gguf");
            System.Console.WriteLine();
            System.Console.WriteLine("  # Download with custom output path:");
            System.Console.WriteLine("  smallmind model download TheBloke/TinyLlama-1.1B-Chat-v1.0-GGUF tinyllama-1.1b-chat-v1.0.Q8_0.gguf my-model.gguf");
            System.Console.WriteLine();
            System.Console.WriteLine("  # Using combined format:");
            System.Console.WriteLine("  smallmind model download TheBloke/TinyLlama-1.1B-Chat-v1.0-GGUF/tinyllama-1.1b-chat-v1.0.Q4_0.gguf");
            System.Console.WriteLine();
            System.Console.WriteLine("Notes:");
            System.Console.WriteLine("  - Downloads from https://huggingface.co/{username}/{model-name}/resolve/main/{filename}");
            System.Console.WriteLine("  - Supported GGUF quantizations: Q8_0, Q4_0 (other types will fail at import)");
            System.Console.WriteLine("  - After download, use 'import-gguf' to convert to SMQ format");
            System.Console.WriteLine();
            System.Console.WriteLine("Popular models (Q4_0 or Q8_0 variants recommended):");
            System.Console.WriteLine("  - TheBloke/TinyLlama-1.1B-Chat-v1.0-GGUF");
            System.Console.WriteLine("  - TheBloke/Mistral-7B-Instruct-v0.2-GGUF");
            System.Console.WriteLine("  - microsoft/phi-2-GGUF");
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
