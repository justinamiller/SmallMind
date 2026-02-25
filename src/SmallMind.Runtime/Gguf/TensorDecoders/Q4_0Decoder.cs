using SmallMind.Quantization.IO.Gguf;

namespace SmallMind.Runtime.Gguf.TensorDecoders
{
    /// <summary>
    /// Decoder for Q4_0 tensor type (4-bit symmetric quantization).
    /// GGUF Q4_0 format: block_size=32, each block has fp16 scale + 16 bytes (32 x 4-bit values).
    /// Values are unsigned 4-bit offset by 8 (0→-8, 8→0, 15→7), using split layout:
    ///   byte j (j=0..15): low nibble → element j; high nibble → element j+16.
    /// Reference: ggml dequantize_row_q4_0 in ggml-quants.c.
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
                const int halfBlock = GgufBlockSize / 2; // 16

                for (int blockIdx = 0; blockIdx < numBlocks; blockIdx++)
                {
                    // Read fp16 scale and convert to fp32
                    ushort scaleHalf = br.ReadUInt16();
                    float scale = HalfToFloat(scaleHalf);

                    int blockStart = blockIdx * GgufBlockSize;

                    // GGUF Q4_0 split layout (ggml dequantize_row_q4_0):
                    //   byte j: low nibble  → element j,      dequant = (byte & 0xF) - 8
                    //           high nibble → element j + 16, dequant = (byte >> 4)  - 8
                    for (int j = 0; j < halfBlock; j++)
                    {
                        byte packedByte = br.ReadByte();

                        int e0 = blockStart + j;
                        int e1 = blockStart + j + halfBlock;

                        if (e0 < totalElements)
                            floatData[e0] = ((packedByte & 0xF) - 8) * scale;
                        if (e1 < totalElements)
                            floatData[e1] = ((packedByte >> 4) - 8) * scale;
                    }
                }
            }

            return floatData;
        }
    }
}
