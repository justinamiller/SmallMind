using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using SmallMind.Quantization.Tensors;

namespace SmallMind.Quantization.Kernels
{
    /// <summary>
    /// Fused matrix multiplication kernels for Q4_0 quantized weights.
    /// Computes C = A * B where A is float32 and B is Q4_0 quantized.
    /// </summary>
    public static class MatMulF32Q4
    {
        // Lookup table for fast nibble to int conversion (4-bit two's complement)
        // This avoids the branch in DecodeNibble for every single element
        private static readonly int[] NibbleToInt = new int[16]
        {
            0, 1, 2, 3, 4, 5, 6, 7,      // 0-7: positive values
            -8, -7, -6, -5, -4, -3, -2, -1  // 8-15: negative values (two's complement)
        };
        
        /// <summary>
        /// Matrix multiply: C[M×N] = A[M×K] * B[K×N] where B is Q4 quantized.
        /// </summary>
        /// <param name="a">Activation matrix (float32, row-major, M×K).</param>
        /// <param name="b">Weight tensor (Q4_0 quantized, K×N).</param>
        /// <param name="c">Output matrix (float32, row-major, M×N). Must be pre-allocated.</param>
        /// <param name="m">Number of rows in A and C.</param>
        /// <param name="k">Number of columns in A and rows in B.</param>
        /// <param name="n">Number of columns in B and C.</param>
        public static void Multiply(ReadOnlySpan<float> a, Q4Tensor b, Span<float> c, int m, int k, int n)
        {
            if (b.Rows != k || b.Cols != n)
                throw new ArgumentException($"B dimensions {b.Rows}×{b.Cols} != expected {k}×{n}");
            if (a.Length < m * k)
                throw new ArgumentException($"A length {a.Length} < {m * k}");
            if (c.Length < m * n)
                throw new ArgumentException($"C length {c.Length} < {m * n}");

            // Fast path for single row (common in inference)
            if (m == 1)
            {
                MultiplyVectorMatrix(a, b, c, k, n);
                return;
            }

            // General batched case
            for (int i = 0; i < m; i++)
            {
                var aRow = a.Slice(i * k, k);
                var cRow = c.Slice(i * n, n);
                MultiplyVectorMatrix(aRow, b, cRow, k, n);
            }
        }

        /// <summary>
        /// Vector-matrix multiply: c[1×N] = a[1×K] * B[K×N] where B is Q4 quantized.
        /// Optimized for single-row inference with cache-friendly row-major traversal and LUT.
        /// </summary>
        private static void MultiplyVectorMatrix(ReadOnlySpan<float> a, Q4Tensor b, Span<float> c, int k, int n)
        {
            c.Clear(); // Zero output

            int blockSize = b.BlockSize;
            int numBlocksPerRow = (n + blockSize - 1) / blockSize;

            // Row-major traversal for better cache locality
            // Outer loop over K (rows of B), inner loop over N (columns of B)
            for (int row = 0; row < k; row++)
            {
                float aVal = a[row];
                if (aVal == 0f) continue; // Skip zero activations
                
                int rowOffset = row * n;
                int rowBlockBase = row * numBlocksPerRow;
                
                // Process output columns in this row
                for (int col = 0; col < n; col++)
                {
                    int linearIdx = rowOffset + col;
                    int blockIdx = rowBlockBase + (col / blockSize);
                    
                    if (blockIdx >= b.Scales.Length) break; // Safety
                    
                    float scale = b.Scales[blockIdx];
                    
                    // Unpack 4-bit value - branchless nibble extraction
                    int byteIdx = linearIdx >> 1; // Divide by 2
                    byte packedByte = b.Data[byteIdx];
                    int shift = (linearIdx & 1) << 2; // 0 or 4
                    byte nibble = (byte)((packedByte >> shift) & 0xF);
                    
                    // Fast LUT-based decode instead of branch
                    int quantVal = NibbleToInt[nibble];
                    c[col] += aVal * quantVal * scale;
                }
            }
        }

        /// <summary>
        /// Optimized vector-matrix multiply using SIMD where beneficial.
        /// c[1×N] = a[1×K] * B[K×N] where B is Q4 quantized.
        /// Zero-allocation SIMD path with row-major traversal and LUT.
        /// </summary>
        public static void MultiplyVectorMatrixSIMD(ReadOnlySpan<float> a, Q4Tensor b, Span<float> c, int k, int n)
        {
            c.Clear();

            int blockSize = b.BlockSize;
            int numBlocksPerRow = (n + blockSize - 1) / blockSize;
            int vectorSize = Vector<float>.Count;

            // Preallocate SIMD scratch buffer on stack to avoid allocations
            Span<float> bValsBuffer = stackalloc float[vectorSize];

            // Row-major traversal for better cache locality
            for (int row = 0; row < k; row++)
            {
                float aVal = a[row];
                if (aVal == 0f) continue; // Skip zero activations
                
                int rowOffset = row * n;
                int rowBlockBase = row * numBlocksPerRow;
                
                // Process columns in SIMD-width chunks where possible
                int col = 0;
                
                // SIMD path for aligned chunks
                if (Vector.IsHardwareAccelerated && n >= vectorSize)
                {
                    for (; col <= n - vectorSize; col += vectorSize)
                    {
                        // Determine block index for this column group
                        int blockIdx = rowBlockBase + (col / blockSize);
                        
                        if (blockIdx >= b.Scales.Length) break;
                        
                        // Load quantized values and dequantize using LUT
                        for (int vi = 0; vi < vectorSize; vi++)
                        {
                            int linearIdx = rowOffset + col + vi;
                            int localBlockIdx = rowBlockBase + ((col + vi) / blockSize);
                            
                            if (localBlockIdx >= b.Scales.Length)
                            {
                                bValsBuffer[vi] = 0f;
                                continue;
                            }
                            
                            float scale = b.Scales[localBlockIdx];
                            
                            // Branchless nibble extraction
                            int byteIdx = linearIdx >> 1;
                            byte packedByte = b.Data[byteIdx];
                            int shift = (linearIdx & 1) << 2;
                            byte nibble = (byte)((packedByte >> shift) & 0xF);
                            
                            // Fast LUT-based decode
                            int quantVal = NibbleToInt[nibble];
                            bValsBuffer[vi] = quantVal * scale;
                        }
                        
                        // SIMD multiply-accumulate
                        var bVec = new Vector<float>(bValsBuffer);
                        var aVec = new Vector<float>(aVal);
                        var cVec = new Vector<float>(c.Slice(col));
                        var result = cVec + aVec * bVec;
                        result.CopyTo(c.Slice(col));
                    }
                }
                
                // Scalar remainder
                for (; col < n; col++)
                {
                    int linearIdx = rowOffset + col;
                    int blockIdx = rowBlockBase + (col / blockSize);
                    
                    if (blockIdx >= b.Scales.Length) break;
                    
                    float scale = b.Scales[blockIdx];
                    
                    // Branchless nibble extraction
                    int byteIdx = linearIdx >> 1;
                    byte packedByte = b.Data[byteIdx];
                    int shift = (linearIdx & 1) << 2;
                    byte nibble = (byte)((packedByte >> shift) & 0xF);
                    
                    // Fast LUT-based decode
                    int quantVal = NibbleToInt[nibble];
                    c[col] += aVal * quantVal * scale;
                }
            }
        }
    }
}
