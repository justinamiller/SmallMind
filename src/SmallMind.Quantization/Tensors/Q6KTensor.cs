using System;
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

                // Decode each sub-block (16 sub-blocks of 16 values each)
                for (int subBlock = 0; subBlock < SUB_BLOCK_COUNT; subBlock++)
                {
                    float sc = d * scales[subBlock];
                    int subBlockDstOffset = dstOffset + subBlock * SUB_BLOCK_SIZE;

                    // Decode 16 values in this sub-block
                    for (int i = 0; i < SUB_BLOCK_SIZE; i++)
                    {
                        int valueIdx = subBlock * SUB_BLOCK_SIZE + i;
                        
                        // Reconstruct 6-bit value from low 4 bits (ql) and high 2 bits (qh)
                        // ql packs 2 values per byte: even values in low nibble, odd in high nibble
                        int qlIdx = valueIdx / 2;
                        byte qlByte = ql[qlIdx];
                        byte low4 = (valueIdx % 2 == 0) ? (byte)(qlByte & 0xF) : (byte)((qlByte >> 4) & 0xF);
                        
                        // Extract high 2 bits from qh (4 values per byte)
                        int qhIdx = valueIdx / 4;
                        int qhShift = (valueIdx % 4) * 2;
                        byte high2 = (byte)((qh[qhIdx] >> qhShift) & 0x3);
                        
                        // Combine to form 6-bit value (range 0-63)
                        int q = low4 | (high2 << 4);
                        
                        // Dequantize: center around 0 with -32 bias
                        dst[subBlockDstOffset + i] = sc * (q - 32);
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
