using System;
using System.Runtime.CompilerServices;

namespace SmallMind.Quantization.Tensors
{
    /// <summary>
    /// Q8_0 quantized tensor: 8-bit symmetric quantization with per-block scaling.
    /// Block size configurable (default 64). Each block has:
    /// - scale: float32
    /// - quantized values: sbyte[-127, 127]
    /// </summary>
    public sealed class Q8Tensor
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
        /// Quantized data (sbyte values in range [-127, 127]).
        /// Length = Rows * Cols.
        /// </summary>
        public sbyte[] Data { get; }

        /// <summary>
        /// Scale factors for each block.
        /// Length = ceil((Rows * Cols) / BlockSize).
        /// </summary>
        public float[] Scales { get; }

        /// <summary>
        /// Creates a Q8_0 tensor from quantized data.
        /// </summary>
        public Q8Tensor(int rows, int cols, int blockSize, sbyte[] data, float[] scales)
        {
            if (rows <= 0) throw new ArgumentException("Rows must be positive", nameof(rows));
            if (cols <= 0) throw new ArgumentException("Cols must be positive", nameof(cols));
            if (blockSize <= 0) throw new ArgumentException("BlockSize must be positive", nameof(blockSize));
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (scales == null) throw new ArgumentNullException(nameof(scales));

            int totalSize = rows * cols;
            if (data.Length != totalSize)
                throw new ArgumentException($"Data length {data.Length} != rows*cols {totalSize}");

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
        /// Quantize a float array to Q8_0 format.
        /// </summary>
        /// <param name="source">Source float array (length = rows * cols).</param>
        /// <param name="rows">Number of rows.</param>
        /// <param name="cols">Number of columns.</param>
        /// <param name="blockSize">Block size (default 64).</param>
        /// <returns>Quantized Q8 tensor.</returns>
        public static Q8Tensor Quantize(float[] source, int rows, int cols, int blockSize = 64)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (rows <= 0) throw new ArgumentException("Rows must be positive", nameof(rows));
            if (cols <= 0) throw new ArgumentException("Cols must be positive", nameof(cols));
            if (blockSize <= 0) throw new ArgumentException("BlockSize must be positive", nameof(blockSize));

            int totalSize = rows * cols;
            if (source.Length != totalSize)
                throw new ArgumentException($"Source length {source.Length} != rows*cols {totalSize}");

            int numBlocks = (totalSize + blockSize - 1) / blockSize;
            var data = new sbyte[totalSize];
            var scales = new float[numBlocks];

            for (int blockIdx = 0; blockIdx < numBlocks; blockIdx++)
            {
                int blockStart = blockIdx * blockSize;
                int blockEnd = Math.Min(blockStart + blockSize, totalSize);
                int currentBlockSize = blockEnd - blockStart;

                // Find max absolute value in block
                float maxAbs = 0f;
                for (int i = blockStart; i < blockEnd; i++)
                {
                    float absVal = MathF.Abs(source[i]);
                    if (absVal > maxAbs) maxAbs = absVal;
                }

                // Compute scale (avoid division by zero)
                float scale = maxAbs > 0f ? maxAbs / 127f : 1f;
                scales[blockIdx] = scale;

                // Quantize values in block
                float invScale = 1f / scale;
                for (int i = blockStart; i < blockEnd; i++)
                {
                    float quantized = source[i] * invScale;
                    // Clamp to [-127, 127] and round
                    int clamped = (int)MathF.Round(Math.Clamp(quantized, -127f, 127f));
                    data[i] = (sbyte)clamped;
                }
            }

            return new Q8Tensor(rows, cols, blockSize, data, scales);
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
                    result[i] = Data[i] * scale;
                }
            }

            return result;
        }
    }
}
