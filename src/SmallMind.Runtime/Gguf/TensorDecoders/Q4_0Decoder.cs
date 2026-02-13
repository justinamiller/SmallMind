using System;
using System.IO;
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
            var packedData = new byte[(totalElements + 1) / 2];

            using (var ms = new MemoryStream(rawData))
            using (var br = new BinaryReader(ms))
            {
                for (int blockIdx = 0; blockIdx < numBlocks; blockIdx++)
                {
                    // Read fp16 scale and convert to fp32
                    ushort scaleHalf = br.ReadUInt16();
                    float scale = HalfToFloat(scaleHalf);

                    // Read 16 bytes (32 x 4-bit values packed)
                    int blockStart = blockIdx * GgufBlockSize;
                    int blockEnd = Math.Min(blockStart + GgufBlockSize, totalElements);

                    for (int i = blockStart; i < blockEnd; i += 2)
                    {
                        byte packedByte = br.ReadByte();

                        // GGUF packs low nibble first, then high nibble
                        int byteIdx = i / 2;
                        packedData[byteIdx] = packedByte;
                    }

                    // Dequantize the block
                    for (int i = blockStart; i < blockEnd; i++)
                    {
                        int byteIdx = i / 2;
                        byte packedByte = packedData[byteIdx];

                        // Extract nibble (low nibble for even indices, high nibble for odd)
                        byte nibble = (i % 2 == 0) 
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
