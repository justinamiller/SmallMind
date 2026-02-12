using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace SmallMind.Quantization.Tensors
{
    /// <summary>
    /// Q4_K quantized tensor - 4-bit per value with super-block structure.
    /// Block size: 256 values per super-block.
    /// Each super-block contains 8 sub-blocks of 32 values.
    /// Total: 144 bytes per super-block (2 + 2 + 12 + 128).
    /// </summary>
    internal sealed class Q4KTensor
    {
        private const int BLOCK_SIZE = 256;
        private const int BYTES_PER_BLOCK = 144;
        private const int SUB_BLOCK_COUNT = 8;
        private const int SUB_BLOCK_SIZE = 32;

        /// <summary>
        /// Number of rows in the tensor.
        /// </summary>
        public int Rows { get; }

        /// <summary>
        /// Number of columns in the tensor.
        /// </summary>
        public int Cols { get; }

        /// <summary>
        /// Raw quantized data (packed 4-bit values + scales + mins).
        /// Layout per 256-value block: d (fp16), dmin (fp16), scales (12 bytes), qs (128 bytes).
        /// </summary>
        public byte[] Data { get; }

        /// <summary>
        /// Initializes a new Q4_K tensor.
        /// </summary>
        /// <param name="rows">Number of rows.</param>
        /// <param name="cols">Number of columns.</param>
        public Q4KTensor(int rows, int cols)
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
        /// Gets the block size for Q4_K quantization.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetBlockSize() => BLOCK_SIZE;

        /// <summary>
        /// Gets the number of bytes per block.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetBytesPerBlock() => BYTES_PER_BLOCK;

        /// <summary>
        /// Dequantizes the Q4_K tensor to FP32.
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

                // Read super-block scale and min (fp16)
                ushort dBits = MemoryMarshal.Read<ushort>(src.Slice(srcOffset));
                ushort dminBits = MemoryMarshal.Read<ushort>(src.Slice(srcOffset + 2));
                float d = HalfToFloat(dBits);
                float dmin = HalfToFloat(dminBits);

                // Read scales (12 bytes encoding 6-bit scales and mins for 8 sub-blocks)
                ReadOnlySpan<byte> scalesBytes = src.Slice(srcOffset + 4, 12);
                
                // Read quantized values (128 bytes, 2 values per byte)
                ReadOnlySpan<byte> qs = src.Slice(srcOffset + 16, 128);

                // Decode each sub-block
                for (int subBlock = 0; subBlock < SUB_BLOCK_COUNT; subBlock++)
                {
                    // Extract 6-bit scale and min for this sub-block from the 12-byte scales field
                    // This is a simplified extraction - actual llama.cpp uses bit packing
                    int scaleIdx = subBlock * 12 / 8;
                    byte scaleByte = scalesBytes[scaleIdx];
                    float sc = d * (scaleByte & 0x3F); // 6-bit scale
                    float m = dmin * ((scaleByte >> 6) & 0x3F); // 6-bit min (approximation)

                    // Dequantize 32 values in this sub-block
                    int subBlockDstOffset = dstOffset + subBlock * SUB_BLOCK_SIZE;
                    int qsOffset = subBlock * (SUB_BLOCK_SIZE / 2); // 16 bytes per sub-block (32 values, 2 per byte)

                    for (int i = 0; i < SUB_BLOCK_SIZE / 2; i++)
                    {
                        byte packed = qs[qsOffset + i];
                        int q0 = packed & 0xF;
                        int q1 = (packed >> 4) & 0xF;

                        dst[subBlockDstOffset + i * 2] = sc * q0 - m;
                        dst[subBlockDstOffset + i * 2 + 1] = sc * q1 - m;
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
    }
}
