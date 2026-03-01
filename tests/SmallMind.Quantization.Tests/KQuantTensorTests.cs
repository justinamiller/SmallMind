using SmallMind.Quantization.Abstractions;
using SmallMind.Quantization.Tensors;

namespace SmallMind.Quantization.Tests
{
    /// <summary>
    /// Tests for Q4_K and Q6_K K-quant tensors.
    /// Validates block structure, dequantization, and fused MatMul correctness.
    /// </summary>
    public class KQuantTensorTests
    {
        private const float MatMul_Tolerance = 0.01f; // 1% tolerance for MatMul correctness

        #region Q4_K Tests

        [Fact]
        public void Q4K_BlockSize_IsCorrect()
        {
            Assert.Equal(256, Q4KTensor.GetBlockSize());
        }

        [Fact]
        public void Q4K_BytesPerBlock_IsCorrect()
        {
            // 2 (d fp16) + 2 (dmin fp16) + 12 (scales) + 128 (qs) = 144 bytes
            Assert.Equal(144, Q4KTensor.GetBytesPerBlock());
        }

        [Fact]
        public void Q4K_Constructor_ValidDimensions_Succeeds()
        {
            // Arrange & Act
            var tensor = new Q4KTensor(rows: 256, cols: 256);

            // Assert
            Assert.Equal(256, tensor.Rows);
            Assert.Equal(256, tensor.Cols);

            int expectedBlocks = (256 * 256) / 256; // 256 blocks
            int expectedBytes = expectedBlocks * 144;
            Assert.Equal(expectedBytes, tensor.Data.Length);
        }

        [Fact]
        public void Q4K_Constructor_InvalidDimensions_Throws()
        {
            // Columns must be divisible by 256
            Assert.Throws<ArgumentException>(() => new Q4KTensor(256, 100));
        }

        [Fact]
        public void Q4K_Dequantize_KnownValues_ProducesExpectedOutput()
        {
            // Arrange: Create a simple Q4_K block with known values
            var tensor = new Q4KTensor(rows: 1, cols: 256);

            // Manually construct a single block:
            // d = 1.0f (fp16), dmin = 0.0f (fp16), scales = all 1s, qs = sequential 0-15 pattern
            ushort d_fp16 = FloatToHalf(1.0f);
            ushort dmin_fp16 = FloatToHalf(0.0f);

            tensor.Data[0] = (byte)(d_fp16 & 0xFF);
            tensor.Data[1] = (byte)((d_fp16 >> 8) & 0xFF);
            tensor.Data[2] = (byte)(dmin_fp16 & 0xFF);
            tensor.Data[3] = (byte)((dmin_fp16 >> 8) & 0xFF);

            // Set scales to encode value 1 (6-bit = 1)
            for (int i = 4; i < 16; i++)
            {
                tensor.Data[i] = 0x04; // 6-bit value 1 in various positions
            }

            // Set qs to simple pattern (0,1,2,3,... packed as nibbles)
            for (int i = 0; i < 128; i++)
            {
                byte nibble_low = (byte)((i * 2) % 16);
                byte nibble_high = (byte)((i * 2 + 1) % 16);
                tensor.Data[16 + i] = (byte)((nibble_high << 4) | nibble_low);
            }

            // Act
            float[] result = tensor.Dequantize();

            // Assert
            Assert.Equal(256, result.Length);
            // Values should be in reasonable range given Q4_K: d=1.0, 6-bit scale≤63, 4-bit q≤15 → max |val|≤945
            foreach (var val in result)
            {
                Assert.True(float.IsFinite(val), "Dequantized value should be finite");
                Assert.InRange(val, -1000f, 1000f); // Reasonable range for Q4_K
            }
        }

        [Fact]
        public void Q4K_WeightTensor_ImplementsInterface()
        {
            // Arrange
            var tensor = new Q4KTensor(rows: 256, cols: 256);
            var weightTensor = new Q4KWeightTensor(tensor);

            // Assert
            Assert.Equal(256, weightTensor.Rows);
            Assert.Equal(256, weightTensor.Cols);
            Assert.Equal(Abstractions.QuantScheme.Q4_K, weightTensor.Scheme);
        }

        [Fact]
        public void Q4K_FusedMatMul_MatchesReferenceImplementation()
        {
            // Arrange: Create small matrices for testing
            int M = 4, K = 256, N = 256;
            var random = new Random(42);

            // Create random FP32 activations
            var activations = new float[M * K];
            for (int i = 0; i < activations.Length; i++)
                activations[i] = (float)(random.NextDouble() * 2.0 - 1.0);

            // Create Q4_K weights with realistic data
            var weights = new Q4KTensor(rows: K, cols: N);
            FillQ4KWithRandomData(weights, random);

            var weightTensor = new Q4KWeightTensor(weights);

            // Act: Fused MatMul
            var outputFused = new float[M * N];
            weightTensor.MatMul(activations, outputFused, M, K, N);

            // Reference: Dequantize then MatMul
            var weightsF32 = weights.Dequantize();
            var outputReference = new float[M * N];
            NaiveMatMul(activations, weightsF32, outputReference, M, K, N);

            // Assert: Results should be close
            AssertArraysClose(outputReference, outputFused, MatMul_Tolerance);
        }

        #endregion

        #region Q6_K Tests

        [Fact]
        public void Q6K_BlockSize_IsCorrect()
        {
            Assert.Equal(256, Q6KTensor.GetBlockSize());
        }

        [Fact]
        public void Q6K_BytesPerBlock_IsCorrect()
        {
            // 128 (ql) + 64 (qh) + 16 (scales) + 2 (d fp16) = 210 bytes
            Assert.Equal(210, Q6KTensor.GetBytesPerBlock());
        }

        [Fact]
        public void Q6K_Constructor_ValidDimensions_Succeeds()
        {
            // Arrange & Act
            var tensor = new Q6KTensor(rows: 256, cols: 256);

            // Assert
            Assert.Equal(256, tensor.Rows);
            Assert.Equal(256, tensor.Cols);

            int expectedBlocks = (256 * 256) / 256; // 256 blocks
            int expectedBytes = expectedBlocks * 210;
            Assert.Equal(expectedBytes, tensor.Data.Length);
        }

        [Fact]
        public void Q6K_Constructor_InvalidDimensions_Throws()
        {
            // Columns must be divisible by 256
            Assert.Throws<ArgumentException>(() => new Q6KTensor(256, 100));
        }

        [Fact]
        public void Q6K_Dequantize_KnownValues_ProducesExpectedOutput()
        {
            // Arrange: Create a simple Q6_K block
            var tensor = new Q6KTensor(rows: 1, cols: 256);

            // Manually construct a single block
            ushort d_fp16 = FloatToHalf(1.0f);

            // Fill ql (low 4 bits) with pattern
            for (int i = 0; i < 128; i++)
            {
                tensor.Data[i] = (byte)(i % 16); // 0-15 pattern
            }

            // Fill qh (high 2 bits) with zeros
            for (int i = 128; i < 192; i++)
            {
                tensor.Data[i] = 0;
            }

            // Fill scales with 1s (int8)
            for (int i = 192; i < 208; i++)
            {
                tensor.Data[i] = 1;
            }

            // Set d (fp16) at end
            tensor.Data[208] = (byte)(d_fp16 & 0xFF);
            tensor.Data[209] = (byte)((d_fp16 >> 8) & 0xFF);

            // Act
            float[] result = tensor.Dequantize();

            // Assert
            Assert.Equal(256, result.Length);
            foreach (var val in result)
            {
                Assert.True(float.IsFinite(val), "Dequantized value should be finite");
                Assert.InRange(val, -100f, 100f);
            }
        }

        [Fact]
        public void Q6K_WeightTensor_ImplementsInterface()
        {
            // Arrange
            var tensor = new Q6KTensor(rows: 256, cols: 256);
            var weightTensor = new Q6KWeightTensor(tensor);

            // Assert
            Assert.Equal(256, weightTensor.Rows);
            Assert.Equal(256, weightTensor.Cols);
            Assert.Equal(Abstractions.QuantScheme.Q6_K, weightTensor.Scheme);
        }

        [Fact]
        public void Q6K_FusedMatMul_MatchesReferenceImplementation()
        {
            // Arrange
            int M = 4, K = 256, N = 256;
            var random = new Random(42);

            var activations = new float[M * K];
            for (int i = 0; i < activations.Length; i++)
                activations[i] = (float)(random.NextDouble() * 2.0 - 1.0);

            var weights = new Q6KTensor(rows: K, cols: N);
            FillQ6KWithRandomData(weights, random);

            var weightTensor = new Q6KWeightTensor(weights);

            // Act
            var outputFused = new float[M * N];
            weightTensor.MatMul(activations, outputFused, M, K, N);

            var weightsF32 = weights.Dequantize();
            var outputReference = new float[M * N];
            NaiveMatMul(activations, weightsF32, outputReference, M, K, N);

            // Assert
            AssertArraysClose(outputReference, outputFused, MatMul_Tolerance);
        }

        #endregion

        #region Helper Methods

        private void FillQ4KWithRandomData(Q4KTensor tensor, Random random)
        {
            // Fill each 144-byte block with valid data:
            // d (fp16) at [0,1] and dmin (fp16) at [2,3] must not be NaN/Infinity.
            // fp16 NaN/Inf has exponent bits [14:10] = 0b11111; fp16 1.0 = 0x3C00 is safe.
            const int bytesPerBlock = 144;
            int numBlocks = tensor.Data.Length / bytesPerBlock;
            for (int b = 0; b < numBlocks; b++)
            {
                int off = b * bytesPerBlock;
                tensor.Data[off + 0] = 0x00; // d low byte  (fp16 1.0 = 0x3C00)
                tensor.Data[off + 1] = 0x3C; // d high byte
                tensor.Data[off + 2] = 0x00; // dmin low byte (fp16 1.0 = 0x3C00)
                tensor.Data[off + 3] = 0x3C; // dmin high byte
                for (int i = 4; i < bytesPerBlock; i++)
                    tensor.Data[off + i] = (byte)random.Next(256);
            }
        }

        private void FillQ6KWithRandomData(Q6KTensor tensor, Random random)
        {
            // Fill each 210-byte block with valid data:
            // d (fp16) at [208,209] must not be NaN/Infinity.
            // fp16 1.0 = 0x3C00 is safe.
            const int bytesPerBlock = 210;
            int numBlocks = tensor.Data.Length / bytesPerBlock;
            for (int b = 0; b < numBlocks; b++)
            {
                int off = b * bytesPerBlock;
                for (int i = 0; i < 208; i++)
                    tensor.Data[off + i] = (byte)random.Next(256);
                tensor.Data[off + 208] = 0x00; // d low byte  (fp16 1.0 = 0x3C00)
                tensor.Data[off + 209] = 0x3C; // d high byte
            }
        }

        private void NaiveMatMul(float[] A, float[] B, float[] C, int M, int K, int N)
        {
            // C[M×N] = A[M×K] × B^T where B is stored as N×K (row n = weights for output n).
            // This matches the fused kernel semantics: C[m][n] = sum_k A[m][k] * B[n][k].
            for (int m = 0; m < M; m++)
            {
                for (int n = 0; n < N; n++)
                {
                    float sum = 0f;
                    for (int k = 0; k < K; k++)
                    {
                        sum += A[m * K + k] * B[n * K + k];
                    }
                    C[m * N + n] = sum;
                }
            }
        }

        private void AssertArraysClose(float[] expected, float[] actual, float tolerance)
        {
            Assert.Equal(expected.Length, actual.Length);

            for (int i = 0; i < expected.Length; i++)
            {
                float diff = Math.Abs(expected[i] - actual[i]);
                float threshold = Math.Max(Math.Abs(expected[i]) * tolerance, 1e-5f);

                Assert.True(diff <= threshold,
                    $"Arrays differ at index {i}: expected {expected[i]}, got {actual[i]}, diff {diff}, threshold {threshold}");
            }
        }

        private ushort FloatToHalf(float value)
        {
            // FP32 to FP16 conversion (signed exponent arithmetic to handle zero/denormals correctly)
            uint bits = BitConverter.ToUInt32(BitConverter.GetBytes(value), 0);
            uint sign = (bits >> 16) & 0x8000;
            int exponent = (int)((bits >> 23) & 0xFF) - 112; // signed: 0.0f gives exp=-112 ≤ 0
            uint mantissa = (bits >> 13) & 0x3FF;

            if (exponent <= 0)
                return (ushort)sign; // Zero or denormal
            if (exponent >= 31)
                return (ushort)(sign | 0x7C00); // Infinity

            return (ushort)(sign | ((uint)exponent << 10) | mantissa);
        }

        #endregion

        // ─── Exact fixture tests for Q6_K bit-accurate decode ──────────────────

        /// <summary>
        /// Verifies Q6_K dequantization against hand-computed expected values using the
        /// llama.cpp ggml layout (two 128-value half-passes, llama.cpp ql/qh indexing).
        /// </summary>
        [Fact]
        public void Q6K_Dequantize_ExactKnownBlock_MatchesExpectedValues()
        {
            // Build a single 256-element Q6_K super-block with known bytes so we can
            // compute the expected float values by hand and compare exactly.
            //
            // Layout: ql[128], qh[64], scales[16], d(fp16)[2]  — total 210 bytes.
            //
            // Chosen values (simple to verify):
            //   d = 1.0f (fp16 = 0x3C00)
            //   scales[i] = 1 for all i  (int8)
            //   ql[0..127] = 0x0F (low nibble=15, high nibble=0)
            //   qh[0..63]  = 0x00 (all high bits = 0)
            //
            // With ql[b]=0x0F and qh[b]=0x00:
            //   For half h=0, l=0..31:
            //     q0 (pos l):    low4 = ql[l]  & 0xF = 15, high2 = (qh[l]>>0)&3 = 0  → q=15  → val=1*1*(15-32)=-17
            //     q1 (pos l+32): low4 = ql[l+32]&0xF = 15, high2 = (qh[l]>>2)&3 = 0  → q=15  → val=-17
            //     q2 (pos l+64): low4 = (ql[l]>>4)&F = 0,  high2 = (qh[l]>>4)&3 = 0  → q=0   → val=1*1*(0-32)=-32
            //     q3 (pos l+96): low4 = (ql[l+32]>>4) = 0, high2 = (qh[l]>>6)&3 = 0  → q=0   → val=-32
            //   Same for half h=1 (uses ql[64..127], qh[32..63], same pattern).
            var rawBlock = new byte[210];

            // ql: all 0x0F
            for (int i = 0; i < 128; i++) rawBlock[i] = 0x0F;
            // qh: all 0x00 (already zero)
            // scales: all 1  (int8 = 0x01)
            for (int i = 192; i < 208; i++) rawBlock[i] = 0x01;
            // d = 1.0f → fp16 = 0x3C00
            rawBlock[208] = 0x00;
            rawBlock[209] = 0x3C;

            float[] dst = new float[256];
            Q6KTensor.Dequantize(rawBlock, dst);

            // Positions 0..31 and 32..63 (q0/q1, nibble=15, high2=0): expected = 1*1*(15-32) = -17
            for (int i = 0; i < 64; i++)
                Assert.Equal(-17f, dst[i], precision: 4);
            // Positions 64..127 (q2/q3, nibble=0, high2=0): expected = 1*1*(0-32) = -32
            for (int i = 64; i < 128; i++)
                Assert.Equal(-32f, dst[i], precision: 4);
            // Second half mirrors first half
            for (int i = 0; i < 64; i++)
                Assert.Equal(-17f, dst[128 + i], precision: 4);
            for (int i = 64; i < 128; i++)
                Assert.Equal(-32f, dst[128 + i], precision: 4);
        }

        /// <summary>
        /// Verifies Q6_K high-bit extraction by using non-zero qh values.
        /// Sets ql=0x00 (low4=0) and qh so that high2=1 for all positions.
        /// Expected q = 0 | (1<<4) = 16, so float = d * scale * (16-32) = 1*1*(-16) = -16.
        /// </summary>
        [Fact]
        public void Q6K_Dequantize_HighBits_ExtractedCorrectly()
        {
            var rawBlock = new byte[210];

            // ql: all 0x00 (low4=0 for all positions)
            // qh: 0x55 = 0b01010101 — bits[1:0]=01, bits[3:2]=01, bits[5:4]=01, bits[7:6]=01
            //   → high2=1 for all four positions that each qh byte covers
            for (int i = 128; i < 192; i++) rawBlock[i] = 0x55;
            // scales: all 1
            for (int i = 192; i < 208; i++) rawBlock[i] = 0x01;
            // d = 1.0f
            rawBlock[208] = 0x00; rawBlock[209] = 0x3C;

            float[] dst = new float[256];
            Q6KTensor.Dequantize(rawBlock, dst);

            // q = 0 | (1<<4) = 16  →  float = 1*1*(16-32) = -16
            foreach (float v in dst)
                Assert.Equal(-16f, v, precision: 4);
        }
    }
}
