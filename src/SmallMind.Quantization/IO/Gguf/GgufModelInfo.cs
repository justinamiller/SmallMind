using System;
using System.Collections.Generic;

namespace SmallMind.Quantization.IO.Gguf
{
    /// <summary>
    /// Parsed GGUF model information including metadata and tensor manifests.
    /// </summary>
    public class GgufModelInfo
    {
        /// <summary>
        /// GGUF format version (2 or 3).
        /// </summary>
        public uint Version { get; set; }

        /// <summary>
        /// Model metadata as key-value pairs.
        /// Keys are extracted from GGUF KV array.
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();

        /// <summary>
        /// Tensor information entries.
        /// </summary>
        public List<GgufTensorInfo> Tensors { get; set; } = new();

        /// <summary>
        /// Offset where tensor data section starts.
        /// </summary>
        public ulong DataOffset { get; set; }

        /// <summary>
        /// Alignment for tensor data (usually 32 bytes for version 3, inferred for version 2).
        /// </summary>
        public uint Alignment { get; set; } = 32;
    }
}
