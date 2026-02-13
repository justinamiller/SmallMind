using SmallMind.Quantization.IO.Gguf;

namespace SmallMind.Runtime.Gguf.TensorDecoders
{
    /// <summary>
    /// Base class for GGUF tensor decoders providing common utility methods.
    /// </summary>
    internal abstract class TensorDecoderBase : ITensorDecoder
    {
        /// <summary>
        /// GGUF standard block size for basic quantization types (Q4_0, Q5_0, Q8_0, etc.).
        /// </summary>
        protected const int GgufBlockSize = 32;

        /// <summary>
        /// K-quant super-block size (Q4_K, Q5_K, Q6_K, Q8_K).
        /// </summary>
        protected const int KQuantBlockSize = 256;

        /// <inheritdoc/>
        public abstract bool CanDecode(GgufTensorType type);

        /// <inheritdoc/>
        public abstract float[] Decode(GgufTensorInfo tensorInfo, byte[] rawData);

        /// <summary>
        /// Calculates the total number of elements from tensor dimensions.
        /// </summary>
        protected int CalculateTotalElements(ulong[] dimensions)
        {
            int totalElements = 1;
            foreach (var dim in dimensions)
            {
                totalElements *= (int)dim;
            }
            return totalElements;
        }

        /// <summary>
        /// Validates that the raw data size matches expected size.
        /// </summary>
        protected void ValidateDataSize(byte[] rawData, int expectedSize, string tensorTypeName)
        {
            if (rawData.Length != expectedSize)
            {
                throw new InvalidDataException(
                    $"{tensorTypeName} tensor data size mismatch: expected {expectedSize} bytes, got {rawData.Length} bytes");
            }
        }

        /// <summary>
        /// Converts IEEE 754 half-precision (fp16) to single-precision (fp32).
        /// </summary>
        /// <param name="half">16-bit half-precision value.</param>
        /// <returns>32-bit single-precision float.</returns>
        protected float HalfToFloat(ushort half)
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
        /// Decodes a 4-bit nibble to signed integer using two's complement.
        /// </summary>
        /// <param name="nibble">4-bit value (0-15).</param>
        /// <returns>Signed integer in range -8 to 7.</returns>
        protected int DecodeNibble(byte nibble)
        {
            // Two's complement for 4-bit signed integer
            // Values 0-7 map to 0-7
            // Values 8-15 map to -8 to -1
            return (nibble < 8) ? nibble : nibble - 16;
        }
    }
}
