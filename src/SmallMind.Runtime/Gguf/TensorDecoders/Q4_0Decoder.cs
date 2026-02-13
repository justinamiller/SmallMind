using SmallMind.Quantization.IO.Gguf;

namespace SmallMind.Runtime.Gguf.TensorDecoders
{
    /// <summary>
    /// Decoder for Q4_0 tensor type (4-bit symmetric quantization).
    /// GGUF Q4_0 format: block_size=32, each block has fp16 scale + 16 bytes (32 x 4-bit values).
    /// </summary>
    internal sealed class Q4_0Decoder : TensorDecoderBase
    {
        public override bool CanDecode(GgufTensorType type)
        {
            return type == GgufTensorType.Q4_0;
        }

        public override float[] Decode(GgufTensorInfo tensorInfo, byte[] rawData)
        {
            int totalElements = CalculateTotalElements(tensorInfo.Dimensions);
            int numBlocks = (totalElements + GgufBlockSize - 1) / GgufBlockSize;

            var floatData = new float[totalElements];

            using (var ms = new MemoryStream(rawData))
            using (var br = new BinaryReader(ms))
            {
                // Reusable buffer for packed nibbles (16 bytes per block = 32 nibbles)
                byte[] blockBuffer = new byte[16];

                for (int blockIdx = 0; blockIdx < numBlocks; blockIdx++)
                {
                    // Read fp16 scale and convert to fp32
                    ushort scaleHalf = br.ReadUInt16();
                    float scale = HalfToFloat(scaleHalf);

                    // Read 16 bytes (32 x 4-bit values packed)
                    // GGUF Q4_0 always stores 16 bytes per block
                    br.Read(blockBuffer, 0, 16);

                    int blockStart = blockIdx * GgufBlockSize;
                    int blockEnd = Math.Min(blockStart + GgufBlockSize, totalElements);

                    // Decode nibbles directly into floatData
                    for (int i = blockStart; i < blockEnd; i++)
                    {
                        int localIdx = i - blockStart;
                        int byteIdx = localIdx / 2;
                        byte packedByte = blockBuffer[byteIdx];

                        // Extract nibble (low nibble for even indices, high nibble for odd)
                        byte nibble = (localIdx % 2 == 0)
                            ? (byte)(packedByte & 0xF)
                            : (byte)((packedByte >> 4) & 0xF);

                        int quantized = DecodeNibble(nibble);
                        floatData[i] = quantized * scale;
                    }
                }
            }

            return floatData;
        }
    }
}
