using SmallMind.Core.Optimized;

namespace SmallMind.Tests;

/// <summary>
/// Tests for OptimizedOps.FusedScaleMaskSoftmax correctness.
/// Regression guard: the Padé [2/2] FastExp approximation previously used here
/// had >1000% relative error for inputs below -3, causing completely wrong attention
/// weights and garbage model output.  These tests ensure the implementation uses
/// accurate exp() and produces correct softmax distributions.
///
/// FusedScaleMaskSoftmax operates on a (seqLen × kSeqLen) scores matrix where row i
/// applies a causal mask: only positions 0..(cacheOffset+i) are valid, the rest are zeroed.
/// </summary>
public class OptimizedOpsSoftmaxTests
{
    private const float Tolerance = 1e-4f;

    // ── Softmax produces valid probability distribution ──────────────────────

    [Fact]
    public void FusedScaleMaskSoftmax_LastRow_OutputSumsToOne()
    {
        // seqLen=4 → last row (i=3) can attend to all 4 positions
        int seqLen = 4;
        // Row 3 gets scores [1.0, 0.5, -0.5, -2.0]
        float[] scores = new float[seqLen * seqLen];
        scores[3 * seqLen + 0] = 1.0f;
        scores[3 * seqLen + 1] = 0.5f;
        scores[3 * seqLen + 2] = -0.5f;
        scores[3 * seqLen + 3] = -2.0f;
        float[] output = new float[seqLen * seqLen];

        OptimizedOps.FusedScaleMaskSoftmax(scores, 1.0f, output, seqLen);

        // Row 3 must sum to 1 (causal mask passes all 4 positions for i=3)
        float sum = output[3 * seqLen] + output[3 * seqLen + 1] + output[3 * seqLen + 2] + output[3 * seqLen + 3];
        Assert.Equal(1.0f, sum, precision: 4);
    }

    [Fact]
    public void FusedScaleMaskSoftmax_LargeNegativeInputs_NearZeroWeight()
    {
        // Key regression test: with winner=0.0 and loser=-20.0 the loser must get
        // near-zero weight.  The broken Padé approximation returned ~0.55 for
        // exp(-20) instead of ~2e-9, completely distorting the attention distribution.
        //
        // Use seqLen=2 so row 1 (i=1) can attend to both positions.
        int seqLen = 2;
        // Row 0 (i=0): only position 0 visible, trivially 1.0
        // Row 1 (i=1): positions 0 and 1 with scores [0.0, -20.0]
        float[] scores = { 0.0f, 0.0f,    // row 0
                            0.0f, -20.0f }; // row 1
        float[] output = new float[seqLen * seqLen];

        OptimizedOps.FusedScaleMaskSoftmax(scores, 1.0f, output, seqLen);

        // Row 1: winner weight ~1.0, loser weight ~0 (exp(-20) ≈ 2e-9)
        float winner = output[seqLen + 0];
        float loser  = output[seqLen + 1];
        Assert.True(winner > 0.999f, $"Winner weight should be > 0.999, was {winner}");
        Assert.True(loser  < 1e-6f,  $"Loser weight should be near 0 (exp(-20)), was {loser}");
    }

    [Fact]
    public void FusedScaleMaskSoftmax_AllEqualScores_UniformDistribution()
    {
        // Row 3 (i=3) with all equal scores → uniform over 4 positions
        int seqLen = 4;
        float[] scores = new float[seqLen * seqLen];
        for (int j = 0; j < seqLen; j++) scores[3 * seqLen + j] = 1.0f;
        float[] output = new float[seqLen * seqLen];

        OptimizedOps.FusedScaleMaskSoftmax(scores, 1.0f, output, seqLen);

        for (int j = 0; j < seqLen; j++)
            Assert.Equal(0.25f, output[3 * seqLen + j], precision: 4);
    }

    [Fact]
    public void FusedScaleMaskSoftmax_CausalMask_ZerosOutFuturePositions()
    {
        // At position 1 (i=1), only positions 0 and 1 can be attended to.
        // Positions 2 and 3 should be zero (causal mask).
        int seqLen = 4;
        float[] scores = { 1.0f, 1.0f, 1.0f, 1.0f,   // row 0
                            1.0f, 1.0f, 1.0f, 1.0f,   // row 1
                            1.0f, 1.0f, 1.0f, 1.0f,   // row 2
                            1.0f, 1.0f, 1.0f, 1.0f };  // row 3
        float[] output = new float[seqLen * seqLen];

        OptimizedOps.FusedScaleMaskSoftmax(scores, 1.0f, output, seqLen);

        // Row 0 (i=0): only position 0 is valid, must sum to 1
        Assert.Equal(1.0f, output[0], precision: 4);
        Assert.Equal(0.0f, output[1], precision: 4);
        Assert.Equal(0.0f, output[2], precision: 4);
        Assert.Equal(0.0f, output[3], precision: 4);

        // Row 1 (i=1): positions 0 and 1 split evenly
        Assert.Equal(0.5f, output[4], precision: 4);
        Assert.Equal(0.5f, output[5], precision: 4);
        Assert.Equal(0.0f, output[6], precision: 4);
        Assert.Equal(0.0f, output[7], precision: 4);
    }

    [Fact]
    public void FusedScaleMaskSoftmax_ScaleApplied_CorrectlyReducesScores()
    {
        // seqLen=2 so row 1 can see both positions.
        // Scale=0.125 (1/sqrt(64), typical for headSize=64 attention).
        // Scores [8.0, 0.0] * 0.125 = [1.0, 0.0] → softmax ≈ [0.731, 0.269].
        int seqLen = 2;
        float[] scores = { 0.0f, 0.0f,    // row 0 (arbitrary)
                            8.0f, 0.0f };  // row 1
        float[] output = new float[seqLen * seqLen];
        float scale = 0.125f;

        OptimizedOps.FusedScaleMaskSoftmax(scores, scale, output, seqLen);

        float expectedWinner = MathF.Exp(1.0f) / (MathF.Exp(1.0f) + 1.0f);
        float expectedLoser  = 1.0f / (MathF.Exp(1.0f) + 1.0f);

        Assert.Equal(expectedWinner, output[seqLen + 0], precision: 4);
        Assert.Equal(expectedLoser,  output[seqLen + 1], precision: 4);
    }

    [Fact]
    public void FusedScaleMaskSoftmax_MatchesReferenceImplementation()
    {
        // Compare against a reference implementation using exact MathF.Exp.
        // Row seqLen-1 can attend to all positions; test with varied scores including
        // large negatives that the Padé approximation handled catastrophically wrong.
        int seqLen = 8;
        float scale = 0.125f;
        float[] rowScores = { 3.2f, -1.5f, 0.8f, -10.0f, 2.1f, -5.0f, 0.0f, -20.0f };

        float[] scores = new float[seqLen * seqLen];
        for (int j = 0; j < seqLen; j++) scores[(seqLen - 1) * seqLen + j] = rowScores[j];

        float[] output = new float[seqLen * seqLen];

        // Reference: causal softmax over last row using exact MathF.Exp
        float max = float.NegativeInfinity;
        for (int j = 0; j < seqLen; j++) max = MathF.Max(max, rowScores[j] * scale);
        float sum = 0;
        var expected = new float[seqLen];
        for (int j = 0; j < seqLen; j++) { expected[j] = MathF.Exp(rowScores[j] * scale - max); sum += expected[j]; }
        for (int j = 0; j < seqLen; j++) expected[j] /= sum;

        OptimizedOps.FusedScaleMaskSoftmax(scores, scale, output, seqLen);

        for (int j = 0; j < seqLen; j++)
            Assert.Equal(expected[j], output[(seqLen - 1) * seqLen + j], precision: 4);
    }
}

