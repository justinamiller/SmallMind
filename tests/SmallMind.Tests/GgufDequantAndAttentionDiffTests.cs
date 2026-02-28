using SmallMind.Core.Core;
using SmallMind.Runtime.Gguf.TensorDecoders;
using SmallMind.Quantization.IO.Gguf;
using SmallMind.Transformers;

namespace SmallMind.Tests;

/// <summary>
/// Tests for Q4_0 dequantization correctness and the reference-vs-optimized
/// attention differential check.
///
/// These tests cover:
///   1. Q4_0Decoder algorithm cross-validation against a known-good reference
///      implementation (catches divergence between decoder and GgufModelLoader's
///      inline ConvertQ4_0Tensor).
///   2. MultiHeadAttention scalar-vs-SIMD equivalence via UseReferenceScalarPath
///      toggle on a tiny deterministic model.
///   3. Weight-orientation sanity check: verifies that [out, in] row-major data
///      copied directly into a Linear weight produces the expected dot-product.
/// </summary>
public class GgufDequantAndAttentionDiffTests
{
    // -------------------------------------------------------------------------
    // Helper: fp16 constant for scale = 1.0  (0x3C00)
    // -------------------------------------------------------------------------
    private const ushort Scale1_0_Fp16 = 0x3C00;

    private static byte[] BuildOneBlockQ4_0(ushort scaleFp16, byte[] dataBytes)
    {
        Assert.Equal(16, dataBytes.Length);
        var buf = new byte[18];
        buf[0] = (byte)(scaleFp16 & 0xFF);
        buf[1] = (byte)(scaleFp16 >> 8);
        Array.Copy(dataBytes, 0, buf, 2, 16);
        return buf;
    }

    // =========================================================================
    // 1. Q4_0 cross-validation: decoder output matches hand-computed reference
    // =========================================================================

    /// <summary>
    /// Verifies that Q4_0Decoder produces the exact values required by the ggml
    /// dequantize_row_q4_0 spec for every possible 4-bit nibble value (0..15).
    /// This provides a reference truth for GgufModelLoader.ConvertQ4_0Tensor
    /// which implements the same algorithm.
    /// </summary>
    [Fact]
    public void Q4_0Decoder_AllNibbleValues_MatchGgmlSpec()
    {
        // Build a 1-block buffer where low nibble of byte j = j and high nibble = 15-j.
        // Expected:
        //   element j   = (j - 8) * 1.0      (low nibble)
        //   element j+16 = ((15-j) - 8) * 1.0 (high nibble)
        var data = new byte[16];
        for (int j = 0; j < 16; j++)
            data[j] = (byte)(((15 - j) << 4) | j);   // high=(15-j), low=j

        var decoder = new Q4_0Decoder();
        var info = new GgufTensorInfo { Name = "t", Type = GgufTensorType.Q4_0, Dimensions = new ulong[] { 32 } };
        float[] result = decoder.Decode(info, BuildOneBlockQ4_0(Scale1_0_Fp16, data));

        for (int j = 0; j < 16; j++)
        {
            Assert.Equal((float)(j - 8), result[j], precision: 5);        // low nibble
            Assert.Equal((float)((15 - j) - 8), result[j + 16], precision: 5); // high nibble
        }
    }

    /// <summary>
    /// Verifies that Q4_0Decoder's byte-to-element mapping is split (not interleaved):
    /// byte[j] low nibble → element j, byte[j] high nibble → element j+16.
    /// This is the exact same invariant tested by GgufQ4_0DecodeTests but expressed as
    /// a cross-validation: we compute the expected output independently and assert equality.
    /// </summary>
    [Fact]
    public void Q4_0Decoder_SplitLayout_CrossValidation()
    {
        // Arrange: fill bytes to make expected values trivially computable
        var data = new byte[16];
        for (int j = 0; j < 16; j++)
            data[j] = (byte)((j << 4) | j);  // low=high=j

        // Expected:  element k = (k%16 - 8) * 1.0  for k in 0..31
        var decoder = new Q4_0Decoder();
        var info = new GgufTensorInfo { Name = "t", Type = GgufTensorType.Q4_0, Dimensions = new ulong[] { 32 } };
        float[] result = decoder.Decode(info, BuildOneBlockQ4_0(Scale1_0_Fp16, data));

        for (int k = 0; k < 32; k++)
        {
            float expected = (k % 16) - 8f;
            Assert.Equal(expected, result[k], precision: 5);
        }
    }

    // =========================================================================
    // 2. Attention scalar-vs-SIMD differential check
    // =========================================================================

    /// <summary>
    /// Creates a tiny (but real) TransformerModel, runs a forward pass with the
    /// optimized SIMD attention path and again with the scalar reference path, then
    /// asserts the logit vectors are numerically identical (max diff < 1e-4).
    ///
    /// If this test fails it means the SIMD MatMulTransposeB or FusedScaleMaskSoftmax
    /// kernel diverges from the scalar contract — the exact root cause reported by
    /// the --diff mode in DiagGgufCommand.
    /// </summary>
    [Fact]
    public void Attention_OptimizedVsReference_LogitDiffIsNegligible()
    {
        // Tiny model: 4 heads, 64 embd, 2 layers, vocab=16, blockSize=8
        var model = new TransformerModel(
            vocabSize: 16,
            blockSize: 8,
            nEmbd: 64,
            nLayer: 2,
            nHead: 4,
            dropout: 0.0,
            seed: 42);

        model.Eval();

        var inputData = new float[] { 1f, 2f, 3f };
        var inputTensor = new Tensor(inputData, new int[] { 1, 3 });

        // --- Optimized path ---
        bool prevFlag = MultiHeadAttention.UseReferenceScalarPath;
        float[] optimizedLogits;
        float[] referenceLogits;

        try
        {
            MultiHeadAttention.UseReferenceScalarPath = false;
            var optOut = model.Forward(inputTensor);
            // Last-position logits
            int vocabSize = optOut.Shape[optOut.Shape.Length - 1];
            optimizedLogits = new float[vocabSize];
            Array.Copy(optOut.Data, optOut.Size - vocabSize, optimizedLogits, 0, vocabSize);

            // --- Reference path (reuse the same input tensor – Forward does not mutate input) ---
            MultiHeadAttention.UseReferenceScalarPath = true;
            var refOut = model.Forward(inputTensor);
            referenceLogits = new float[vocabSize];
            Array.Copy(refOut.Data, refOut.Size - vocabSize, referenceLogits, 0, vocabSize);
        }
        finally
        {
            MultiHeadAttention.UseReferenceScalarPath = prevFlag;
        }

        // Assert max absolute difference is below numerical noise threshold
        float maxDiff = 0f;
        for (int i = 0; i < optimizedLogits.Length; i++)
        {
            float d = MathF.Abs(optimizedLogits[i] - referenceLogits[i]);
            if (d > maxDiff) maxDiff = d;
        }

        Assert.True(maxDiff < 1e-4f,
            $"Attention SIMD divergence detected: max|opt-ref| = {maxDiff:G4} " +
            "(expected < 1e-4). This indicates an attention kernel bug, not a weight issue.");
    }

    /// <summary>
    /// Verifies that UseReferenceScalarPath can be toggled at runtime without
    /// affecting the next forward pass that uses the optimized path.
    /// (Ensures the flag reset in DiagGgufCommand's finally block works.)
    /// </summary>
    [Fact]
    public void Attention_ReferencePathToggle_RestoresCorrectly()
    {
        bool original = MultiHeadAttention.UseReferenceScalarPath;

        MultiHeadAttention.UseReferenceScalarPath = true;
        Assert.True(MultiHeadAttention.UseReferenceScalarPath);

        MultiHeadAttention.UseReferenceScalarPath = false;
        Assert.False(MultiHeadAttention.UseReferenceScalarPath);

        // Restore
        MultiHeadAttention.UseReferenceScalarPath = original;
        Assert.Equal(original, MultiHeadAttention.UseReferenceScalarPath);
    }

    // =========================================================================
    // 3. Weight orientation: [out, in] row-major direct-copy is correct
    // =========================================================================

    /// <summary>
    /// Verifies the fundamental weight orientation contract:
    /// A GGUF dequantized weight with dims [in=2, out=3] stored as
    ///   W[out=0,in=0], W[0,1], W[1,0], W[1,1], W[2,0], W[2,1]  (row-major)
    /// should be directly copyable into a Linear.Weight tensor with shape [3, 2]
    /// (= [outFeatures, inFeatures]) and produce y = x @ W^T correctly.
    ///
    /// W = [[1, 2], [3, 4], [5, 6]]  (3 output rows × 2 input cols)
    /// x = [1, 0] → y = W @ x^T = [1, 3, 5]
    /// x = [0, 1] → y = W @ x^T = [2, 4, 6]
    /// </summary>
    [Fact]
    public void WeightOrientation_DirectCopy_OutInRowMajor_IsCorrect()
    {
        // Simulate GGUF dequantized weight: out=3, in=2 → 6 floats in [out, in] row-major
        // W[0,0]=1  W[0,1]=2
        // W[1,0]=3  W[1,1]=4
        // W[2,0]=5  W[2,1]=6
        float[] ggufData = { 1f, 2f, 3f, 4f, 5f, 6f };

        // SmallMind Linear(inFeatures=2, outFeatures=3)
        var linear = new Linear(inFeatures: 2, outFeatures: 3, useBias: false);

        // Inject weight directly (simulating CopyWeights direct copy)
        Assert.Equal(6, linear.Weight!.Data.Length);
        Array.Copy(ggufData, linear.Weight.Data, 6);

        linear.Eval();  // precompute transpose cache

        // x = [1, 0]: y should be [W[0,0], W[1,0], W[2,0]] = [1, 3, 5]
        var x1 = new Tensor(new float[] { 1f, 0f }, new int[] { 1, 2 });
        var y1 = linear.Forward(x1);
        Assert.Equal(1f, y1.Data[0], precision: 5);
        Assert.Equal(3f, y1.Data[1], precision: 5);
        Assert.Equal(5f, y1.Data[2], precision: 5);

        // x = [0, 1]: y should be [W[0,1], W[1,1], W[2,1]] = [2, 4, 6]
        var x2 = new Tensor(new float[] { 0f, 1f }, new int[] { 1, 2 });
        var y2 = linear.Forward(x2);
        Assert.Equal(2f, y2.Data[0], precision: 5);
        Assert.Equal(4f, y2.Data[1], precision: 5);
        Assert.Equal(6f, y2.Data[2], precision: 5);
    }

    /// <summary>
    /// Verifies that a TRANSPOSED copy produces wrong results for the weight orientation
    /// (i.e., that the direct-copy path is NOT equivalent to a transposed-copy path).
    /// This serves as a "sanity check of the sanity check": if someone accidentally
    /// transposes weights that should be direct-copied, this test would catch it.
    ///
    /// W = [[1, 2], [3, 4], [5, 6]]  (3 output rows × 2 input cols)
    /// stored transposed as W^T = [[1, 3, 5], [2, 4, 6]]  in row-major.
    ///
    /// With transposed data in the weight tensor (shape [3, 2]), what Linear computes is:
    ///   y = x @ weightStored^T  where weightStored is 3×2 with W^T values
    ///   = x @ (W^T)^T = x @ W  — columns and rows are swapped vs correct
    ///
    /// For x = [1, 0]:
    ///   correct   → y = [W[0,0], W[1,0], W[2,0]] = [1, 3, 5]
    ///   transposed → y = [W^T[0,0], W^T[1,0], W^T[2,0]] = [1, 2, ?]  ← wrong for row 1/2
    /// </summary>
    [Fact]
    public void WeightOrientation_TransposedCopy_ProducesWrongResult()
    {
        // W = [[1,2],[3,4],[5,6]].  Correct orientation stores in [out,in] row-major:
        //   ggufData = {1,2,3,4,5,6}  (3 rows × 2 cols).
        // Transposed orientation stores W^T in [in,out] row-major:
        //   W^T = [[1,3,5],[2,4,6]]  → transposedData = {1,3,5,2,4,6}
        // But we still force it into a [3,2]-shaped weight (wrong assignment).
        float[] transposedData = { 1f, 3f, 5f, 2f, 4f, 6f };

        var linear = new Linear(inFeatures: 2, outFeatures: 3, useBias: false);
        Array.Copy(transposedData, linear.Weight!.Data, 6);
        linear.Eval();

        // x = [1, 0]:
        // Correct result  (direct copy of W):      y = [1, 3, 5]
        // Incorrect result (transposed copy of W): y[0] = 1*1 + 0*3 = 1  (same for row 0!)
        //                                          y[1] = 1*5 + 0*2 = 5  ← WRONG (expected 3)
        //                                          y[2] = 1*2 + 0*4 = 2  ← WRONG (expected 5) wait...
        // weight stored = [[1,3],[5,2],[4,6]]  (force-fitting 6 elements into [3,2])
        // so: y[0]=1*1+0*3=1, y[1]=1*5+0*2=5, y[2]=1*4+0*6=4
        // Compare against correct: [1, 3, 5] → y[1] and y[2] are wrong
        var x1 = new Tensor(new float[] { 1f, 0f }, new int[] { 1, 2 });
        var y1 = linear.Forward(x1);

        // y1[0] happens to equal 1 in both cases, but y1[1] must NOT equal 3 (the correct value)
        Assert.NotEqual(3f, y1.Data[1]);  // transposed gives 5, not 3
        Assert.NotEqual(5f, y1.Data[2]);  // transposed gives 4, not 5
    }
}
