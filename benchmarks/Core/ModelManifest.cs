using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SmallMind.Benchmarks.Core;

/// <summary>
/// Manifest entry for a benchmark model.
/// </summary>
public sealed class ModelManifestEntry
{
    /// <summary>
    /// Model name/identifier.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Download URL for the model.
    /// </summary>
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// SHA256 checksum for verification.
    /// </summary>
    [JsonPropertyName("sha256")]
    public string Sha256 { get; set; } = string.Empty;

    /// <summary>
    /// File size in bytes.
    /// </summary>
    [JsonPropertyName("size")]
    public long Size { get; set; }

    /// <summary>
    /// Quantization type (e.g., "Q4_0", "Q8_0", "F16").
    /// </summary>
    [JsonPropertyName("quantType")]
    public string QuantType { get; set; } = string.Empty;

    /// <summary>
    /// Maximum context length supported.
    /// </summary>
    [JsonPropertyName("contextLength")]
    public int ContextLength { get; set; }

    /// <summary>
    /// Whether to run this model in CI (small, fast models only).
    /// </summary>
    [JsonPropertyName("ci")]
    public bool Ci { get; set; }

    /// <summary>
    /// Optional description.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Optional tags for categorization.
    /// </summary>
    [JsonPropertyName("tags")]
    public string[]? Tags { get; set; }
}

/// <summary>
/// Model manifest containing all benchmark models.
/// </summary>
public sealed class ModelManifest
{
    /// <summary>
    /// Manifest version for compatibility tracking.
    /// </summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0";

    /// <summary>
    /// All model entries.
    /// </summary>
    [JsonPropertyName("models")]
    public List<ModelManifestEntry> Models { get; set; } = new();
}
