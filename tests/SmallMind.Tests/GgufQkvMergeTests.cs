using SmallMind.Runtime;

namespace SmallMind.Tests;

/// <summary>
/// Unit tests for QKV weight merging in GgufModelLoader.
/// Verifies the exact [Q || K || V] layout and shape assertions.
/// </summary>
public class GgufQkvMergeTests
{
    [Fact]
    public void MergeQkvArrays_CorrectLayout_QThenKThenV()
    {
        // Arrange: deterministic Q/K/V slices with known values
        float[] q = [1f, 2f, 3f, 4f];
        float[] k = [10f, 20f, 30f, 40f];
        float[] v = [100f, 200f, 300f, 400f];
        int expectedTotal = q.Length + k.Length + v.Length;

        // Act
        float[] merged = GgufModelLoader.MergeQkvArrays(q, k, v, expectedTotal, layerIndex: 0);

        // Assert: layout must be exactly [Q, K, V]
        Assert.Equal(expectedTotal, merged.Length);
        // Q occupies [0..3]
        Assert.Equal(1f, merged[0]);
        Assert.Equal(2f, merged[1]);
        Assert.Equal(3f, merged[2]);
        Assert.Equal(4f, merged[3]);
        // K occupies [4..7]
        Assert.Equal(10f, merged[4]);
        Assert.Equal(20f, merged[5]);
        Assert.Equal(30f, merged[6]);
        Assert.Equal(40f, merged[7]);
        // V occupies [8..11]
        Assert.Equal(100f, merged[8]);
        Assert.Equal(200f, merged[9]);
        Assert.Equal(300f, merged[10]);
        Assert.Equal(400f, merged[11]);
    }

    [Fact]
    public void MergeQkvArrays_UnequallySizedQKV_ReturnsCorrectLayout()
    {
        // Arrange: Q is larger than K/V (GQA scenario)
        float[] q = [1f, 2f, 3f, 4f, 5f, 6f, 7f, 8f]; // 8 elements (4 heads x 2)
        float[] k = [10f, 20f];                          // 2 elements (1 KV head x 2)
        float[] v = [100f, 200f];                        // 2 elements (1 KV head x 2)
        int expectedTotal = q.Length + k.Length + v.Length; // 12

        // Act
        float[] merged = GgufModelLoader.MergeQkvArrays(q, k, v, expectedTotal, layerIndex: 3);

        // Assert
        Assert.Equal(12, merged.Length);
        // Q block
        for (int i = 0; i < 8; i++)
            Assert.Equal(q[i], merged[i]);
        // K block
        Assert.Equal(10f, merged[8]);
        Assert.Equal(20f, merged[9]);
        // V block
        Assert.Equal(100f, merged[10]);
        Assert.Equal(200f, merged[11]);
    }

    [Fact]
    public void MergeQkvArrays_SizeMismatch_ThrowsWithLayerInfo()
    {
        // Arrange
        float[] q = [1f, 2f];
        float[] k = [3f, 4f];
        float[] v = [5f, 6f];
        int wrongExpectedTotal = 100; // deliberately wrong

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(
            () => GgufModelLoader.MergeQkvArrays(q, k, v, wrongExpectedTotal, layerIndex: 7));

        // Message must mention layer index and expected/actual sizes
        Assert.Contains("layer 7", ex.Message);
        Assert.Contains("6", ex.Message);      // actual total
        Assert.Contains("100", ex.Message);    // expected total
    }

    [Fact]
    public void MergeQkvArrays_EmptyArrays_ReturnsEmptyWithZeroExpected()
    {
        // Arrange
        float[] q = [];
        float[] k = [];
        float[] v = [];

        // Act
        float[] merged = GgufModelLoader.MergeQkvArrays(q, k, v, 0, layerIndex: 0);

        // Assert
        Assert.Empty(merged);
    }

    [Fact]
    public void MergeQkvArrays_DoesNotMutateInputArrays()
    {
        // Arrange
        float[] q = [1f, 2f];
        float[] k = [3f, 4f];
        float[] v = [5f, 6f];
        float[] originalQ = (float[])q.Clone();
        float[] originalK = (float[])k.Clone();
        float[] originalV = (float[])v.Clone();

        // Act
        GgufModelLoader.MergeQkvArrays(q, k, v, 6, layerIndex: 0);

        // Assert: inputs unchanged
        Assert.Equal(originalQ, q);
        Assert.Equal(originalK, k);
        Assert.Equal(originalV, v);
    }
}
