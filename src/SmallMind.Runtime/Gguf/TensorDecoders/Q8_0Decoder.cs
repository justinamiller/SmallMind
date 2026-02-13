using System;
using System.IO;
using SmallMind.Quantization.IO.Gguf;

namespace SmallMind.Runtime.Gguf.TensorDecoders
{
    /// <summary>
    /// Decoder for Q8_0 tensor type (8-bit symmetric quantization).
    /// GGUF Q8_0 format: block_size=32, each block has fp16 scale + 32 x int8 values.
    /// </summary>
    internal sealed class Q8_0Decoder : TensorDecoderBase
    {
        public override bool CanDecode(GgufTensorType type)
        {
            return type == GgufTensorType.Q8_0;
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

                    // GGUF Q8_0 always stores exactly 32 int8 values per block
                    // even if the last block has fewer elements
                    int blockStart = blockIdx * GgufBlockSize;
                    int blockEnd = Math.Min(blockStart + GgufBlockSize, totalElements);
                    int elementsInBlock = blockEnd - blockStart;

                    // Read all 32 int8 values
                    for (int j = 0; j < GgufBlockSize; j++)
                    {
                        sbyte quantized = br.ReadSByte();
                        
                        // Only use the first 'elementsInBlock' values
                        if (j < elementsInBlock)
                        {
                            floatData[blockStart + j] = quantized * scale;
                        }
                    }
                }
            }

            return floatData;
        }
    }
}
