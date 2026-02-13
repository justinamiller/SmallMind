using SmallMind.Quantization.IO.Gguf;

namespace SmallMind.Runtime.Gguf.TensorDecoders
{
    /// <summary>
    /// Decoder for floating-point tensor types (F32, F16).
    /// </summary>
    internal sealed class FloatingPointDecoder : TensorDecoderBase
    {
        public override bool CanDecode(GgufTensorType type)
        {
            return type == GgufTensorType.F32 || type == GgufTensorType.F16;
        }

        public override float[] Decode(GgufTensorInfo tensorInfo, byte[] rawData)
        {
            int totalElements = CalculateTotalElements(tensorInfo.Dimensions);

            if (tensorInfo.Type == GgufTensorType.F32)
            {
                return DecodeF32(rawData, totalElements);
            }
            else if (tensorInfo.Type == GgufTensorType.F16)
            {
                return DecodeF16(rawData, totalElements);
            }
            else
            {
                throw new InvalidOperationException(
                    $"FloatingPointDecoder cannot decode type: {tensorInfo.Type}");
            }
        }

        /// <summary>
        /// Decodes F32 tensor data (direct float values).
        /// </summary>
        private float[] DecodeF32(byte[] rawData, int totalElements)
        {
            int expectedSize = totalElements * sizeof(float);
            ValidateDataSize(rawData, expectedSize, "F32");

            var floatData = new float[totalElements];
            Buffer.BlockCopy(rawData, 0, floatData, 0, rawData.Length);

            return floatData;
        }

        /// <summary>
        /// Decodes F16 tensor data (half-precision floats converted to F32).
        /// </summary>
        private float[] DecodeF16(byte[] rawData, int totalElements)
        {
            int expectedSize = totalElements * sizeof(ushort);
            ValidateDataSize(rawData, expectedSize, "F16");

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

            return floatData;
        }
    }
}
