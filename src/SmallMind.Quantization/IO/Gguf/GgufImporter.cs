using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SmallMind.Quantization.IO.Smq;
using SmallMind.Quantization.Tensors;

namespace SmallMind.Quantization.IO.Gguf
{
    /// <summary>
    /// Imports GGUF model files and converts them to SMQ format.
    /// </summary>
    public sealed class GgufImporter
    {
        private const int GgufBlockSize = 32; // GGUF uses block size 32
        private const int SmqBlockSize = 64;  // SMQ default block size

        /// <summary>
        /// Import a GGUF file and convert it to SMQ format.
        /// </summary>
        /// <param name="ggufPath">Path to input GGUF file.</param>
        /// <param name="smqPath">Path to output SMQ file.</param>
        public void ImportToSmq(string ggufPath, string smqPath)
        {
            if (!File.Exists(ggufPath))
                throw new FileNotFoundException($"GGUF file not found: {ggufPath}");

            // Read GGUF model info
            GgufModelInfo modelInfo;
            using (var stream = File.OpenRead(ggufPath))
            using (var reader = new GgufReader(stream))
            {
                modelInfo = reader.ReadModelInfo();
            }

            // Validate all tensors are supported
            var unsupportedTensors = ValidateTensorTypes(modelInfo.Tensors);
            if (unsupportedTensors.Count > 0)
            {
                var sb = new StringBuilder();
                sb.AppendLine("The following tensors have unsupported types:");
                foreach (var (name, type) in unsupportedTensors)
                {
                    sb.AppendLine($"  - {name}: {type}");
                }
                sb.AppendLine();
                sb.AppendLine("Supported types: Q8_0, Q4_0");
                throw new NotSupportedException(sb.ToString());
            }

            // Convert tensors
            var smqTensors = new Dictionary<string, object>();

            using (var stream = File.OpenRead(ggufPath))
            using (var reader = new GgufReader(stream))
            {
                // Re-read model info to position stream correctly
                modelInfo = reader.ReadModelInfo();

                foreach (var tensorInfo in modelInfo.Tensors)
                {
                    object tensor = ConvertTensor(reader, tensorInfo);
                    smqTensors[tensorInfo.Name] = tensor;
                }
            }

            // Extract metadata for SMQ
            var smqMetadata = ExtractMetadata(modelInfo.Metadata);

            // Write SMQ file
            using (var stream = File.Create(smqPath))
            using (var writer = new SmqWriter(stream))
            {
                writer.WriteModel(smqTensors, smqMetadata);
            }
        }

        /// <summary>
        /// Validate that all tensors are supported types.
        /// Returns list of unsupported (name, type) pairs.
        /// </summary>
        private List<(string name, GgufTensorType type)> ValidateTensorTypes(List<GgufTensorInfo> tensors)
        {
            var unsupported = new List<(string, GgufTensorType)>();

            foreach (var tensor in tensors)
            {
                if (!IsSupportedType(tensor.Type))
                {
                    unsupported.Add((tensor.Name, tensor.Type));
                }
            }

            return unsupported;
        }

        /// <summary>
        /// Check if a tensor type is supported for conversion.
        /// </summary>
        private bool IsSupportedType(GgufTensorType type)
        {
            return type == GgufTensorType.Q8_0 || type == GgufTensorType.Q4_0;
        }

        /// <summary>
        /// Convert a GGUF tensor to SMQ tensor.
        /// </summary>
        private object ConvertTensor(GgufReader reader, GgufTensorInfo tensorInfo)
        {
            // Read raw tensor data
            byte[] rawData = reader.ReadTensorData(tensorInfo.Offset, tensorInfo.Size);

            return tensorInfo.Type switch
            {
                GgufTensorType.Q8_0 => ConvertQ8_0Tensor(rawData, tensorInfo.Dimensions),
                GgufTensorType.Q4_0 => ConvertQ4_0Tensor(rawData, tensorInfo.Dimensions),
                _ => throw new NotSupportedException($"Unsupported tensor type: {tensorInfo.Type}")
            };
        }

        /// <summary>
        /// Convert GGUF Q8_0 tensor to SMQ Q8Tensor.
        /// GGUF Q8_0 format: block_size=32, each block has fp16 scale + 32 x int8 values.
        /// SMQ Q8Tensor: configurable block_size (default 64), each block has fp32 scale + N x int8 values.
        /// </summary>
        private Q8Tensor ConvertQ8_0Tensor(byte[] rawData, ulong[] dimensions)
        {
            // Calculate tensor shape
            if (dimensions.Length != 2)
                throw new NotSupportedException($"Only 2D tensors supported, got {dimensions.Length}D");

            int rows = (int)dimensions[0];
            int cols = (int)dimensions[1];
            int totalElements = rows * cols;

            // GGUF uses block size 32
            int ggufNumBlocks = (totalElements + GgufBlockSize - 1) / GgufBlockSize;

            // Parse GGUF Q8_0 blocks
            var ggufData = new sbyte[totalElements];
            var ggufScales = new float[ggufNumBlocks];

            using (var ms = new MemoryStream(rawData))
            using (var br = new BinaryReader(ms))
            {
                for (int blockIdx = 0; blockIdx < ggufNumBlocks; blockIdx++)
                {
                    // Read fp16 scale and convert to fp32
                    ushort scaleHalf = br.ReadUInt16();
                    float scale = HalfToFloat(scaleHalf);
                    ggufScales[blockIdx] = scale;

                    // Read 32 int8 values (or fewer for last block)
                    int blockStart = blockIdx * GgufBlockSize;
                    int blockEnd = Math.Min(blockStart + GgufBlockSize, totalElements);
                    for (int i = blockStart; i < blockEnd; i++)
                    {
                        ggufData[i] = br.ReadSByte();
                    }
                }
            }

            // Re-quantize: dequantize with GGUF blocks, then quantize with SMQ blocks
            // GGUF block size (32) differs from SMQ default (64), so we must re-quantize
            var floatData = new float[totalElements];
            for (int blockIdx = 0; blockIdx < ggufNumBlocks; blockIdx++)
            {
                int blockStart = blockIdx * GgufBlockSize;
                int blockEnd = Math.Min(blockStart + GgufBlockSize, totalElements);
                float scale = ggufScales[blockIdx];

                for (int i = blockStart; i < blockEnd; i++)
                {
                    floatData[i] = ggufData[i] * scale;
                }
            }

            return Q8Tensor.Quantize(floatData, rows, cols, SmqBlockSize);
        }

        /// <summary>
        /// Convert GGUF Q4_0 tensor to SMQ Q4Tensor.
        /// GGUF Q4_0 format: block_size=32, each block has fp16 scale + 16 bytes (32 x 4-bit values).
        /// SMQ Q4Tensor: configurable block_size (default 64), each block has fp32 scale + packed 4-bit values.
        /// </summary>
        private Q4Tensor ConvertQ4_0Tensor(byte[] rawData, ulong[] dimensions)
        {
            // Calculate tensor shape
            if (dimensions.Length != 2)
                throw new NotSupportedException($"Only 2D tensors supported, got {dimensions.Length}D");

            int rows = (int)dimensions[0];
            int cols = (int)dimensions[1];
            int totalElements = rows * cols;

            // GGUF uses block size 32
            int ggufNumBlocks = (totalElements + GgufBlockSize - 1) / GgufBlockSize;

            // Parse GGUF Q4_0 blocks
            var ggufData = new byte[(totalElements + 1) / 2];
            var ggufScales = new float[ggufNumBlocks];

            using (var ms = new MemoryStream(rawData))
            using (var br = new BinaryReader(ms))
            {
                for (int blockIdx = 0; blockIdx < ggufNumBlocks; blockIdx++)
                {
                    // Read fp16 scale and convert to fp32
                    ushort scaleHalf = br.ReadUInt16();
                    float scale = HalfToFloat(scaleHalf);
                    ggufScales[blockIdx] = scale;

                    // Read 16 bytes (32 x 4-bit values packed)
                    int blockStart = blockIdx * GgufBlockSize;
                    int blockEnd = Math.Min(blockStart + GgufBlockSize, totalElements);
                    
                    for (int i = blockStart; i < blockEnd; i += 2)
                    {
                        byte packedByte = br.ReadByte();
                        
                        // GGUF packs low nibble first, then high nibble
                        int byteIdx = i / 2;
                        ggufData[byteIdx] = packedByte;
                    }
                }
            }

            // Re-quantize: dequantize with GGUF blocks, then quantize with SMQ blocks
            // GGUF block size (32) differs from SMQ default (64), so we must re-quantize
            var floatData = new float[totalElements];
            
            for (int blockIdx = 0; blockIdx < ggufNumBlocks; blockIdx++)
            {
                int blockStart = blockIdx * GgufBlockSize;
                int blockEnd = Math.Min(blockStart + GgufBlockSize, totalElements);
                float scale = ggufScales[blockIdx];

                for (int i = blockStart; i < blockEnd; i++)
                {
                    int byteIdx = i / 2;
                    byte packedByte = ggufData[byteIdx];
                    
                    // Extract nibble
                    byte nibble = (i % 2 == 0) 
                        ? (byte)(packedByte & 0xF) 
                        : (byte)((packedByte >> 4) & 0xF);
                    
                    int quantized = Q4Tensor.DecodeNibble(nibble);
                    floatData[i] = quantized * scale;
                }
            }

            return Q4Tensor.Quantize(floatData, rows, cols, SmqBlockSize);
        }

        /// <summary>
        /// Convert IEEE 754 half-precision (fp16) to single-precision (fp32).
        /// Simple bit manipulation approach.
        /// </summary>
        private float HalfToFloat(ushort half)
        {
            // Extract sign, exponent, mantissa
            uint sign = (uint)(half >> 15) & 0x1u;
            uint exponent = (uint)(half >> 10) & 0x1Fu;
            uint mantissa = (uint)half & 0x3FFu;

            uint result;

            if (exponent == 0)
            {
                if (mantissa == 0)
                {
                    // Zero
                    result = sign << 31;
                }
                else
                {
                    // Denormalized number - convert to normalized fp32
                    // Normalize the mantissa
                    exponent = 1;
                    while ((mantissa & 0x400) == 0)
                    {
                        mantissa <<= 1;
                        exponent--;
                    }
                    mantissa &= 0x3FFu; // Remove leading 1
                    result = (sign << 31) | ((exponent + (127 - 15)) << 23) | (mantissa << 13);
                }
            }
            else if (exponent == 0x1F)
            {
                // Infinity or NaN
                result = (sign << 31) | 0x7F800000u | (mantissa << 13);
            }
            else
            {
                // Normalized number
                result = (sign << 31) | ((exponent + (127 - 15)) << 23) | (mantissa << 13);
            }

            // Convert uint bits to float
            byte[] bytes = BitConverter.GetBytes(result);
            return BitConverter.ToSingle(bytes, 0);
        }

        /// <summary>
        /// Extract relevant metadata from GGUF metadata for SMQ.
        /// </summary>
        private Dictionary<string, object> ExtractMetadata(Dictionary<string, object> ggufMetadata)
        {
            var smqMetadata = new Dictionary<string, object>();

            // Copy relevant keys (common GGUF metadata keys)
            var relevantKeys = new[]
            {
                "general.architecture",
                "general.name",
                "general.quantization_version",
                "general.file_type",
                "llama.context_length",
                "llama.embedding_length",
                "llama.block_count",
                "llama.feed_forward_length",
                "llama.attention.head_count",
                "llama.attention.head_count_kv",
                "llama.rope.dimension_count",
                "llama.vocab_size"
            };

            foreach (var key in relevantKeys)
            {
                if (ggufMetadata.TryGetValue(key, out var value))
                {
                    smqMetadata[key] = value;
                }
            }

            // Add conversion metadata
            smqMetadata["converted_from"] = "GGUF";
            smqMetadata["conversion_date"] = DateTime.UtcNow.ToString("O");

            return smqMetadata;
        }
    }
}
