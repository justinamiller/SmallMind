using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace SmallMind.Benchmarks.Core;

/// <summary>
/// Downloads and verifies GGUF models with SHA256 checksum validation.
/// Implements caching to avoid re-downloading.
/// </summary>
public sealed class ModelDownloader : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _cacheDirectory;

    public ModelDownloader(string? cacheDirectory = null)
    {
        _httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
        _cacheDirectory = cacheDirectory 
            ?? Environment.GetEnvironmentVariable("SMALLMIND_BENCH_MODEL_CACHE") 
            ?? Path.Combine(Path.GetTempPath(), "SmallMind", "BenchCache");
        
        Directory.CreateDirectory(_cacheDirectory);
    }

    /// <summary>
    /// Downloads or retrieves a model from cache.
    /// </summary>
    /// <param name="entry">Model manifest entry</param>
    /// <param name="progress">Optional progress callback (bytesDownloaded, totalBytes)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Path to the cached model file</returns>
    public async Task<string> DownloadModelAsync(
        ModelManifestEntry entry,
        IProgress<(long downloaded, long total)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (entry == null)
            throw new ArgumentNullException(nameof(entry));

        var fileName = GetSafeFileName(entry.Name);
        var filePath = Path.Combine(_cacheDirectory, fileName);

        // Check if already cached and valid
        if (File.Exists(filePath))
        {
            var cachedChecksum = await ComputeSha256Async(filePath, cancellationToken);
            if (string.Equals(cachedChecksum, entry.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"Model '{entry.Name}' found in cache: {filePath}");
                return filePath;
            }

            Console.WriteLine($"Cached model '{entry.Name}' checksum mismatch. Re-downloading...");
            File.Delete(filePath);
        }

        // Download model
        Console.WriteLine($"Downloading model '{entry.Name}' from {entry.Url}...");
        Console.WriteLine($"Expected size: {entry.Size / (1024.0 * 1024.0):F2} MB");

        using var response = await _httpClient.GetAsync(entry.Url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? entry.Size;
        var tempPath = filePath + ".tmp";

        try
        {
            using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, useAsync: true);

            var buffer = new byte[8192];
            long downloadedBytes = 0;
            int bytesRead;

            while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                downloadedBytes += bytesRead;
                progress?.Report((downloadedBytes, totalBytes));
            }

            Console.WriteLine($"Download complete: {downloadedBytes / (1024.0 * 1024.0):F2} MB");
        }
        catch
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
            throw;
        }

        // Verify checksum
        Console.WriteLine("Verifying checksum...");
        var downloadedChecksum = await ComputeSha256Async(tempPath, cancellationToken);

        if (!string.Equals(downloadedChecksum, entry.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(tempPath);
            throw new InvalidDataException(
                $"Checksum verification failed for '{entry.Name}'.\n" +
                $"Expected: {entry.Sha256}\n" +
                $"Got:      {downloadedChecksum}");
        }

        Console.WriteLine("Checksum verified successfully.");

        // Move to final location
        File.Move(tempPath, filePath, overwrite: true);
        return filePath;
    }

    /// <summary>
    /// Computes SHA256 hash of a file.
    /// </summary>
    public static async Task<string> ComputeSha256Async(string filePath, CancellationToken cancellationToken = default)
    {
        using var sha256 = SHA256.Create();
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 8192, useAsync: true);

        var hashBytes = await sha256.ComputeHashAsync(stream, cancellationToken);
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
    }

    private static string GetSafeFileName(string name)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var safeName = name;
        foreach (var c in invalidChars)
        {
            safeName = safeName.Replace(c, '_');
        }
        return safeName + ".gguf";
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}
