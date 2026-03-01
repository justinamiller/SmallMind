using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SmallMind.Quantization.Tensors
{
    /// <summary>
    /// Q6_K quantized tensor - 6-bit per value with super-block structure.
    /// Block size: 256 values per super-block.
    /// Each super-block contains 16 sub-blocks of 16 values.
    /// Total: 210 bytes per super-block (128 + 64 + 16 + 2).
    /// </summary>
    internal sealed class Q6KTensor
    {
        private const int BLOCK_SIZE = 256;
        private const int BYTES_PER_BLOCK = 210;
        private const int SUB_BLOCK_COUNT = 16;
        private const int SUB_BLOCK_SIZE = 16;

        /// <summary>
        /// Number of rows in the tensor.
        /// </summary>
        public int Rows { get; }

        /// <summary>
        /// Number of columns in the tensor.
        /// </summary>
        public int Cols { get; }

        /// <summary>
        /// Raw quantized data (packed 6-bit values + scales).
        /// Layout per 256-value block: ql (128 bytes - low 4 bits), qh (64 bytes - high 2 bits), 
        /// scales (16 bytes - int8 per sub-block), d (fp16 super-block scale).
        /// </summary>
        public byte[] Data { get; }

        /// <summary>
        /// Initializes a new Q6_K tensor.
        /// </summary>
        /// <param name="rows">Number of rows.</param>
        /// <param name="cols">Number of columns.</param>
        public Q6KTensor(int rows, int cols)
        {
            if (rows <= 0) throw new ArgumentOutOfRangeException(nameof(rows));
            if (cols <= 0) throw new ArgumentOutOfRangeException(nameof(cols));
            if (cols % BLOCK_SIZE != 0)
                throw new ArgumentException($"Columns must be divisible by block size ({BLOCK_SIZE})", nameof(cols));

            Rows = rows;
            Cols = cols;

            int numBlocks = (rows * cols) / BLOCK_SIZE;
            Data = new byte[numBlocks * BYTES_PER_BLOCK];
        }

        /// <summary>
        /// Gets the block size for Q6_K quantization.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetBlockSize() => BLOCK_SIZE;

        /// <summary>
        /// Gets the number of bytes per block.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetBytesPerBlock() => BYTES_PER_BLOCK;

        /// <summary>
        /// Dequantizes the Q6_K tensor to FP32.
        /// </summary>
        /// <param name="src">Source quantized data.</param>
        /// <param name="dst">Destination FP32 buffer.</param>
        public static void Dequantize(ReadOnlySpan<byte> src, Span<float> dst)
        {
            if (dst.Length % BLOCK_SIZE != 0)
                throw new ArgumentException($"Destination length must be divisible by {BLOCK_SIZE}");

            int numBlocks = dst.Length / BLOCK_SIZE;
            int srcBlockSize = BYTES_PER_BLOCK;

            for (int block = 0; block < numBlocks; block++)
            {
                int srcOffset = block * srcBlockSize;
                int dstOffset = block * BLOCK_SIZE;

                // Read ql (128 bytes - low 4 bits of 6-bit values)
                ReadOnlySpan<byte> ql = src.Slice(srcOffset, 128);

                // Read qh (64 bytes - high 2 bits of 6-bit values)
                ReadOnlySpan<byte> qh = src.Slice(srcOffset + 128, 64);

                // Read scales (16 bytes - int8 per sub-block)
                ReadOnlySpan<sbyte> scales = MemoryMarshal.Cast<byte, sbyte>(src.Slice(srcOffset + 192, 16));

                // Read super-block scale d (fp16)
                ushort dBits = MemoryMarshal.Read<ushort>(src.Slice(srcOffset + 208));
                float d = HalfToFloat(dBits);

                // Decode 256 values using the llama.cpp ggml Q6_K layout.
                // Two half-passes of 128 values each (h=0: positions 0..127, h=1: 128..255).
                // Within each half-pass, l=0..31 produces 4 output positions:
                //   pos h*128+l:    ql[h*64+l]&0xF,     qh[h*32+l] bits 0-1
                //   pos h*128+l+32: ql[h*64+l+32]&0xF,  qh[h*32+l] bits 2-3
                //   pos h*128+l+64: ql[h*64+l]>>4,       qh[h*32+l] bits 4-5
                //   pos h*128+l+96: ql[h*64+l+32]>>4,    qh[h*32+l] bits 6-7
                // Scale index = absolute position / 16
                for (int h = 0; h < 2; h++)
                {
                    int qlBase = h * 64;
                    int qhBase = h * 32;
                    int outBase = h * 128;

                    for (int l = 0; l < 32; l++)
                    {
                        byte ql0 = ql[qlBase + l];
                        byte ql1 = ql[qlBase + l + 32];
                        byte qhb = qh[qhBase + l];

                        int p0 = outBase + l;
                        int p1 = outBase + l + 32;
                        int p2 = outBase + l + 64;
                        int p3 = outBase + l + 96;

                        float sc0 = d * scales[p0 / 16];
                        float sc1 = d * scales[p1 / 16];
                        float sc2 = d * scales[p2 / 16];
                        float sc3 = d * scales[p3 / 16];

                        int q0 = (ql0 & 0xF) | (((qhb >> 0) & 3) << 4);
                        int q1 = (ql1 & 0xF) | (((qhb >> 2) & 3) << 4);
                        int q2 = ((ql0 >> 4) & 0xF) | (((qhb >> 4) & 3) << 4);
                        int q3 = ((ql1 >> 4) & 0xF) | (((qhb >> 6) & 3) << 4);

                        dst[dstOffset + p0] = sc0 * (q0 - 32);
                        dst[dstOffset + p1] = sc1 * (q1 - 32);
                        dst[dstOffset + p2] = sc2 * (q2 - 32);
                        dst[dstOffset + p3] = sc3 * (q3 - 32);
                    }
                }
            }
        }

        /// <summary>
        /// Converts FP16 (half precision) bits to FP32.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe float HalfToFloat(ushort halfBits)
        {
            // Simple FP16 to FP32 conversion
            uint sign = (uint)(halfBits & 0x8000) << 16;
            uint exponent = (uint)(halfBits & 0x7C00) >> 10;
            uint mantissa = (uint)(halfBits & 0x03FF);

            if (exponent == 0)
            {
                // Denormalized or zero
                if (mantissa == 0)
                {
                    uint result = sign;
                    return *(float*)&result;
                }
                // Denormalized - convert to normalized
                while ((mantissa & 0x0400) == 0)
                {
                    mantissa <<= 1;
                    exponent--;
                }
                exponent++;
                mantissa &= 0x03FF;
            }
            else if (exponent == 31)
            {
                // Infinity or NaN
                uint result = sign | 0x7F800000 | (mantissa << 13);
                return *(float*)&result;
            }

            // Normalized
            exponent = exponent + (127 - 15);
            mantissa = mantissa << 13;
            uint floatBits = sign | (exponent << 23) | mantissa;
            return *(float*)&floatBits;
        }

        /// <summary>
        /// Dequantize the entire tensor to FP32.
        /// </summary>
        public float[] Dequantize()
        {
            int totalSize = Rows * Cols;
            var result = new float[totalSize];
            Dequantize(Data, result);
            return result;
        }
    }
}
