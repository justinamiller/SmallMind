using System;
using System.Runtime.CompilerServices;

namespace SmallMind.Quantization.Tensors
{
    /// <summary>
    /// Q5_0 quantized tensor: 5-bit symmetric quantization with per-block scaling.
    /// Block size is 32 (GGUF standard). Each block has:
    /// - scale: fp16 (2 bytes)
    /// - qh: 4 bytes containing high bits (1 bit per value, 32 bits total)
    /// - qs: 16 bytes containing low 4 bits (two values per byte)
    /// Total: 22 bytes per block
    /// 
    /// Dequantization:
    /// - Reconstruct 5-bit value: q = ((highBit &lt;&lt; 4) | lowNibble) - 16
    /// - Scale: value = q * scale
    /// This gives effective range [-16, 15] with better precision than Q4_0.
    /// </summary>
    internal sealed class Q5_0Tensor
    {
        private const int Q5_0_BLOCK_SIZE = 32; // GGUF standard block size for Q5_0

        /// <summary>
        /// Number of rows in the tensor.
        /// </summary>
        public int Rows { get; }

        /// <summary>
        /// Number of columns in the tensor.
        /// </summary>
        public int Cols { get; }

        /// <summary>
        /// Block size for quantization (fixed at 32 for Q5_0).
        /// </summary>
        public int BlockSize => Q5_0_BLOCK_SIZE;

        /// <summary>
        /// Low 4 bits of quantized data (two values per byte).
        /// Length = (Rows * Cols + 1) / 2.
        /// </summary>
        public byte[] DataLow { get; }

        /// <summary>
        /// High bits of quantized data (1 bit per value, packed in uint32).
        /// Each block has 32 values, so 32 bits = 4 bytes per block.
        /// Length = numBlocks * 4.
        /// </summary>
        public byte[] DataHigh { get; }

        /// <summary>
        /// Scale factors for each block (fp32 for simplicity).
        /// Length = ceil((Rows * Cols) / BlockSize).
        /// </summary>
        public float[] Scales { get; }

        /// <summary>
        /// Creates a Q5_0 tensor from quantized data.
        /// </summary>
        public Q5_0Tensor(int rows, int cols, byte[] dataLow, byte[] dataHigh, float[] scales)
        {
            if (rows <= 0) throw new ArgumentException("Rows must be positive", nameof(rows));
            if (cols <= 0) throw new ArgumentException("Cols must be positive", nameof(cols));
            if (dataLow == null) throw new ArgumentNullException(nameof(dataLow));
            if (dataHigh == null) throw new ArgumentNullException(nameof(dataHigh));
            if (scales == null) throw new ArgumentNullException(nameof(scales));

            int totalSize = rows * cols;
            int expectedDataLowLen = (totalSize + 1) / 2;
            if (dataLow.Length != expectedDataLowLen)
                throw new ArgumentException($"DataLow length {dataLow.Length} != expected {expectedDataLowLen}");

            int expectedBlocks = (totalSize + Q5_0_BLOCK_SIZE - 1) / Q5_0_BLOCK_SIZE;
            int expectedDataHighLen = expectedBlocks * 4; // 4 bytes per block for 32 high bits
            if (dataHigh.Length != expectedDataHighLen)
                throw new ArgumentException($"DataHigh length {dataHigh.Length} != expected {expectedDataHighLen}");

            if (scales.Length != expectedBlocks)
                throw new ArgumentException($"Scales length {scales.Length} != expected blocks {expectedBlocks}");

            Rows = rows;
            Cols = cols;
            DataLow = dataLow;
            DataHigh = dataHigh;
            Scales = scales;
        }

        /// <summary>
        /// Quantize a float array to Q5_0 format.
        /// </summary>
        /// <param name="source">Source float array (length = rows * cols).</param>
        /// <param name="rows">Number of rows.</param>
        /// <param name="cols">Number of columns.</param>
        /// <returns>Quantized Q5_0 tensor.</returns>
        public static Q5_0Tensor Quantize(float[] source, int rows, int cols)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (rows <= 0) throw new ArgumentException("Rows must be positive", nameof(rows));
            if (cols <= 0) throw new ArgumentException("Cols must be positive", nameof(cols));

            int totalSize = rows * cols;
            if (source.Length != totalSize)
                throw new ArgumentException($"Source length {source.Length} != rows*cols {totalSize}");

            int numBlocks = (totalSize + Q5_0_BLOCK_SIZE - 1) / Q5_0_BLOCK_SIZE;
            int packedLowLen = (totalSize + 1) / 2;
            int packedHighLen = numBlocks * 4; // 4 bytes per block
            
            var dataLow = new byte[packedLowLen];
            var dataHigh = new byte[packedHighLen];
            var scales = new float[numBlocks];

            for (int blockIdx = 0; blockIdx < numBlocks; blockIdx++)
            {
                int blockStart = blockIdx * Q5_0_BLOCK_SIZE;
                int blockEnd = Math.Min(blockStart + Q5_0_BLOCK_SIZE, totalSize);

                // Find max absolute value in block
                float maxAbs = 0f;
                for (int i = blockStart; i < blockEnd; i++)
                {
                    float absVal = MathF.Abs(source[i]);
                    if (absVal > maxAbs) maxAbs = absVal;
                }

                // Compute scale for 5-bit range [-16, 15]
                float scale = maxAbs > 0f ? maxAbs / 15f : 1f;
                scales[blockIdx] = scale;

                // Quantize values in block
                float invScale = 1f / scale;
                uint highBits = 0; // Accumulate high bits for this block
                
                for (int i = blockStart; i < blockEnd; i++)
                {
                    float quantized = source[i] * invScale;
                    // Clamp to [-16, 15] and round, then add 16 to get [0, 31]
                    int clamped = (int)MathF.Round(Math.Clamp(quantized, -16f, 15f)) + 16;
                    
                    // Extract low 4 bits and high bit
                    int lowNibble = clamped & 0xF;
                    int highBit = (clamped >> 4) & 1;
                    
                    // Pack low nibble (two values per byte)
                    int byteIdx = i / 2;
                    if (i % 2 == 0)
                    {
                        dataLow[byteIdx] = (byte)(lowNibble & 0xF);
                    }
                    else
                    {
                        dataLow[byteIdx] = (byte)((dataLow[byteIdx] & 0x0F) | ((lowNibble & 0xF) << 4));
                    }
                    
                    // Pack high bit into highBits accumulator
                    int bitPos = i - blockStart; // Position within block [0, 31]
                    if (highBit != 0)
                    {
                        highBits |= (uint)(1 << bitPos);
                    }
                }
                
                // Store high bits (4 bytes per block, little-endian)
                int highByteStart = blockIdx * 4;
                dataHigh[highByteStart + 0] = (byte)(highBits & 0xFF);
                dataHigh[highByteStart + 1] = (byte)((highBits >> 8) & 0xFF);
                dataHigh[highByteStart + 2] = (byte)((highBits >> 16) & 0xFF);
                dataHigh[highByteStart + 3] = (byte)((highBits >> 24) & 0xFF);
            }

            return new Q5_0Tensor(rows, cols, dataLow, dataHigh, scales);
        }

        /// <summary>
        /// Decode a 5-bit signed value from low nibble and high bit.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Decode5Bit(byte lowNibble, int highBit)
        {
            // Reconstruct 5-bit value [0, 31] then subtract 16 to get [-16, 15]
            int val = (lowNibble & 0xF) | ((highBit & 1) << 4);
            return val - 16;
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
                int blockStart = blockIdx * Q5_0_BLOCK_SIZE;
                int blockEnd = Math.Min(blockStart + Q5_0_BLOCK_SIZE, totalSize);
                float scale = Scales[blockIdx];
                
                // Load high bits for this block (4 bytes = 32 bits)
                int highByteStart = blockIdx * 4;
                uint highBits = (uint)(
                    DataHigh[highByteStart + 0] |
                    (DataHigh[highByteStart + 1] << 8) |
                    (DataHigh[highByteStart + 2] << 16) |
                    (DataHigh[highByteStart + 3] << 24)
                );

                for (int i = blockStart; i < blockEnd; i++)
                {
                    int byteIdx = i / 2;
                    byte packedByte = DataLow[byteIdx];
                    
                    // Extract low nibble
                    byte lowNibble = (i % 2 == 0) 
                        ? (byte)(packedByte & 0xF)
                        : (byte)((packedByte >> 4) & 0xF);
                    
                    // Extract high bit
                    int bitPos = i - blockStart;
                    int highBit = (int)((highBits >> bitPos) & 1);
                    
                    int quantized = Decode5Bit(lowNibble, highBit);
                    result[i] = quantized * scale;
                }
            }

            return result;
        }
    }
}
