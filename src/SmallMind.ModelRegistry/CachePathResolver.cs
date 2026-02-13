using System.Runtime.InteropServices;

namespace SmallMind.ModelRegistry
{
    /// <summary>
    /// Resolves cache directory paths for different operating systems.
    /// </summary>
    internal static class CachePathResolver
    {
        /// <summary>
        /// Gets the default model cache directory for the current platform.
        /// Respects SMALLMIND_MODEL_CACHE environment variable override.
        /// </summary>
        public static string GetDefaultCacheDirectory()
        {
            // Check for environment variable override first
            string? envOverride = Environment.GetEnvironmentVariable("SMALLMIND_MODEL_CACHE");
            if (!string.IsNullOrWhiteSpace(envOverride))
            {
                return envOverride;
            }

            // Platform-specific defaults
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Windows: %LOCALAPPDATA%/SmallMind/models
                string? localAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA");
                if (string.IsNullOrWhiteSpace(localAppData))
                {
                    localAppData = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                        "AppData",
                        "Local");
                }
                return Path.Combine(localAppData, "SmallMind", "models");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // macOS: ~/Library/Caches/SmallMind/models
                string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                return Path.Combine(home, "Library", "Caches", "SmallMind", "models");
            }
            else
            {
                // Linux/Unix: ~/.cache/smallmind/models
                string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string? cacheBase = Environment.GetEnvironmentVariable("XDG_CACHE_HOME");
                if (string.IsNullOrWhiteSpace(cacheBase))
                {
                    cacheBase = Path.Combine(home, ".cache");
                }
                return Path.Combine(cacheBase, "smallmind", "models");
            }
        }

        /// <summary>
        /// Gets the model directory path for a given model ID.
        /// </summary>
        public static string GetModelDirectory(string cacheRoot, string modelId)
        {
            return Path.Combine(cacheRoot, modelId);
        }

        /// <summary>
        /// Gets the manifest file path for a given model ID.
        /// </summary>
        public static string GetManifestPath(string cacheRoot, string modelId)
        {
            return Path.Combine(cacheRoot, modelId, "manifest.json");
        }
    }
}
