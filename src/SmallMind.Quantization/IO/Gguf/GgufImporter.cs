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
    internal sealed class GgufImporter
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
                sb.AppendLine("Supported types: F32, F16, Q8_0, Q4_0");
                throw new UnsupportedQuantizationException(sb.ToString());
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
            return type == GgufTensorType.F32 
                || type == GgufTensorType.F16 
                || type == GgufTensorType.Q8_0 
                || type == GgufTensorType.Q4_0;
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
                GgufTensorType.F32 => ConvertF32Tensor(rawData, tensorInfo.Dimensions),
                GgufTensorType.F16 => ConvertF16Tensor(rawData, tensorInfo.Dimensions),
                GgufTensorType.Q8_0 => ConvertQ8_0Tensor(rawData, tensorInfo.Dimensions),
                GgufTensorType.Q4_0 => ConvertQ4_0Tensor(rawData, tensorInfo.Dimensions),
                _ => throw new UnsupportedQuantizationException($"Unsupported tensor type: {tensorInfo.Type}")
            };
        }

        /// <summary>
        /// Convert GGUF F32 tensor to Fp32Tensor.
        /// F32 format: direct 32-bit float values.
        /// </summary>
        private Fp32Tensor ConvertF32Tensor(byte[] rawData, ulong[] dimensions)
        {
            // Calculate total elements from dimensions
            int totalElements = 1;
            foreach (var dim in dimensions)
            {
                totalElements *= (int)dim;
            }

            // F32 is 4 bytes per element
            if (rawData.Length != totalElements * sizeof(float))
                throw new InvalidDataException(
                    $"F32 tensor size mismatch: expected {totalElements * sizeof(float)} bytes, got {rawData.Length} bytes");

            // Direct conversion using Buffer.BlockCopy
            var floatData = new float[totalElements];
            Buffer.BlockCopy(rawData, 0, floatData, 0, rawData.Length);
            
            return Fp32Tensor.FromUlongDimensions(floatData, dimensions);
        }

        /// <summary>
        /// Convert GGUF F16 tensor to Fp32Tensor.
        /// F16 format: 16-bit half-precision float values.
        /// </summary>
        private Fp32Tensor ConvertF16Tensor(byte[] rawData, ulong[] dimensions)
        {
            // Calculate total elements from dimensions
            int totalElements = 1;
            foreach (var dim in dimensions)
            {
                totalElements *= (int)dim;
            }

            // F16 is 2 bytes per element
            if (rawData.Length != totalElements * sizeof(ushort))
                throw new InvalidDataException(
                    $"F16 tensor size mismatch: expected {totalElements * sizeof(ushort)} bytes, got {rawData.Length} bytes");

            var floatData = new float[totalElements];
            
            using (var ms = new MemoryStream(rawData))
            using (var br = new BinaryReader(ms))
            {
                for (int i = 0; i < totalElements; i++)
                {
                    ushort halfBits = br.ReadUInt16();
                    floatData[i] = HalfToFloat(halfBits);
                }
            }
            
            return Fp32Tensor.FromUlongDimensions(floatData, dimensions);
        }

        /// <summary>
        /// Convert GGUF Q8_0 tensor to SMQ Q8Tensor.
        /// GGUF Q8_0 format: block_size=32, each block has fp16 scale + 32 x int8 values.
        /// SMQ Q8Tensor: configurable block_size (default 64), each block has fp32 scale + N x int8 values.
        /// </summary>
        private Q8Tensor ConvertQ8_0Tensor(byte[] rawData, ulong[] dimensions)
        {
            // Calculate total elements from dimensions (supports any number of dimensions)
            int totalElements = 1;
            foreach (var dim in dimensions)
            {
                totalElements *= (int)dim;
            }
            
            // For SMQ, we need to provide rows and cols
            // If 1D, treat as 1 x N; if 2D, use as is; if higher-D, flatten
            int rows, cols;
            if (dimensions.Length == 1)
            {
                rows = 1;
                cols = (int)dimensions[0];
            }
            else if (dimensions.Length == 2)
            {
                rows = (int)dimensions[0];
                cols = (int)dimensions[1];
            }
            else
            {
                // Flatten multi-dimensional tensors to 2D
                rows = (int)dimensions[0];
                cols = totalElements / rows;
            }

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
            // Calculate total elements from dimensions (supports any number of dimensions)
            int totalElements = 1;
            foreach (var dim in dimensions)
            {
                totalElements *= (int)dim;
            }
            
            // For SMQ, we need to provide rows and cols
            // If 1D, treat as 1 x N; if 2D, use as is; if higher-D, flatten
            int rows, cols;
            if (dimensions.Length == 1)
            {
                rows = 1;
                cols = (int)dimensions[0];
            }
            else if (dimensions.Length == 2)
            {
                rows = (int)dimensions[0];
                cols = (int)dimensions[1];
            }
            else
            {
                // Flatten multi-dimensional tensors to 2D
                rows = (int)dimensions[0];
                cols = totalElements / rows;
            }

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

            // Copy model architecture metadata
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
                "llama.rope.freq_base",
                "llama.attention.layer_norm_rms_epsilon",
                "llama.vocab_size"
            };

            foreach (var key in relevantKeys)
            {
                if (ggufMetadata.TryGetValue(key, out var value))
                {
                    smqMetadata[key] = value;
                }
            }

            // Preserve tokenizer metadata
            var tokenizerMetadata = GgufTokenizerExtractor.PreserveTokenizerMetadata(ggufMetadata);
            foreach (var kvp in tokenizerMetadata)
            {
                smqMetadata[kvp.Key] = kvp.Value;
            }

            // Add conversion metadata
            smqMetadata["converted_from"] = "GGUF";
            smqMetadata["conversion_date"] = DateTime.UtcNow.ToString("O");

            return smqMetadata;
        }
    }
}
