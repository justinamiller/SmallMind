using System;
using System.IO;
using SmallMind.Quantization.IO.Gguf;

namespace SmallMind.Runtime.Gguf.TensorDecoders
{
    /// <summary>
    /// Decoder for Q4_1 tensor type (4-bit with min/max).
    /// GGUF Q4_1 format: block_size=32, each block has fp16 scale + fp16 min + 16 bytes (32 x 4-bit unsigned values).
    /// </summary>
    internal sealed class Q4_1Decoder : TensorDecoderBase
    {
        public override bool CanDecode(GgufTensorType type)
        {
            return type == GgufTensorType.Q4_1;
        }

        public override float[] Decode(GgufTensorInfo tensorInfo, byte[] rawData)
        {
            int totalElements = CalculateTotalElements(tensorInfo.Dimensions);
            int numBlocks = (totalElements + GgufBlockSize - 1) / GgufBlockSize;

            var floatData = new float[totalElements];

            using (var ms = new MemoryStream(rawData))
            using (var br = new BinaryReader(ms))
            {
                for (int blockIdx = 0; blockIdx < numBlocks; blockIdx++)
                {
                    // Read fp16 scale and convert to fp32
                    ushort scaleHalf = br.ReadUInt16();
                    float scale = HalfToFloat(scaleHalf);

                    // Read fp16 min and convert to fp32
                    ushort minHalf = br.ReadUInt16();
                    float min = HalfToFloat(minHalf);

                    // Read 16 bytes (32 x 4-bit unsigned values packed)
                    int blockStart = blockIdx * GgufBlockSize;
                    int blockEnd = Math.Min(blockStart + GgufBlockSize, totalElements);

                    // Read packed nibbles for this block
                    byte[] blockData = new byte[16];
                    br.Read(blockData, 0, 16);

                    // Dequantize the block
                    for (int i = blockStart; i < blockEnd; i++)
                    {
                        int localIdx = i - blockStart;
                        int byteIdx = localIdx / 2;
                        byte packedByte = blockData[byteIdx];

                        // Extract unsigned nibble (0-15)
                        byte nibble = (localIdx % 2 == 0)
                            ? (byte)(packedByte & 0xF)
                            : (byte)((packedByte >> 4) & 0xF);

                        // Dequantize: value = scale * nibble + min
                        floatData[i] = scale * nibble + min;
                    }
                }
            }

            return floatData;
        }
    }

    /// <summary>
    /// Decoder for Q5_0 tensor type (5-bit symmetric quantization).
    /// GGUF Q5_0 format: block_size=32, each block has fp16 scale + 4 bytes high bits + 16 bytes low nibbles.
    /// </summary>
    internal sealed class Q5_0Decoder : TensorDecoderBase
    {
        public override bool CanDecode(GgufTensorType type)
        {
            return type == GgufTensorType.Q5_0;
        }

        public override float[] Decode(GgufTensorInfo tensorInfo, byte[] rawData)
        {
            int totalElements = CalculateTotalElements(tensorInfo.Dimensions);
            int numBlocks = (totalElements + GgufBlockSize - 1) / GgufBlockSize;

            var floatData = new float[totalElements];

            using (var ms = new MemoryStream(rawData))
            using (var br = new BinaryReader(ms))
            {
                for (int blockIdx = 0; blockIdx < numBlocks; blockIdx++)
                {
                    // Read fp16 scale and convert to fp32
                    ushort scaleHalf = br.ReadUInt16();
                    float scale = HalfToFloat(scaleHalf);

                    // Read 4 bytes of high bits (32 bits, 1 per value)
                    uint highBits = br.ReadUInt32();

                    // Read 16 bytes (32 x 4-bit low nibbles packed)
                    byte[] lowNibbles = new byte[16];
                    br.Read(lowNibbles, 0, 16);

                    int blockStart = blockIdx * GgufBlockSize;
                    int blockEnd = Math.Min(blockStart + GgufBlockSize, totalElements);

                    // Dequantize the block
                    for (int i = blockStart; i < blockEnd; i++)
                    {
                        int localIdx = i - blockStart;
                        int byteIdx = localIdx / 2;
                        byte packedByte = lowNibbles[byteIdx];

                        // Extract low 4 bits
                        byte low4 = (localIdx % 2 == 0)
                            ? (byte)(packedByte & 0xF)
                            : (byte)((packedByte >> 4) & 0xF);

                        // Extract high bit (5th bit)
                        uint highBit = (highBits >> localIdx) & 1;

                        // Combine to 5-bit value: [high bit][low 4 bits]
                        int value5bit = (int)((highBit << 4) | low4);

                        // Convert to signed using two's complement (range -16 to 15)
                        int signedValue = (value5bit < 16) ? value5bit : value5bit - 32;

                        // Dequantize
                        floatData[i] = signedValue * scale;
                    }
                }
            }

            return floatData;
        }
    }

    /// <summary>
    /// Decoder for Q5_1 tensor type (5-bit with min/max).
    /// GGUF Q5_1 format: block_size=32, each block has fp16 scale + fp16 min + 4 bytes high bits + 16 bytes low nibbles.
    /// </summary>
    internal sealed class Q5_1Decoder : TensorDecoderBase
    {
        public override bool CanDecode(GgufTensorType type)
        {
            return type == GgufTensorType.Q5_1;
        }

        public override float[] Decode(GgufTensorInfo tensorInfo, byte[] rawData)
        {
            int totalElements = CalculateTotalElements(tensorInfo.Dimensions);
            int numBlocks = (totalElements + GgufBlockSize - 1) / GgufBlockSize;

            var floatData = new float[totalElements];

            using (var ms = new MemoryStream(rawData))
            using (var br = new BinaryReader(ms))
            {
                for (int blockIdx = 0; blockIdx < numBlocks; blockIdx++)
                {
                    // Read fp16 scale and convert to fp32
                    ushort scaleHalf = br.ReadUInt16();
                    float scale = HalfToFloat(scaleHalf);

                    // Read fp16 min and convert to fp32
                    ushort minHalf = br.ReadUInt16();
                    float min = HalfToFloat(minHalf);

                    // Read 4 bytes of high bits (32 bits, 1 per value)
                    uint highBits = br.ReadUInt32();

                    // Read 16 bytes (32 x 4-bit low nibbles packed)
                    byte[] lowNibbles = new byte[16];
                    br.Read(lowNibbles, 0, 16);

                    int blockStart = blockIdx * GgufBlockSize;
                    int blockEnd = Math.Min(blockStart + GgufBlockSize, totalElements);

                    // Dequantize the block
                    for (int i = blockStart; i < blockEnd; i++)
                    {
                        int localIdx = i - blockStart;
                        int byteIdx = localIdx / 2;
                        byte packedByte = lowNibbles[byteIdx];

                        // Extract low 4 bits
                        byte low4 = (localIdx % 2 == 0)
                            ? (byte)(packedByte & 0xF)
                            : (byte)((packedByte >> 4) & 0xF);

                        // Extract high bit (5th bit)
                        uint highBit = (highBits >> localIdx) & 1;

                        // Combine to 5-bit unsigned value: [high bit][low 4 bits]
                        uint value5bit = (highBit << 4) | low4;

                        // Dequantize with scale and min
                        floatData[i] = scale * value5bit + min;
                    }
                }
            }

            return floatData;
        }
    }

    /// <summary>
    /// Decoder for Q4_K tensor type (K-quant 4-bit).
    /// </summary>
    internal sealed class Q4KDecoder : TensorDecoderBase
    {
        public override bool CanDecode(GgufTensorType type)
        {
            return type == GgufTensorType.Q4_K;
        }

        public override float[] Decode(GgufTensorInfo tensorInfo, byte[] rawData)
        {
            throw new NotImplementedException("Q4_K decoder migration in progress");
        }
    }

    /// <summary>
    /// Decoder for Q5_K tensor type (K-quant 5-bit).
    /// </summary>
    internal sealed class Q5KDecoder : TensorDecoderBase
    {
        public override bool CanDecode(GgufTensorType type)
        {
            return type == GgufTensorType.Q5_K;
        }

        public override float[] Decode(GgufTensorInfo tensorInfo, byte[] rawData)
        {
            throw new NotImplementedException("Q5_K decoder migration in progress");
        }
    }

    /// <summary>
    /// Decoder for Q6_K tensor type (K-quant 6-bit).
    /// </summary>
    internal sealed class Q6KDecoder : TensorDecoderBase
    {
        public override bool CanDecode(GgufTensorType type)
        {
            return type == GgufTensorType.Q6_K;
        }

        public override float[] Decode(GgufTensorInfo tensorInfo, byte[] rawData)
        {
            throw new NotImplementedException("Q6_K decoder migration in progress");
        }
    }

    /// <summary>
    /// Decoder for Q8_K tensor type (K-quant 8-bit).
    /// This is a HIGH PRIORITY decoder that needs to be implemented.
    /// </summary>
    internal sealed class Q8KDecoder : TensorDecoderBase
    {
        public override bool CanDecode(GgufTensorType type)
        {
            return type == GgufTensorType.Q8_K;
        }

        public override float[] Decode(GgufTensorInfo tensorInfo, byte[] rawData)
        {
            throw new NotImplementedException(
                "Q8_K decoder not yet implemented. This is a HIGH PRIORITY decoder - see GGUF_TENSOR_COMPATIBILITY_MATRIX.md");
        }
    }
}
