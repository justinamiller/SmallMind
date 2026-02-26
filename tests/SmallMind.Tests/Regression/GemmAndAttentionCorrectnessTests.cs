using SmallMind.Core.Simd;
using SmallMind.Tests.Fixtures;
using SmallMind.Transformers;

namespace SmallMind.Tests.Regression
{
    /// <summary>
    /// Regression tests for two correctness bugs fixed together:
    ///
    /// 1. GemmMicrokernels: The 256×256 and 512×512 fast-paths called C.Clear() unconditionally,
    ///    discarding existing accumulator values when accumulate=true.  Separately, matrices with
    ///    M smaller than the SIMD register-blocking width (MR=6) produced only partial scalar tiles
    ///    whose output contained residual FP noise across multiple K-blocks for non-power-of-two
    ///    real-world shapes (e.g. TinyLlama: M=8, K=2048, N=2560).
    ///
    /// 2. MultiHeadAttention: GetOrAllocateWorkspace was called with clearBeforeReuse=false for
    ///    the scores workspace. Because the scores tensor shape is reused across forward passes
    ///    in multi-step generation, stale attention scores from a previous step leaked into the
    ///    current softmax, corrupting the output.
    /// </summary>
    [Trait("Category", "Regression")]
    [Trait("Subcategory", "Correctness")]
    public class GemmAndAttentionCorrectnessTests
    {
        // Tolerance: relative error ≤ 1 % for large matrix products (accumulated FP32 rounding)
        private const float RelTolerance = 0.01f;
        // Small positive value used as a denominator guard in relative-error calculation
        private const float Epsilon = 1e-6f;

        #region Helpers

        private static void NaiveMatMul(float[] A, float[] B, float[] C, int M, int K, int N)
        {
            Array.Clear(C, 0, C.Length);
            for (int i = 0; i < M; i++)
                for (int k = 0; k < K; k++)
                    for (int j = 0; j < N; j++)
                        C[i * N + j] += A[i * K + k] * B[k * N + j];
        }

        private static void FillRandom(float[] arr, Random rng)
        {
            for (int i = 0; i < arr.Length; i++)
                arr[i] = (float)(rng.NextDouble() * 2.0 - 1.0);
        }

        /// <summary>Returns the maximum relative error between two arrays.</summary>
        private static float MaxRelError(float[] expected, float[] actual)
        {
            float maxErr = 0f;
            for (int i = 0; i < expected.Length; i++)
            {
                float denom = Math.Abs(expected[i]);
                float err = Math.Abs(expected[i] - actual[i]) / (denom > Epsilon ? denom : 1f);
                if (err > maxErr) maxErr = err;
            }
            return maxErr;
        }

        #endregion

        #region Bug 1a – GemmMicrokernels fast-path accumulated incorrectly with accumulate=true

        [Fact]
        public void GemmMicrokernels_Accumulate_256x256_AddsToPreviousResult()
        {
            // Strategy: set A = zeros so A×B = 0 exactly.
            // accumulate=true  → C must stay unchanged (C += 0 = C).
            // accumulate=false → C must become 0      (C  = 0).
            // This is exact arithmetic, no FP-tolerance needed.
            const int M = 256, K = 256, N = 256;
            var rng = new Random(1234);
            float[] A = new float[M * K];    // intentionally all-zero
            float[] B = new float[K * N];
            float[] C_bias = new float[M * N];

            FillRandom(B, rng);
            FillRandom(C_bias, rng);   // non-zero bias values

            // Act – accumulate=true: C should be unchanged when A is zero
            float[] C_test = (float[])C_bias.Clone();
            MatMulOps.MatMul(A, B, C_test, M, K, N, accumulate: true);

            // Assert – every element must equal the original bias
            for (int i = 0; i < C_bias.Length; i++)
            {
                Assert.True(C_test[i] == C_bias[i],
                    $"256×256 accumulate=true: C[{i}] = {C_test[i]} but bias was {C_bias[i]}. " +
                    "Likely caused by C.Clear() inside the 256×256 fast-path ignoring accumulate=true.");
            }
        }

        [Fact]
        public void GemmMicrokernels_Accumulate_512x512_AddsToPreviousResult()
        {
            // Same zero-A strategy for the 512×512 fast-path.
            const int M = 512, K = 512, N = 512;
            var rng = new Random(5678);
            float[] A = new float[M * K];    // all-zero
            float[] B = new float[K * N];
            float[] C_bias = new float[M * N];

            FillRandom(B, rng);
            FillRandom(C_bias, rng);

            float[] C_test = (float[])C_bias.Clone();
            MatMulOps.MatMul(A, B, C_test, M, K, N, accumulate: true);

            for (int i = 0; i < C_bias.Length; i++)
            {
                Assert.True(C_test[i] == C_bias[i],
                    $"512×512 accumulate=true: C[{i}] = {C_test[i]} but bias was {C_bias[i]}. " +
                    "Likely caused by C.Clear() inside the 512×512 fast-path ignoring accumulate=true.");
            }
        }

        #endregion

        #region Bug 1b – GemmMicrokernels correctness for TinyLlama-shaped matrices

        /// <summary>
        /// Validates GemmMicrokernels against a naive reference for the TinyLlama feed-forward
        /// projection shapes (M=8, K=2048, N=2560).  These are the shapes that expose the
        /// partial-tile edge case on the AVX-512/AVX2 blocked path (M=8 is not a multiple of
        /// MR=6, so rows 6-7 fall through to the scalar microkernel across every K-block).
        /// </summary>
        [Theory]
        [InlineData(8, 2048, 2560)]   // TinyLlama feed-forward up-projection
        [InlineData(8, 2048, 2048)]   // query/key projection
        [InlineData(8, 2048, 512)]    // down-projection (smaller N)
        [InlineData(7, 1024, 1024)]   // M<MR_AVX2 path
        [InlineData(4, 512, 512)]     // very small M
        public void GemmMicrokernels_TinyLlamaShapes_MatchNaiveReference(int M, int K, int N)
        {
            var rng = new Random(42 + M + K + N);
            float[] A = new float[M * K];
            float[] B = new float[K * N];
            float[] C_ref = new float[M * N];
            float[] C_test = new float[M * N];

            FillRandom(A, rng);
            FillRandom(B, rng);

            NaiveMatMul(A, B, C_ref, M, K, N);
            MatMulOps.MatMul(A, B, C_test, M, K, N);   // accumulate=false (default)

            float err = MaxRelError(C_ref, C_test);
            Assert.True(err < RelTolerance,
                $"M={M} K={K} N={N}: max relative error {err:E3} exceeds {RelTolerance}.");
        }

        #endregion

        #region Bug 2 – MultiHeadAttention stale scores across reused workspace

        /// <summary>
        /// Runs the same multi-head attention forward pass three times back-to-back with an
        /// identical query/key/value input.  If stale attention scores from a previous step are
        /// leaked into the softmax (due to clearBeforeReuse=false on the scores workspace), the
        /// outputs would differ between calls.
        /// </summary>
        [Fact]
        public void MultiHeadAttention_RepeatedForward_ProducesIdenticalOutputs()
        {
            const int nEmbd = 32;
            const int nHead = 4;
            const int blockSize = 16;
            const int seqLen = 8;     // sequence length for the test input
            const float dropout = 0.0f;
            var random = new Random(99);

            var attn = new MultiHeadAttention(nEmbd, nHead, blockSize, dropout, random);
            attn.Eval();

            // Create a fixed input tensor that we'll reuse across all three calls
            var input = new SmallMind.Core.Core.Tensor(new int[] { 1, seqLen, nEmbd });
            for (int i = 0; i < input.Size; i++)
                input.Data[i] = (float)(random.NextDouble() * 0.2 - 0.1);

            // First forward pass
            var out1 = attn.Forward(input);
            float[] snapshot1 = (float[])out1.Data.Clone();

            // Second forward pass – the scores workspace is now reused
            var out2 = attn.Forward(input);
            float[] snapshot2 = (float[])out2.Data.Clone();

            // Third forward pass
            var out3 = attn.Forward(input);
            float[] snapshot3 = (float[])out3.Data.Clone();

            // All three outputs must be bitwise identical (same input, eval mode, no dropout)
            for (int i = 0; i < snapshot1.Length; i++)
            {
                Assert.True(snapshot1[i] == snapshot2[i],
                    $"Attention output differs between pass 1 and pass 2 at index {i}: " +
                    $"{snapshot1[i]} vs {snapshot2[i]}. " +
                    "Likely stale scores workspace – clearBeforeReuse was false.");
                Assert.True(snapshot1[i] == snapshot3[i],
                    $"Attention output differs between pass 1 and pass 3 at index {i}: " +
                    $"{snapshot1[i]} vs {snapshot3[i]}.");
            }
        }

        /// <summary>
        /// Simulates multi-step generation: each step presents a single new token at an
        /// incrementally longer sequence position.  If workspace reuse corrupts the scores,
        /// individual forward passes would produce non-finite values.
        /// </summary>
        [Fact]
        public void TransformerBlock_MultiStep_ScoresRemainFinite()
        {
            const int nEmbd = 32;
            const int nHead = 4;
            const int blockSize = 16;
            // Small vocab; only needs to be large enough to avoid out-of-range token IDs
            const int vocabSize = 64;
            const double dropout = 0.0;
            // Number of autoregressive decode steps; 6 is enough to trigger workspace reuse
            // multiple times across overlapping causal windows.
            const int numSteps = 6;
            var random = new Random(7);

            var model = new SmallMind.Transformers.TransformerModel(
                vocabSize: vocabSize,
                blockSize: blockSize,
                nEmbd: nEmbd,
                nLayer: 2,
                nHead: nHead,
                dropout: dropout,
                seed: 7);
            model.Eval();

            // Run sequential single-token forward passes (simulates autoregressive decode)
            for (int step = 0; step < numSteps; step++)
            {
                var token = new SmallMind.Core.Core.Tensor(
                    new float[] { (float)(step + 1) },
                    new int[] { 1, 1 });

                var logits = model.Forward(token, positionOffset: step);

                // Verify no NaN/Inf in logits at every step
                for (int i = 0; i < logits.Data.Length; i++)
                {
                    Assert.True(float.IsFinite(logits.Data[i]),
                        $"Step {step}: logit[{i}] = {logits.Data[i]} is not finite. " +
                        "Possible stale attention scores corrupting softmax.");
                }
            }
        }

        #endregion
    }
}
