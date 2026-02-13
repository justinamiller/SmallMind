using System.Text;
using SmallMind.Quantization.IO.Gguf;

namespace SmallMind.Runtime.Gguf
{
    /// <summary>
    /// Represents the compatibility status of a GGUF model with the SmallMind runtime.
    /// Provides detailed diagnostics about supported and unsupported tensors.
    /// </summary>
    public sealed record GgufCompatibilityReport
    {
        /// <summary>
        /// Total number of tensors in the GGUF model.
        /// </summary>
        public int TotalTensors { get; init; }

        /// <summary>
        /// Number of tensors that are supported.
        /// </summary>
        public int SupportedTensors { get; init; }

        /// <summary>
        /// Number of tensors that are unsupported.
        /// </summary>
        public int UnsupportedTensors { get; init; }

        /// <summary>
        /// Whether all tensors are supported (fully compatible).
        /// </summary>
        public bool IsFullyCompatible => UnsupportedTensors == 0;

        /// <summary>
        /// List of unsupported tensor types (as strings) and their tensor names.
        /// </summary>
        public Dictionary<string, List<string>> UnsupportedTensorsByType { get; init; } = new();

        /// <summary>
        /// List of supported tensor types (as strings) and their counts.
        /// </summary>
        public Dictionary<string, int> SupportedTensorsByType { get; init; } = new();

        /// <summary>
        /// Model architecture (e.g., "llama", "gpt2", "phi").
        /// </summary>
        public string? Architecture { get; init; }

        /// <summary>
        /// GGUF format version.
        /// </summary>
        public uint FormatVersion { get; init; }

        /// <summary>
        /// Generates a human-readable summary of the compatibility report.
        /// </summary>
        public string GetSummary()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== GGUF Compatibility Report ===");
            sb.AppendLine($"Architecture: {Architecture ?? "Unknown"}");
            sb.AppendLine($"GGUF Version: {FormatVersion}");
            sb.AppendLine($"Total Tensors: {TotalTensors}");
            sb.AppendLine($"Supported: {SupportedTensors} ({100.0 * SupportedTensors / TotalTensors:F1}%)");
            sb.AppendLine($"Unsupported: {UnsupportedTensors}");
            sb.AppendLine();

            if (IsFullyCompatible)
            {
                sb.AppendLine("✅ Model is FULLY COMPATIBLE with SmallMind runtime.");
                sb.AppendLine();
                sb.AppendLine("Supported Tensor Types:");
                foreach (var (type, count) in SupportedTensorsByType.OrderByDescending(kv => kv.Value))
                {
                    sb.AppendLine($"  ✅ {type}: {count} tensor(s)");
                }
            }
            else
            {
                sb.AppendLine("⚠️ Model has UNSUPPORTED tensors. Loading will fail.");
                sb.AppendLine();
                sb.AppendLine("Unsupported Tensor Types:");
                foreach (var (type, tensorNames) in UnsupportedTensorsByType.OrderByDescending(kv => kv.Value.Count))
                {
                    sb.AppendLine($"  ❌ {type}: {tensorNames.Count} tensor(s)");
                    int displayCount = Math.Min(5, tensorNames.Count);
                    for (int i = 0; i < displayCount; i++)
                    {
                        sb.AppendLine($"     - {tensorNames[i]}");
                    }
                    if (tensorNames.Count > 5)
                    {
                        sb.AppendLine($"     ... and {tensorNames.Count - 5} more");
                    }
                }

                sb.AppendLine();
                sb.AppendLine("Supported Tensor Types:");
                foreach (var (type, count) in SupportedTensorsByType.OrderByDescending(kv => kv.Value))
                {
                    sb.AppendLine($"  ✅ {type}: {count} tensor(s)");
                }

                sb.AppendLine();
                sb.AppendLine("To fix:");
                sb.AppendLine("1. Re-quantize the model to supported formats (Q4_0, Q8_0, Q4_K, Q5_K, Q6_K, Q8_K)");
                sb.AppendLine("2. Use llama.cpp quantize tool:");
                sb.AppendLine("   ./quantize model.gguf model-q8_0.gguf Q8_0");
                sb.AppendLine("3. Or use SmallMind's native quantization when loading FP32 models");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Throws an exception if the model is not fully compatible.
        /// </summary>
        public void ThrowIfIncompatible()
        {
            if (!IsFullyCompatible)
            {
                throw new NotSupportedException(
                    $"GGUF model contains {UnsupportedTensors} unsupported tensor(s). " +
                    $"Run GetCompatibilityReport() for details. " +
                    $"Unsupported types: {string.Join(", ", UnsupportedTensorsByType.Keys)}");
            }
        }

        /// <summary>
        /// Creates a compatibility report from a GGUF model.
        /// </summary>
        internal static GgufCompatibilityReport FromModelInfo(
            GgufModelInfo modelInfo,
            Func<GgufTensorType, bool> isSupportedFunc)
        {
            var report = new GgufCompatibilityReport
            {
                TotalTensors = modelInfo.Tensors.Count,
                FormatVersion = modelInfo.Version,
                Architecture = modelInfo.Metadata.TryGetValue("general.architecture", out var arch)
                    ? arch?.ToString()
                    : null
            };

            var supportedByType = new Dictionary<string, int>();
            var unsupportedByType = new Dictionary<string, List<string>>();
            int supportedCount = 0;
            int unsupportedCount = 0;

            foreach (var tensor in modelInfo.Tensors)
            {
                string typeStr = tensor.Type.ToString();

                if (isSupportedFunc(tensor.Type))
                {
                    supportedCount++;
                    if (!supportedByType.ContainsKey(typeStr))
                        supportedByType[typeStr] = 0;
                    supportedByType[typeStr]++;
                }
                else
                {
                    unsupportedCount++;
                    if (!unsupportedByType.ContainsKey(typeStr))
                        unsupportedByType[typeStr] = new List<string>();
                    unsupportedByType[typeStr].Add(tensor.Name);
                }
            }

            return report with
            {
                SupportedTensors = supportedCount,
                UnsupportedTensors = unsupportedCount,
                SupportedTensorsByType = supportedByType,
                UnsupportedTensorsByType = unsupportedByType
            };
        }
    }
}
