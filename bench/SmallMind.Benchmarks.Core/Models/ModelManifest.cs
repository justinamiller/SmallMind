using System;

namespace SmallMind.Benchmarks.Core.Models
{
    /// <summary>
    /// Model manifest entry describing a GGUF model for benchmarking.
    /// </summary>
    public sealed class ModelManifestEntry
    {
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string Sha256 { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
        public string QuantType { get; set; } = string.Empty;
        public int ContextLength { get; set; }
        public bool CI { get; set; }
        public string Description { get; set; } = string.Empty;
    }

    /// <summary>
    /// Model manifest file schema.
    /// </summary>
    public sealed class ModelManifest
    {
        public string Version { get; set; } = "1.0";
        public ModelManifestEntry[] Models { get; set; } = Array.Empty<ModelManifestEntry>();
    }
}
