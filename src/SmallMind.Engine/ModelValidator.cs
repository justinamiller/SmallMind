using System;
using System.Collections.Generic;
using System.IO;
using SmallMind.Abstractions;

namespace SmallMind.Engine;

/// <summary>
/// Internal helper for validating model files and metadata before loading.
/// Provides early validation with actionable error messages.
/// </summary>
internal static class ModelValidator
{
    // Validation constants
    private const int MaxVocabSize = 1_000_000;
    private const int MaxBlockSize = 1_000_000;
    private const int MaxEmbedDim = 100_000;
    private const int MaxNumLayers = 1_000;
    private const int MaxNumHeads = 256;
    private const double MaxRopeTheta = 1_000_000;
    private const long MaxTensorElements = 2_500_000_000; // ~10GB at FP32

    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".smq",
        ".gguf"
    };

    private static readonly HashSet<string> UnsupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".onnx", ".pt", ".pth", ".safetensors", ".pb", ".h5", ".keras"
    };

    /// <summary>
    /// Validates that the model path is valid and the extension is supported.
    /// </summary>
    public static void ValidatePathAndExtension(ModelLoadRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Path))
        {
            throw new ArgumentException("Model path cannot be empty", nameof(request));
        }

        if (!File.Exists(request.Path))
        {
            throw new FileNotFoundException(
                $"Model file not found: {request.Path}{Environment.NewLine}" +
                $"Remediation: Ensure the file path is correct and the file exists.");
        }

        var ext = Path.GetExtension(request.Path).ToLowerInvariant();

        // Check for known unsupported formats first (better error message)
        if (UnsupportedExtensions.Contains(ext))
        {
            throw new UnsupportedModelException(
                request.Path,
                ext,
                $"Model format '{ext}' is not supported by SmallMind.{Environment.NewLine}" +
                $"Supported formats: .smq (recommended), .gguf (with AllowGgufImport=true){Environment.NewLine}" +
                $"Remediation: Convert your model to .smq or .gguf format, or use a pretrained SmallMind model.");
        }

        // Validate supported extensions
        if (!SupportedExtensions.Contains(ext))
        {
            throw new UnsupportedModelException(
                request.Path,
                ext,
                $"Unsupported model format '{ext}'.{Environment.NewLine}" +
                $"Supported formats: .smq (recommended), .gguf (with AllowGgufImport=true){Environment.NewLine}" +
                $"Remediation: Use a supported model format or convert your model using SmallMind.Console.");
        }

        // Special validation for GGUF files
        if (ext == ".gguf" && !request.AllowGgufImport)
        {
            throw new UnsupportedModelException(
                request.Path,
                ext,
                $"GGUF files require explicit opt-in via AllowGgufImport=true.{Environment.NewLine}" +
                $"GGUF import is experimental and converts to .smq format on first load.{Environment.NewLine}" +
                $"Remediation: Set AllowGgufImport=true in ModelLoadRequest, or pre-convert to .smq using SmallMind.Console.");
        }
    }

    /// <summary>
    /// Validates model metadata for sanity (vocab size, context length, dimensions, etc.)
    /// </summary>
    public static void ValidateMetadata(
        Dictionary<string, object>? metadata,
        string modelPath)
    {
        if (metadata == null || metadata.Count == 0)
        {
            // Metadata is optional for some model formats, but warn
            return;
        }

        // Validate vocab size
        if (metadata.TryGetValue("vocab_size", out var vocabObj))
        {
            var vocabSize = GetMetadataInt(metadata, "vocab_size", 0);
            if (vocabSize <= 0 || vocabSize > MaxVocabSize)
            {
                throw new UnsupportedModelException(
                    modelPath,
                    Path.GetExtension(modelPath),
                    $"Invalid vocab_size={vocabSize}. Expected range: 1-{MaxVocabSize}.{Environment.NewLine}" +
                    $"Remediation: The model file may be corrupted or in an unsupported format.");
            }
        }

        // Validate context length (block_size)
        if (metadata.TryGetValue("block_size", out var blockSizeObj))
        {
            var blockSize = GetMetadataInt(metadata, "block_size", 0);
            if (blockSize <= 0 || blockSize > MaxBlockSize)
            {
                throw new UnsupportedModelException(
                    modelPath,
                    Path.GetExtension(modelPath),
                    $"Invalid block_size={blockSize}. Expected range: 1-{MaxBlockSize}.{Environment.NewLine}" +
                    $"Remediation: The model file may be corrupted. Context length should be reasonable (e.g., 512-8192).");
            }
        }

        // Validate embedding dimension
        if (metadata.TryGetValue("embed_dim", out var embedDimObj))
        {
            var embedDim = GetMetadataInt(metadata, "embed_dim", 0);
            if (embedDim <= 0 || embedDim > MaxEmbedDim)
            {
                throw new UnsupportedModelException(
                    modelPath,
                    Path.GetExtension(modelPath),
                    $"Invalid embed_dim={embedDim}. Expected range: 1-{MaxEmbedDim}.{Environment.NewLine}" +
                    $"Remediation: The model file may be corrupted. Typical embedding dimensions are 256-4096.");
            }
        }

        // Validate number of layers
        if (metadata.TryGetValue("num_layers", out var numLayersObj))
        {
            var numLayers = GetMetadataInt(metadata, "num_layers", 0);
            if (numLayers <= 0 || numLayers > MaxNumLayers)
            {
                throw new UnsupportedModelException(
                    modelPath,
                    Path.GetExtension(modelPath),
                    $"Invalid num_layers={numLayers}. Expected range: 1-{MaxNumLayers}.{Environment.NewLine}" +
                    $"Remediation: The model file may be corrupted. Typical layer counts are 6-96.");
            }
        }

        // Validate number of attention heads
        if (metadata.TryGetValue("num_heads", out var numHeadsObj))
        {
            var numHeads = GetMetadataInt(metadata, "num_heads", 0);
            if (numHeads <= 0 || numHeads > MaxNumHeads)
            {
                throw new UnsupportedModelException(
                    modelPath,
                    Path.GetExtension(modelPath),
                    $"Invalid num_heads={numHeads}. Expected range: 1-{MaxNumHeads}.{Environment.NewLine}" +
                    $"Remediation: The model file may be corrupted. Typical head counts are 8-32.");
            }

            // Validate that embed_dim is divisible by num_heads
            if (metadata.TryGetValue("embed_dim", out var embedDimObj2))
            {
                var embedDim = GetMetadataInt(metadata, "embed_dim", 0);
                if (embedDim % numHeads != 0)
                {
                    throw new UnsupportedModelException(
                        modelPath,
                        Path.GetExtension(modelPath),
                        $"Invalid configuration: embed_dim={embedDim} must be divisible by num_heads={numHeads}.{Environment.NewLine}" +
                        $"Remediation: The model file may be corrupted or incorrectly configured.");
                }
            }
        }

        // Validate RoPE parameters if present
        if (metadata.TryGetValue("rope_theta", out var ropeObj))
        {
            var ropeTheta = Convert.ToDouble(ropeObj);
            if (ropeTheta <= 0 || ropeTheta > MaxRopeTheta)
            {
                throw new UnsupportedModelException(
                    modelPath,
                    Path.GetExtension(modelPath),
                    $"Invalid rope_theta={ropeTheta}. Expected range: 0-{MaxRopeTheta}.{Environment.NewLine}" +
                    $"Remediation: The model file may have invalid RoPE parameters.");
            }
        }
    }

    /// <summary>
    /// Validates tensor shapes for basic sanity checks.
    /// </summary>
    public static void ValidateTensorShapes(
        Dictionary<string, (int[] shape, int elementCount)>? tensorInfo,
        string modelPath)
    {
        if (tensorInfo == null || tensorInfo.Count == 0)
        {
            // No tensor info available, skip validation
            return;
        }

        foreach (var (tensorName, (shape, elementCount)) in tensorInfo)
        {
            // Check for negative dimensions
            foreach (var dim in shape)
            {
                if (dim <= 0)
                {
                    throw new UnsupportedModelException(
                        modelPath,
                        Path.GetExtension(modelPath),
                        $"Invalid tensor '{tensorName}': dimension {dim} <= 0.{Environment.NewLine}" +
                        $"Remediation: The model file may be corrupted.");
                }
            }

            // Check for excessively large tensors (> 10GB per tensor)
            if (elementCount > MaxTensorElements)
            {
                throw new UnsupportedModelException(
                    modelPath,
                    Path.GetExtension(modelPath),
                    $"Tensor '{tensorName}' is too large ({elementCount} elements, shape: [{string.Join(", ", shape)}]).{Environment.NewLine}" +
                    $"Maximum supported tensor size: {MaxTensorElements} elements.{Environment.NewLine}" +
                    $"Remediation: The model may be too large for this runtime, or the file may be corrupted.");
            }
        }
    }

    /// <summary>
    /// Estimates memory requirements based on model metadata.
    /// </summary>
    public static long EstimateMemoryRequirementBytes(
        Dictionary<string, object>? metadata,
        int quantizationBits = 32)
    {
        if (metadata == null)
        {
            return 0; // Can't estimate without metadata
        }

        long totalParams = 0;

        // Try to get parameter count directly
        if (metadata.TryGetValue("num_params", out var numParamsObj))
        {
            totalParams = Convert.ToInt64(numParamsObj);
        }
        else
        {
            // Estimate from model architecture
            var vocabSize = GetMetadataInt(metadata, "vocab_size", 50000);
            var embedDim = GetMetadataInt(metadata, "embed_dim", 768);
            var numLayers = GetMetadataInt(metadata, "num_layers", 12);

            // Rough estimation: embedding + layers + LM head
            // Embedding: vocab_size * embed_dim
            // Each layer: ~4 * embed_dim^2 (attention + FFN)
            // LM head: vocab_size * embed_dim
            totalParams = (long)vocabSize * embedDim + 
                         (long)numLayers * 4 * embedDim * embedDim + 
                         (long)vocabSize * embedDim;
        }

        // Calculate bytes based on quantization
        var bytesPerParam = quantizationBits / 8.0;
        var estimatedBytes = (long)(totalParams * bytesPerParam);

        // Add overhead for activations, KV cache, etc. (20% overhead)
        return (long)(estimatedBytes * 1.2);
    }

    private static int GetMetadataInt(Dictionary<string, object> metadata, string key, int defaultValue)
    {
        if (metadata.TryGetValue(key, out var value))
        {
            // Handle JsonElement from deserialized SMQ metadata
            if (value is System.Text.Json.JsonElement jsonElement)
            {
                if (jsonElement.ValueKind == System.Text.Json.JsonValueKind.Number)
                {
                    return jsonElement.GetInt32();
                }
                else if (jsonElement.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    if (int.TryParse(jsonElement.GetString(), out int parsed))
                    {
                        return parsed;
                    }
                }
            }
            
            return Convert.ToInt32(value);
        }
        return defaultValue;
    }
}
