using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SmallMind.ModelRegistry
{
    /// <summary>
    /// Manages model registration, caching, and verification.
    /// </summary>
    internal sealed class ModelRegistry
    {
        private readonly string _cacheRoot;
        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="ModelRegistry"/> class.
        /// </summary>
        /// <param name="cacheRoot">Root cache directory. If null, uses platform default.</param>
        public ModelRegistry(string? cacheRoot = null)
        {
            _cacheRoot = cacheRoot ?? CachePathResolver.GetDefaultCacheDirectory();
        }

        /// <summary>
        /// Gets the cache root directory.
        /// </summary>
        public string CacheRoot => _cacheRoot;

        /// <summary>
        /// Adds a model to the registry from a local file or URL.
        /// </summary>
        /// <param name="source">Local file path or HTTP(S) URL.</param>
        /// <param name="modelId">Optional model ID. If null, generates from filename.</param>
        /// <param name="displayName">Optional display name. If null, uses model ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The registered model ID.</returns>
        public async Task<string> AddModelAsync(
            string source,
            string? modelId = null,
            string? displayName = null,
            CancellationToken cancellationToken = default)
        {
            // Determine if source is a URL or file path
            bool isUrl = source.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                         source.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

            // Generate model ID if not provided
            if (string.IsNullOrWhiteSpace(modelId))
            {
                modelId = GenerateModelId(source);
            }

            // Use model ID as display name if not provided
            if (string.IsNullOrWhiteSpace(displayName))
            {
                displayName = modelId;
            }

            // Ensure model directory exists
            string modelDir = CachePathResolver.GetModelDirectory(_cacheRoot, modelId);
            Directory.CreateDirectory(modelDir);

            // Determine target file name
            string fileName = Path.GetFileName(source);
            if (isUrl)
            {
                // Extract filename from URL or use default
                Uri uri = new Uri(source);
                fileName = Path.GetFileName(uri.LocalPath);
                if (string.IsNullOrWhiteSpace(fileName) || fileName == "/")
                {
                    fileName = "model.bin";
                }
            }

            string targetPath = Path.Combine(modelDir, fileName);

            // Copy or download the file
            long fileSize;
            if (isUrl)
            {
                fileSize = await DownloadFileAsync(source, targetPath, cancellationToken);
            }
            else
            {
                fileSize = CopyOrLinkFile(source, targetPath);
            }

            // Compute SHA256
            string sha256 = FileHashUtility.ComputeSha256(targetPath);

            // Detect format from extension
            string format = DetectFormat(fileName);

            // Create manifest
            var manifest = new ModelManifest
            {
                ModelId = modelId,
                DisplayName = displayName,
                Format = format,
                Files = new List<ModelFileEntry>
                {
                    new ModelFileEntry
                    {
                        Path = fileName,
                        SizeBytes = fileSize,
                        Sha256 = sha256
                    }
                },
                CreatedUtc = DateTime.UtcNow.ToString("o"),
                Source = source
            };

            // Save manifest
            SaveManifest(modelId, manifest);

            return modelId;
        }

        /// <summary>
        /// Lists all registered models.
        /// </summary>
        /// <returns>List of model manifests.</returns>
        public List<ModelManifest> ListModels()
        {
            var models = new List<ModelManifest>();

            if (!Directory.Exists(_cacheRoot))
            {
                return models;
            }

            foreach (string modelDir in Directory.GetDirectories(_cacheRoot))
            {
                string manifestPath = Path.Combine(modelDir, "manifest.json");
                if (File.Exists(manifestPath))
                {
                    try
                    {
                        var manifest = LoadManifest(Path.GetFileName(modelDir));
                        if (manifest != null)
                        {
                            models.Add(manifest);
                        }
                    }
                    catch
                    {
                        // Skip invalid manifests
                    }
                }
            }

            return models;
        }

        /// <summary>
        /// Verifies a model's integrity.
        /// </summary>
        /// <param name="modelId">Model ID to verify.</param>
        /// <returns>Verification result.</returns>
        public ModelVerificationResult VerifyModel(string modelId)
        {
            var result = new ModelVerificationResult { ModelId = modelId };

            string manifestPath = CachePathResolver.GetManifestPath(_cacheRoot, modelId);
            if (!File.Exists(manifestPath))
            {
                result.IsValid = false;
                result.Errors.Add("Manifest file not found");
                return result;
            }

            ModelManifest? manifest;
            try
            {
                manifest = LoadManifest(modelId);
                if (manifest == null)
                {
                    result.IsValid = false;
                    result.Errors.Add("Failed to load manifest");
                    return result;
                }
            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.Errors.Add($"Manifest parsing error: {ex.Message}");
                return result;
            }

            string modelDir = CachePathResolver.GetModelDirectory(_cacheRoot, modelId);
            foreach (var fileEntry in manifest.Files)
            {
                string filePath = Path.Combine(modelDir, fileEntry.Path);

                if (!File.Exists(filePath))
                {
                    result.IsValid = false;
                    result.Errors.Add($"File not found: {fileEntry.Path}");
                    continue;
                }

                var fileInfo = new FileInfo(filePath);
                if (fileInfo.Length != fileEntry.SizeBytes)
                {
                    result.IsValid = false;
                    result.Errors.Add($"File size mismatch: {fileEntry.Path} (expected {fileEntry.SizeBytes}, got {fileInfo.Length})");
                }

                if (!FileHashUtility.VerifySha256(filePath, fileEntry.Sha256))
                {
                    result.IsValid = false;
                    result.Errors.Add($"SHA256 mismatch: {fileEntry.Path}");
                }
            }

            result.IsValid = result.Errors.Count == 0;
            return result;
        }

        /// <summary>
        /// Gets the manifest for a specific model.
        /// </summary>
        /// <param name="modelId">Model ID.</param>
        /// <returns>Model manifest, or null if not found.</returns>
        public ModelManifest? GetManifest(string modelId)
        {
            return LoadManifest(modelId);
        }

        /// <summary>
        /// Gets the primary model file path for a model.
        /// </summary>
        /// <param name="modelId">Model ID.</param>
        /// <returns>Full path to the primary model file, or null if not found.</returns>
        public string? GetModelFilePath(string modelId)
        {
            var manifest = LoadManifest(modelId);
            if (manifest == null || manifest.Files.Count == 0)
            {
                return null;
            }

            string modelDir = CachePathResolver.GetModelDirectory(_cacheRoot, modelId);
            return Path.Combine(modelDir, manifest.Files[0].Path);
        }

        private ModelManifest? LoadManifest(string modelId)
        {
            string manifestPath = CachePathResolver.GetManifestPath(_cacheRoot, modelId);
            if (!File.Exists(manifestPath))
            {
                return null;
            }

            string json = File.ReadAllText(manifestPath);
            return JsonSerializer.Deserialize<ModelManifest>(json);
        }

        private void SaveManifest(string modelId, ModelManifest manifest)
        {
            string manifestPath = CachePathResolver.GetManifestPath(_cacheRoot, modelId);
            string json = JsonSerializer.Serialize(manifest, _jsonOptions);
            File.WriteAllText(manifestPath, json);
        }

        private string GenerateModelId(string source)
        {
            string fileName = Path.GetFileNameWithoutExtension(source);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                fileName = "model";
            }

            // Sanitize filename for use as ID
            int validCount = 0;
            for (int i = 0; i < fileName.Length; i++)
            {
                char c = fileName[i];
                if (char.IsLetterOrDigit(c) || c == '-' || c == '_')
                {
                    validCount++;
                }
            }
            
            var buffer = new char[validCount];
            int pos = 0;
            for (int i = 0; i < fileName.Length; i++)
            {
                char c = fileName[i];
                if (char.IsLetterOrDigit(c) || c == '-' || c == '_')
                {
                    buffer[pos++] = c;
                }
            }
            fileName = new string(buffer);

            // Ensure uniqueness
            string baseId = fileName;
            int counter = 1;
            string modelId = baseId;
            while (Directory.Exists(CachePathResolver.GetModelDirectory(_cacheRoot, modelId)))
            {
                modelId = $"{baseId}-{counter}";
                counter++;
            }

            return modelId;
        }

        private string DetectFormat(string fileName)
        {
            string ext = Path.GetExtension(fileName).ToLowerInvariant();
            return ext switch
            {
                ".gguf" => "gguf",
                ".smq" => "smq",
                ".bin" => "bin",
                _ => "unknown"
            };
        }

        private long CopyOrLinkFile(string sourcePath, string targetPath)
        {
            if (!File.Exists(sourcePath))
            {
                throw new FileNotFoundException($"Source file not found: {sourcePath}");
            }

            // If target already exists and is the same file, skip
            if (File.Exists(targetPath))
            {
                var sourceInfo = new FileInfo(sourcePath);
                var targetInfo = new FileInfo(targetPath);
                
                // Simple check: if sizes match, assume it's already there
                if (sourceInfo.Length == targetInfo.Length)
                {
                    return targetInfo.Length;
                }
            }

            // Try hardlink first (same volume), fall back to copy
            try
            {
                // For simplicity, just copy. Hardlink would require P/Invoke or newer .NET API.
                File.Copy(sourcePath, targetPath, overwrite: true);
            }
            catch
            {
                // If copy fails, throw
                throw;
            }

            return new FileInfo(targetPath).Length;
        }

        private async Task<long> DownloadFileAsync(string url, string targetPath, CancellationToken cancellationToken)
        {
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromMinutes(30); // Long timeout for large files

            using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            using var fileStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, useAsync: true);
            await response.Content.CopyToAsync(fileStream, cancellationToken);

            return fileStream.Length;
        }
    }

    /// <summary>
    /// Result of model verification.
    /// </summary>
    internal sealed class ModelVerificationResult
    {
        /// <summary>
        /// Gets or sets the model ID.
        /// </summary>
        public string ModelId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets whether the model is valid.
        /// </summary>
        public bool IsValid { get; set; } = true;

        /// <summary>
        /// Gets the list of errors encountered during verification.
        /// </summary>
        public List<string> Errors { get; } = new List<string>();
    }
}
