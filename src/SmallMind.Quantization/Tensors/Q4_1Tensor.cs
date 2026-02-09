using System;
using System.Runtime.CompilerServices;

namespace SmallMind.Quantization.Tensors
{
    /// <summary>
    /// Q4_1 quantized tensor: 4-bit asymmetric quantization with per-block scale and minimum.
    /// Block size is 32 (GGUF standard). Each block has:
    /// - scale: fp16 (2 bytes)
    /// - min: fp16 (2 bytes)
    /// - quantized values: 4-bit unsigned in [0, 15], packed two per byte (16 bytes for 32 values)
    /// Total: 20 bytes per block
    /// 
    /// Dequantization formula: value = q * scale + min
    /// This allows better representation of asymmetric distributions compared to Q4_0.
    /// </summary>
    internal sealed class Q4_1Tensor
    {
        private const int Q4_1_BLOCK_SIZE = 32; // GGUF standard block size for Q4_1

        /// <summary>
        /// Number of rows in the tensor.
        /// </summary>
        public int Rows { get; }

        /// <summary>
        /// Number of columns in the tensor.
        /// </summary>
        public int Cols { get; }

        /// <summary>
        /// Block size for quantization (fixed at 32 for Q4_1).
        /// </summary>
        public int BlockSize => Q4_1_BLOCK_SIZE;

        /// <summary>
        /// Packed quantized data (two 4-bit unsigned values per byte).
        /// Length = (Rows * Cols + 1) / 2.
        /// </summary>
        public byte[] Data { get; }

        /// <summary>
        /// Scale factors for each block (fp32 for simplicity, could be fp16).
        /// Length = ceil((Rows * Cols) / BlockSize).
        /// </summary>
        public float[] Scales { get; }

        /// <summary>
        /// Minimum values for each block (fp32 for simplicity, could be fp16).
        /// Length = ceil((Rows * Cols) / BlockSize).
        /// </summary>
        public float[] Mins { get; }

        /// <summary>
        /// Creates a Q4_1 tensor from quantized data.
        /// </summary>
        public Q4_1Tensor(int rows, int cols, byte[] data, float[] scales, float[] mins)
        {
            if (rows <= 0) throw new ArgumentException("Rows must be positive", nameof(rows));
            if (cols <= 0) throw new ArgumentException("Cols must be positive", nameof(cols));
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (scales == null) throw new ArgumentNullException(nameof(scales));
            if (mins == null) throw new ArgumentNullException(nameof(mins));

            int totalSize = rows * cols;
            int expectedDataLen = (totalSize + 1) / 2; // Two 4-bit values per byte
            if (data.Length != expectedDataLen)
                throw new ArgumentException($"Data length {data.Length} != expected {expectedDataLen}");

            int expectedBlocks = (totalSize + Q4_1_BLOCK_SIZE - 1) / Q4_1_BLOCK_SIZE;
            if (scales.Length != expectedBlocks)
                throw new ArgumentException($"Scales length {scales.Length} != expected blocks {expectedBlocks}");
            if (mins.Length != expectedBlocks)
                throw new ArgumentException($"Mins length {mins.Length} != expected blocks {expectedBlocks}");

            Rows = rows;
            Cols = cols;
            Data = data;
            Scales = scales;
            Mins = mins;
        }

        /// <summary>
        /// Quantize a float array to Q4_1 format.
        /// </summary>
        /// <param name="source">Source float array (length = rows * cols).</param>
        /// <param name="rows">Number of rows.</param>
        /// <param name="cols">Number of columns.</param>
        /// <returns>Quantized Q4_1 tensor.</returns>
        public static Q4_1Tensor Quantize(float[] source, int rows, int cols)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (rows <= 0) throw new ArgumentException("Rows must be positive", nameof(rows));
            if (cols <= 0) throw new ArgumentException("Cols must be positive", nameof(cols));

            int totalSize = rows * cols;
            if (source.Length != totalSize)
                throw new ArgumentException($"Source length {source.Length} != rows*cols {totalSize}");

            int numBlocks = (totalSize + Q4_1_BLOCK_SIZE - 1) / Q4_1_BLOCK_SIZE;
            int packedLen = (totalSize + 1) / 2;
            var data = new byte[packedLen];
            var scales = new float[numBlocks];
            var mins = new float[numBlocks];

            for (int blockIdx = 0; blockIdx < numBlocks; blockIdx++)
            {
                int blockStart = blockIdx * Q4_1_BLOCK_SIZE;
                int blockEnd = Math.Min(blockStart + Q4_1_BLOCK_SIZE, totalSize);

                // Find min and max values in block
                float minVal = float.MaxValue;
                float maxVal = float.MinValue;
                for (int i = blockStart; i < blockEnd; i++)
                {
                    float val = source[i];
                    if (val < minVal) minVal = val;
                    if (val > maxVal) maxVal = val;
                }

                // Compute scale and min for 4-bit unsigned range [0, 15]
                float range = maxVal - minVal;
                float scale = range > 0f ? range / 15f : 1f;
                scales[blockIdx] = scale;
                mins[blockIdx] = minVal;

                // Quantize values in block
                float invScale = 1f / scale;
                for (int i = blockStart; i < blockEnd; i++)
                {
                    float normalized = (source[i] - minVal) * invScale;
                    // Clamp to [0, 15] and round
                    int clamped = (int)MathF.Round(Math.Clamp(normalized, 0f, 15f));
                    
                    // Pack two 4-bit values per byte
                    // Low nibble = even index, high nibble = odd index
                    int byteIdx = i / 2;
                    if (i % 2 == 0)
                    {
                        // Low nibble (bits 0-3)
                        data[byteIdx] = (byte)(clamped & 0xF);
                    }
                    else
                    {
                        // High nibble (bits 4-7) - preserve low nibble
                        data[byteIdx] = (byte)((data[byteIdx] & 0x0F) | ((clamped & 0xF) << 4));
                    }
                }
            }

            return new Q4_1Tensor(rows, cols, data, scales, mins);
        }

        /// <summary>
        /// Decode a 4-bit unsigned value from a nibble.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int DecodeNibble(byte nibble)
        {
            // 4-bit unsigned: [0, 15]
            return nibble & 0xF;
        }

        /// <summary>
        /// Dequantize back to float array (for reference/testing).
        /// Not used in hot path inference.
        /// </summary>
        public float[] Dequantize()
        {
            int totalSize = Rows * Cols;
            var result = new float[totalSize];
            int numBlocks = Scales.Length;

            for (int blockIdx = 0; blockIdx < numBlocks; blockIdx++)
            {
                int blockStart = blockIdx * Q4_1_BLOCK_SIZE;
                int blockEnd = Math.Min(blockStart + Q4_1_BLOCK_SIZE, totalSize);
                float scale = Scales[blockIdx];
                float min = Mins[blockIdx];

                for (int i = blockStart; i < blockEnd; i++)
                {
                    int byteIdx = i / 2;
                    byte packedByte = Data[byteIdx];
                    
                    // Extract nibble (low or high)
                    byte nibble = (i % 2 == 0) 
                        ? (byte)(packedByte & 0xF)       // Low nibble
                        : (byte)((packedByte >> 4) & 0xF); // High nibble
                    
                    int quantized = DecodeNibble(nibble);
                    // Q4_1 formula: value = q * scale + min
                    result[i] = quantized * scale + min;
                }
            }

            return result;
        }
    }
}
