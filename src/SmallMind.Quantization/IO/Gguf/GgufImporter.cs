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
                sb.AppendLine("Supported types: F32, F16, Q8_0, Q4_0, Q4_1, Q5_0, Q4_K, Q6_K");
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
                || type == GgufTensorType.Q4_0
                || type == GgufTensorType.Q4_1
                || type == GgufTensorType.Q5_0
                || type == GgufTensorType.Q4_K
                || type == GgufTensorType.Q6_K;
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
                GgufTensorType.Q4_1 => ConvertQ4_1Tensor(rawData, tensorInfo.Dimensions),
                GgufTensorType.Q5_0 => ConvertQ5_0Tensor(rawData, tensorInfo.Dimensions),
                GgufTensorType.Q4_K => ConvertQ4KTensor(rawData, tensorInfo.Dimensions),
                GgufTensorType.Q6_K => ConvertQ6KTensor(rawData, tensorInfo.Dimensions),
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
        /// Convert GGUF Q4_1 tensor to native Q4_1Tensor.
        /// GGUF Q4_1 format: block_size=32, each block has fp16 scale + fp16 min + 16 bytes (32 x 4-bit unsigned values).
        /// Returns native Q4_1Tensor with block_size=32 to preserve quantized format.
        /// </summary>
        private Q4_1Tensor ConvertQ4_1Tensor(byte[] rawData, ulong[] dimensions)
        {
            // Calculate total elements from dimensions
            int totalElements = 1;
            foreach (var dim in dimensions)
            {
                totalElements *= (int)dim;
            }

            // For tensor, we need rows and cols
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

            // GGUF uses block size 32 (matches Q4_1 native block size)
            int numBlocks = (totalElements + GgufBlockSize - 1) / GgufBlockSize;

            // Parse GGUF Q4_1 blocks
            var data = new byte[(totalElements + 1) / 2];
            var scales = new float[numBlocks];
            var mins = new float[numBlocks];

            using (var ms = new MemoryStream(rawData))
            using (var br = new BinaryReader(ms))
            {
                for (int blockIdx = 0; blockIdx < numBlocks; blockIdx++)
                {
                    // Read fp16 scale and convert to fp32
                    ushort scaleHalf = br.ReadUInt16();
                    scales[blockIdx] = HalfToFloat(scaleHalf);

                    // Read fp16 min and convert to fp32
                    ushort minHalf = br.ReadUInt16();
                    mins[blockIdx] = HalfToFloat(minHalf);

                    // Read 16 bytes (32 x 4-bit unsigned values packed)
                    int blockStart = blockIdx * GgufBlockSize;
                    int blockEnd = Math.Min(blockStart + GgufBlockSize, totalElements);

                    for (int i = blockStart; i < blockEnd; i += 2)
                    {
                        byte packedByte = br.ReadByte();

                        // GGUF packs low nibble first, then high nibble
                        int byteIdx = i / 2;
                        data[byteIdx] = packedByte;
                    }
                }
            }

            // Return native Q4_1Tensor (no re-quantization needed since block sizes match)
            return new Q4_1Tensor(rows, cols, data, scales, mins);
        }

        /// <summary>
        /// Convert GGUF Q5_0 tensor to native Q5_0Tensor.
        /// GGUF Q5_0 format: block_size=32, each block has fp16 scale + 4 bytes high bits + 16 bytes low nibbles.
        /// Returns native Q5_0Tensor with block_size=32 to preserve quantized format.
        /// </summary>
        private Q5_0Tensor ConvertQ5_0Tensor(byte[] rawData, ulong[] dimensions)
        {
            // Calculate total elements from dimensions
            int totalElements = 1;
            foreach (var dim in dimensions)
            {
                totalElements *= (int)dim;
            }

            // For tensor, we need rows and cols
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

            // GGUF uses block size 32 (matches Q5_0 native block size)
            int numBlocks = (totalElements + GgufBlockSize - 1) / GgufBlockSize;

            // Parse GGUF Q5_0 blocks
            var dataLow = new byte[(totalElements + 1) / 2];
            var dataHigh = new byte[numBlocks * 4];
            var scales = new float[numBlocks];

            using (var ms = new MemoryStream(rawData))
            using (var br = new BinaryReader(ms))
            {
                for (int blockIdx = 0; blockIdx < numBlocks; blockIdx++)
                {
                    // Read fp16 scale and convert to fp32
                    ushort scaleHalf = br.ReadUInt16();
                    scales[blockIdx] = HalfToFloat(scaleHalf);

                    // Read 4 bytes of high bits (32 bits, 1 per value)
                    int highByteStart = blockIdx * 4;
                    dataHigh[highByteStart + 0] = br.ReadByte();
                    dataHigh[highByteStart + 1] = br.ReadByte();
                    dataHigh[highByteStart + 2] = br.ReadByte();
                    dataHigh[highByteStart + 3] = br.ReadByte();

                    // Read 16 bytes (32 x 4-bit low nibbles packed)
                    int blockStart = blockIdx * GgufBlockSize;
                    int blockEnd = Math.Min(blockStart + GgufBlockSize, totalElements);

                    for (int i = blockStart; i < blockEnd; i += 2)
                    {
                        byte packedByte = br.ReadByte();

                        // GGUF packs low nibble first, then high nibble
                        int byteIdx = i / 2;
                        dataLow[byteIdx] = packedByte;
                    }
                }
            }

            // Return native Q5_0Tensor (no re-quantization needed since block sizes match)
            return new Q5_0Tensor(rows, cols, dataLow, dataHigh, scales);
        }

        /// <summary>
        /// Convert GGUF Q4_K tensor to native Q4KTensor.
        /// GGUF Q4_K format: block_size=256, each super-block has:
        /// - d (fp16): super-block scale
        /// - dmin (fp16): super-block min
        /// - scales (12 bytes): 8 6-bit scales packed
        /// - qs (128 bytes): 256 4-bit values (2 per byte)
        /// Total: 144 bytes per block.
        /// </summary>
        private Q4KTensor ConvertQ4KTensor(byte[] rawData, ulong[] dimensions)
        {
            // Calculate total elements from dimensions
            int totalElements = 1;
            foreach (var dim in dimensions)
            {
                totalElements *= (int)dim;
            }

            // For tensor, we need rows and cols
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

            // Q4_K uses block size 256
            const int Q4K_BLOCK_SIZE = 256;
            const int Q4K_BYTES_PER_BLOCK = 144;

            if (totalElements % Q4K_BLOCK_SIZE != 0)
                throw new InvalidDataException(
                    $"Q4_K tensor size {totalElements} is not divisible by block size {Q4K_BLOCK_SIZE}");

            int numBlocks = totalElements / Q4K_BLOCK_SIZE;

            // Validate raw data size
            int expectedSize = numBlocks * Q4K_BYTES_PER_BLOCK;
            if (rawData.Length != expectedSize)
                throw new InvalidDataException(
                    $"Q4_K tensor data size mismatch: expected {expectedSize} bytes, got {rawData.Length} bytes");

            // Create Q4KTensor and copy the raw data directly (GGUF format matches our internal format)
            var tensor = new Q4KTensor(rows, cols);
            Buffer.BlockCopy(rawData, 0, tensor.Data, 0, rawData.Length);

            return tensor;
        }

        /// <summary>
        /// Convert GGUF Q6_K tensor to native Q6KTensor.
        /// GGUF Q6_K format: block_size=256, each super-block has:
        /// - ql (128 bytes): low 4 bits of 256 6-bit values
        /// - qh (64 bytes): high 2 bits of 256 6-bit values (4 values per byte)
        /// - scales (16 bytes): 16 int8 scales (one per sub-block)
        /// - d (fp16): super-block scale
        /// Total: 210 bytes per block.
        /// </summary>
        private Q6KTensor ConvertQ6KTensor(byte[] rawData, ulong[] dimensions)
        {
            // Calculate total elements from dimensions
            int totalElements = 1;
            foreach (var dim in dimensions)
            {
                totalElements *= (int)dim;
            }

            // For tensor, we need rows and cols
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

            // Q6_K uses block size 256
            const int Q6K_BLOCK_SIZE = 256;
            const int Q6K_BYTES_PER_BLOCK = 210;

            if (totalElements % Q6K_BLOCK_SIZE != 0)
                throw new InvalidDataException(
                    $"Q6_K tensor size {totalElements} is not divisible by block size {Q6K_BLOCK_SIZE}");

            int numBlocks = totalElements / Q6K_BLOCK_SIZE;

            // Validate raw data size
            int expectedSize = numBlocks * Q6K_BYTES_PER_BLOCK;
            if (rawData.Length != expectedSize)
                throw new InvalidDataException(
                    $"Q6_K tensor data size mismatch: expected {expectedSize} bytes, got {rawData.Length} bytes");

            // Create Q6KTensor and copy the raw data directly (GGUF format matches our internal format)
            var tensor = new Q6KTensor(rows, cols);
            Buffer.BlockCopy(rawData, 0, tensor.Data, 0, rawData.Length);

            return tensor;
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
