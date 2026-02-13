using System.Runtime.CompilerServices;

namespace SmallMind.Quantization.Tensors
{
    /// <summary>
    /// Q4_0 quantized tensor: 4-bit symmetric quantization with per-block scaling.
    /// Block size configurable (default 64). Each block has:
    /// - scale: float32
    /// - quantized values: 4-bit signed in [-8, 7], packed two per byte
    /// </summary>
    internal sealed class Q4Tensor
    {
        /// <summary>
        /// Number of rows in the tensor.
        /// </summary>
        public int Rows { get; }

        /// <summary>
        /// Number of columns in the tensor.
        /// </summary>
        public int Cols { get; }

        /// <summary>
        /// Block size for quantization (number of elements per block).
        /// </summary>
        public int BlockSize { get; }

        /// <summary>
        /// Packed quantized data (two 4-bit values per byte).
        /// Length = (Rows * Cols + 1) / 2.
        /// </summary>
        public byte[] Data { get; }

        /// <summary>
        /// Scale factors for each block.
        /// Length = ceil((Rows * Cols) / BlockSize).
        /// </summary>
        public float[] Scales { get; }

        /// <summary>
        /// Creates a Q4_0 tensor from quantized data.
        /// </summary>
        public Q4Tensor(int rows, int cols, int blockSize, byte[] data, float[] scales)
        {
            if (rows <= 0) throw new ArgumentException("Rows must be positive", nameof(rows));
            if (cols <= 0) throw new ArgumentException("Cols must be positive", nameof(cols));
            if (blockSize <= 0) throw new ArgumentException("BlockSize must be positive", nameof(blockSize));
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (scales == null) throw new ArgumentNullException(nameof(scales));

            int totalSize = rows * cols;
            int expectedDataLen = (totalSize + 1) / 2; // Two 4-bit values per byte
            if (data.Length != expectedDataLen)
                throw new ArgumentException($"Data length {data.Length} != expected {expectedDataLen}");

            int expectedBlocks = (totalSize + blockSize - 1) / blockSize;
            if (scales.Length != expectedBlocks)
                throw new ArgumentException($"Scales length {scales.Length} != expected blocks {expectedBlocks}");

            Rows = rows;
            Cols = cols;
            BlockSize = blockSize;
            Data = data;
            Scales = scales;
        }

        /// <summary>
        /// Quantize a float array to Q4_0 format.
        /// </summary>
        /// <param name="source">Source float array (length = rows * cols).</param>
        /// <param name="rows">Number of rows.</param>
        /// <param name="cols">Number of columns.</param>
        /// <param name="blockSize">Block size (default 64).</param>
        /// <returns>Quantized Q4 tensor.</returns>
        public static Q4Tensor Quantize(float[] source, int rows, int cols, int blockSize = 64)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (rows <= 0) throw new ArgumentException("Rows must be positive", nameof(rows));
            if (cols <= 0) throw new ArgumentException("Cols must be positive", nameof(cols));
            if (blockSize <= 0) throw new ArgumentException("BlockSize must be positive", nameof(blockSize));

            int totalSize = rows * cols;
            if (source.Length != totalSize)
                throw new ArgumentException($"Source length {source.Length} != rows*cols {totalSize}");

            int numBlocks = (totalSize + blockSize - 1) / blockSize;
            int packedLen = (totalSize + 1) / 2;
            var data = new byte[packedLen];
            var scales = new float[numBlocks];

            for (int blockIdx = 0; blockIdx < numBlocks; blockIdx++)
            {
                int blockStart = blockIdx * blockSize;
                int blockEnd = Math.Min(blockStart + blockSize, totalSize);

                // Find max absolute value in block
                float maxAbs = 0f;
                for (int i = blockStart; i < blockEnd; i++)
                {
                    float absVal = MathF.Abs(source[i]);
                    if (absVal > maxAbs) maxAbs = absVal;
                }

                // Compute scale for 4-bit range [-8, 7]
                // Using 7 as max to fit signed 4-bit
                float scale = maxAbs > 0f ? maxAbs / 7f : 1f;
                scales[blockIdx] = scale;

                // Quantize values in block
                float invScale = 1f / scale;
                for (int i = blockStart; i < blockEnd; i++)
                {
                    float quantized = source[i] * invScale;
                    // Clamp to [-8, 7] and round
                    int clamped = (int)MathF.Round(Math.Clamp(quantized, -8f, 7f));

                    // Pack two 4-bit values per byte
                    // Low nibble = even index, high nibble = odd index
                    int byteIdx = i / 2;
                    if (i % 2 == 0)
                    {
                        // Low nibble (bits 0-3) - clear existing byte first
                        data[byteIdx] = (byte)(clamped & 0xF);
                    }
                    else
                    {
                        // High nibble (bits 4-7) - preserve low nibble
                        data[byteIdx] = (byte)((data[byteIdx] & 0x0F) | ((clamped & 0xF) << 4));
                    }
                }
            }

            return new Q4Tensor(rows, cols, blockSize, data, scales);
        }

        /// <summary>
        /// Decode a 4-bit signed value from a nibble.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int DecodeNibble(byte nibble)
        {
            // 4-bit two's complement: if bit 3 is set, it's negative
            return (nibble < 8) ? nibble : nibble - 16;
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
                int blockStart = blockIdx * BlockSize;
                int blockEnd = Math.Min(blockStart + BlockSize, totalSize);
                float scale = Scales[blockIdx];

                for (int i = blockStart; i < blockEnd; i++)
                {
                    int byteIdx = i / 2;
                    byte packedByte = Data[byteIdx];

                    // Extract nibble (low or high)
                    byte nibble = (i % 2 == 0)
                        ? (byte)(packedByte & 0xF)       // Low nibble
                        : (byte)((packedByte >> 4) & 0xF); // High nibble

                    int quantized = DecodeNibble(nibble);
                    result[i] = quantized * scale;
                }
            }

            return result;
        }
    }
}
