using System;
using System.Collections.Generic;
using SmallMind.Quantization.Tensors;

namespace SmallMind.Quantization.IO.Smq
{
    /// <summary>
    /// SMQ (SmallMind Quantized) file format constants and utilities.
    /// Format version: SMQv0001
    /// Layout: Header | Metadata JSON | Tensor Directory | Tensor Data Blobs
    /// </summary>
    public static class SmqFormat
    {
        /// <summary>
        /// Magic header for SMQ files (8 bytes ASCII).
        /// </summary>
        public const string MagicHeader = "SMQv0001";

        /// <summary>
        /// Current format version.
        /// </summary>
        public const uint FormatVersion = 1;

        /// <summary>
        /// Size of the fixed header in bytes.
        /// </summary>
        public const int HeaderSize = 32; // Magic(8) + Version(4) + HeaderSize(4) + TensorCount(4) + MetadataJsonLength(4) + Reserved(8)

        /// <summary>
        /// Tensor metadata entry in the directory.
        /// </summary>
        public class TensorEntry
        {
            /// <summary>
            /// Tensor name (e.g., "model.layers.0.attn.wq").
            /// </summary>
            public string Name { get; set; } = "";

            /// <summary>
            /// Data type / quantization scheme.
            /// </summary>
            public QuantScheme DataType { get; set; }

            /// <summary>
            /// Number of dimensions (rank).
            /// </summary>
            public int Rank { get; set; }

            /// <summary>
            /// Dimension sizes.
            /// </summary>
            public int[] Dimensions { get; set; } = Array.Empty<int>();

            /// <summary>
            /// Block size for quantization (0 for non-quantized types).
            /// </summary>
            public uint BlockSize { get; set; }

            /// <summary>
            /// Offset of tensor data in file.
            /// </summary>
            public ulong DataOffset { get; set; }

            /// <summary>
            /// Length of tensor data in bytes.
            /// </summary>
            public ulong DataLength { get; set; }

            /// <summary>
            /// Offset of auxiliary data (e.g., scales) in file (0 if none).
            /// </summary>
            public ulong AuxOffset { get; set; }

            /// <summary>
            /// Length of auxiliary data in bytes (0 if none).
            /// </summary>
            public ulong AuxLength { get; set; }
        }

        /// <summary>
        /// Calculate total elements in a tensor.
        /// </summary>
        public static int GetTotalElements(int[] dimensions)
        {
            if (dimensions == null || dimensions.Length == 0)
                return 0;

            int total = 1;
            for (int i = 0; i < dimensions.Length; i++)
            {
                total *= dimensions[i];
            }
            return total;
        }

        /// <summary>
        /// Calculate expected data size for a tensor.
        /// </summary>
        public static ulong GetExpectedDataSize(QuantScheme dataType, int totalElements)
        {
            return dataType switch
            {
                QuantScheme.F32 => (ulong)(totalElements * sizeof(float)),
                QuantScheme.F16 => (ulong)(totalElements * sizeof(ushort)),
                QuantScheme.Q8_0 => (ulong)totalElements, // sbyte per element
                QuantScheme.Q4_0 => (ulong)((totalElements + 1) / 2), // 2 elements per byte
                _ => throw new NotSupportedException($"Unsupported data type: {dataType}")
            };
        }

        /// <summary>
        /// Calculate expected auxiliary data size for a tensor.
        /// </summary>
        public static ulong GetExpectedAuxSize(QuantScheme dataType, int totalElements, uint blockSize)
        {
            return dataType switch
            {
                QuantScheme.F32 or QuantScheme.F16 => 0, // No aux data
                QuantScheme.Q8_0 or QuantScheme.Q4_0 => (ulong)(((totalElements + (int)blockSize - 1) / (int)blockSize) * sizeof(float)), // float scale per block
                _ => 0
            };
        }
    }
}
