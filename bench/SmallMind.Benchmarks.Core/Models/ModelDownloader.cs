using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SmallMind.Benchmarks.Core.Models
{
    /// <summary>
    /// Manages model downloading, caching, and verification.
    /// No external dependencies - pure .NET/BCL implementation.
    /// </summary>
    public sealed class ModelDownloader
    {
        private readonly string _cacheDirectory;

        public ModelDownloader(string? cacheDirectory = null)
        {
            _cacheDirectory = cacheDirectory 
                ?? System.Environment.GetEnvironmentVariable("SMALLMIND_BENCH_MODEL_CACHE")
                ?? Path.Combine(Path.GetTempPath(), "smallmind-bench-models");

            Directory.CreateDirectory(_cacheDirectory);
        }

        /// <summary>
        /// Load model manifest from JSON file.
        /// </summary>
        public static ModelManifest LoadManifest(string manifestPath)
        {
            if (!File.Exists(manifestPath))
                throw new FileNotFoundException($"Manifest not found: {manifestPath}", manifestPath);

            string json = File.ReadAllText(manifestPath);
            var manifest = JsonSerializer.Deserialize<ModelManifest>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            });

            if (manifest == null)
                throw new InvalidOperationException("Failed to deserialize manifest");

            return manifest;
        }

        /// <summary>
        /// Get cached model path if available and verified, otherwise null.
        /// </summary>
        public string? GetCachedModelPath(ModelManifestEntry entry)
        {
            string cachedPath = Path.Combine(_cacheDirectory, $"{entry.Name}.gguf");
            
            if (!File.Exists(cachedPath))
                return null;

            // Verify file size
            var fileInfo = new FileInfo(cachedPath);
            if (fileInfo.Length != entry.SizeBytes)
            {
                Console.WriteLine($"⚠️  Cached file size mismatch for {entry.Name}: expected {entry.SizeBytes}, got {fileInfo.Length}");
                return null;
            }

            // Verify checksum (can skip for performance if size matches and env var set)
            bool skipChecksum = System.Environment.GetEnvironmentVariable("SMALLMIND_BENCH_SKIP_CHECKSUM") == "1";
            if (!skipChecksum)
            {
                string actualSha256 = ComputeSha256(cachedPath);
                if (!string.Equals(actualSha256, entry.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"⚠️  Cached file SHA256 mismatch for {entry.Name}");
                    Console.WriteLine($"    Expected: {entry.Sha256}");
                    Console.WriteLine($"    Actual:   {actualSha256}");
                    return null;
                }
            }

            return cachedPath;
        }

        /// <summary>
        /// Download model if not cached, verify checksum.
        /// </summary>
        public async Task<string> DownloadModelAsync(
            ModelManifestEntry entry,
            IProgress<DownloadProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            // Check cache first
            string? cachedPath = GetCachedModelPath(entry);
            if (cachedPath != null)
            {
                Console.WriteLine($"✅ Using cached model: {cachedPath}");
                return cachedPath;
            }

            string downloadPath = Path.Combine(_cacheDirectory, $"{entry.Name}.gguf");
            string tempPath = downloadPath + ".tmp";

            try
            {
                Console.WriteLine($"📥 Downloading {entry.DisplayName} ({FormatBytes(entry.SizeBytes)})...");
                Console.WriteLine($"    URL: {entry.Url}");

                using var httpClient = new System.Net.Http.HttpClient();
                httpClient.Timeout = TimeSpan.FromHours(1); // Long timeout for large models

                using var response = await httpClient.GetAsync(entry.Url, System.Net.Http.HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                response.EnsureSuccessStatusCode();

                long? totalBytes = response.Content.Headers.ContentLength;

                await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                await using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, useAsync: true);

                var buffer = new byte[81920]; // 80 KB buffer
                long downloadedBytes = 0;
                int bytesRead;

                while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                    downloadedBytes += bytesRead;

                    if (totalBytes.HasValue)
                    {
                        double percentage = (double)downloadedBytes / totalBytes.Value * 100.0;
                        progress?.Report(new DownloadProgress(downloadedBytes, totalBytes.Value, percentage));

                        // Print progress every 10 MB
                        if (downloadedBytes % (10 * 1024 * 1024) < buffer.Length)
                        {
                            Console.WriteLine($"    Downloaded {FormatBytes(downloadedBytes)} / {FormatBytes(totalBytes.Value)} ({percentage:F1}%)");
                        }
                    }
                }

                Console.WriteLine($"✅ Download complete: {FormatBytes(downloadedBytes)}");

                // Verify file size
                if (downloadedBytes != entry.SizeBytes)
                {
                    throw new InvalidOperationException(
                        $"Downloaded file size mismatch: expected {entry.SizeBytes}, got {downloadedBytes}");
                }

                // Verify SHA256 checksum
                Console.WriteLine($"🔍 Verifying SHA256 checksum...");
                string actualSha256 = ComputeSha256(tempPath);
                
                if (!string.Equals(actualSha256, entry.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"SHA256 mismatch:\n  Expected: {entry.Sha256}\n  Actual:   {actualSha256}");
                }

                Console.WriteLine($"✅ Checksum verified");

                // Move to final location
                File.Move(tempPath, downloadPath, overwrite: true);

                return downloadPath;
            }
            catch
            {
                // Clean up temp file on failure
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
                throw;
            }
        }

        /// <summary>
        /// Compute SHA256 hash of a file.
        /// </summary>
        private static string ComputeSha256(string filePath)
        {
            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            
            byte[] hash = sha256.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>
        /// Format bytes for human-readable display.
        /// </summary>
        private static string FormatBytes(long bytes)
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
    }

    /// <summary>
    /// Download progress information.
    /// </summary>
    public readonly struct DownloadProgress
    {
        public long BytesDownloaded { get; }
        public long TotalBytes { get; }
        public double Percentage { get; }

        public DownloadProgress(long bytesDownloaded, long totalBytes, double percentage)
        {
            BytesDownloaded = bytesDownloaded;
            TotalBytes = totalBytes;
            Percentage = percentage;
        }
    }
}
