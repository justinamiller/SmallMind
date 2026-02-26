using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

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

                // Extract 6-bit scales and mins for all 8 sub-blocks from the 12-byte packed field
                // Per llama.cpp Q4_K: the 12 bytes encode both scales and mins packed together
                // The encoding uses 6 bits per scale and 6 bits per min for 8 sub-blocks
                // Total: 8*(6+6) = 96 bits = 12 bytes
                Span<byte> scales = stackalloc byte[SUB_BLOCK_COUNT];
                Span<byte> mins = stackalloc byte[SUB_BLOCK_COUNT];

                // Unpack 8 6-bit scales and 8 6-bit mins from 12 bytes using GGML get_scale_min_k4 format.
                // For j < 4:  scale[j] = scalesBytes[j] & 0x3F,  min[j] = scalesBytes[j+4] & 0x3F
                // For j >= 4: scale[j] = (scalesBytes[j+4] & 0xF) | ((scalesBytes[j-4] >> 6) << 4)
                //              min[j]   = (scalesBytes[j+4] >> 4)  | ((scalesBytes[j]   >> 6) << 4)
                scales[0] = (byte)(scalesBytes[0] & 0x3F);
                scales[1] = (byte)(scalesBytes[1] & 0x3F);
                scales[2] = (byte)(scalesBytes[2] & 0x3F);
                scales[3] = (byte)(scalesBytes[3] & 0x3F);
                scales[4] = (byte)((scalesBytes[8]  & 0xF) | ((scalesBytes[0] >> 6) << 4));
                scales[5] = (byte)((scalesBytes[9]  & 0xF) | ((scalesBytes[1] >> 6) << 4));
                scales[6] = (byte)((scalesBytes[10] & 0xF) | ((scalesBytes[2] >> 6) << 4));
                scales[7] = (byte)((scalesBytes[11] & 0xF) | ((scalesBytes[3] >> 6) << 4));

                mins[0] = (byte)(scalesBytes[4] & 0x3F);
                mins[1] = (byte)(scalesBytes[5] & 0x3F);
                mins[2] = (byte)(scalesBytes[6] & 0x3F);
                mins[3] = (byte)(scalesBytes[7] & 0x3F);
                mins[4] = (byte)((scalesBytes[8]  >> 4) | ((scalesBytes[4] >> 6) << 4));
                mins[5] = (byte)((scalesBytes[9]  >> 4) | ((scalesBytes[5] >> 6) << 4));
                mins[6] = (byte)((scalesBytes[10] >> 4) | ((scalesBytes[6] >> 6) << 4));
                mins[7] = (byte)((scalesBytes[11] >> 4) | ((scalesBytes[7] >> 6) << 4));

                // Decode each sub-block.
                // GGML Q4_K qs layout: for each 64-value chunk (two sub-blocks), 32 consecutive bytes are used.
                //   low nibbles  of those bytes → first  sub-block (32 values)
                //   high nibbles of those bytes → second sub-block (32 values)
                // So sub-block sb uses qs[(sb/2)*32 .. (sb/2)*32+31]:
                //   if sb is even → low  nibbles   (value[l] = qs[(sb/2)*32 + l] & 0xF)
                //   if sb is odd  → high nibbles   (value[l] = qs[(sb/2)*32 + l] >> 4)
                for (int subBlock = 0; subBlock < SUB_BLOCK_COUNT; subBlock++)
                {
                    float sc = d * scales[subBlock];
                    float m = dmin * mins[subBlock];

                    int subBlockDstOffset = dstOffset + subBlock * SUB_BLOCK_SIZE;
                    int qsByteBase = (subBlock / 2) * SUB_BLOCK_SIZE; // 32 bytes per pair
                    bool useHighNibble = (subBlock % 2) != 0;

                    for (int l = 0; l < SUB_BLOCK_SIZE; l++)
                    {
                        byte b = qs[qsByteBase + l];
                        int q = useHighNibble ? ((b >> 4) & 0xF) : (b & 0xF);
                        dst[subBlockDstOffset + l] = sc * q - m;
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
