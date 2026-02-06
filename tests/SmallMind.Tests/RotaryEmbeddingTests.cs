using System;
using Xunit;
using SmallMind.Transformers;

namespace SmallMind.Tests
{
    /// <summary>
    /// Unit tests for Rotary Position Embeddings (RoPE).
    /// Tests correctness of rotation and precomputation.
    /// </summary>
    public class RotaryEmbeddingTests
    {
        private const float Tolerance = 1e-4f;

        [Fact]
        public void Constructor_ValidParameters_CreatesSuccessfully()
        {
            // Arrange & Act
            var rope = new RotaryEmbedding(maxSeqLen: 512, headDim: 64);

            // Assert
            Assert.Equal(512, rope.MaxSeqLen);
            Assert.Equal(64, rope.HeadDim);
        }

        [Fact]
        public void Constructor_OddHeadDim_ThrowsException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentException>(() =>
                new RotaryEmbedding(maxSeqLen: 512, headDim: 63));
        }

        [Fact]
        public void ApplyInPlace_Position0_RotatesCorrectly()
        {
            // Arrange
            var rope = new RotaryEmbedding(maxSeqLen: 512, headDim: 4, theta: 10000f);
            
            // Create simple test data: Q and K with known values
            // Shape: (batch=1, heads=1, seqLen=1, headDim=4)
            var q = new float[] { 1f, 0f, 1f, 0f };  // Two pairs: (1,0) and (1,0)
            var k = new float[] { 0f, 1f, 0f, 1f };  // Two pairs: (0,1) and (0,1)

            // Act
            rope.ApplyInPlace(q, k, position: 0, batchSize: 1, nHeads: 1, nKvHeads: 1, seqLen: 1);

            // Assert - for position 0, rotation should apply based on precomputed angles
            // Freq for dim 0: 1 / (10000^(0/4)) = 1
            // Freq for dim 1: 1 / (10000^(2/4)) = 1/100
            // At position 0: angle_0 = 0 * 1 = 0, angle_1 = 0 * 0.01 = 0
            // So cos = 1, sin = 0 for all pairs at position 0
            // Rotation: (x0, x1) -> (x0*1 - x1*0, x0*0 + x1*1) = (x0, x1) (identity)
            
            Assert.Equal(1f, q[0], Tolerance); // Should remain unchanged
            Assert.Equal(0f, q[1], Tolerance);
            Assert.Equal(0f, k[0], Tolerance);
            Assert.Equal(1f, k[1], Tolerance);
        }

        [Fact]
        public void ApplyInPlace_MultiplePositions_RotatesDifferently()
        {
            // Arrange
            var rope = new RotaryEmbedding(maxSeqLen: 512, headDim: 4, theta: 10000f);
            
            // Create test data with 2 sequence positions
            // Shape: (batch=1, heads=1, seqLen=2, headDim=4)
            var q = new float[8]; // 1*1*2*4
            var k = new float[8];
            
            // Initialize with simple pattern
            for (int i = 0; i < 8; i++)
            {
                q[i] = i % 2 == 0 ? 1f : 0f;  // [1,0,1,0,1,0,1,0]
                k[i] = i % 2 == 0 ? 0f : 1f;  // [0,1,0,1,0,1,0,1]
            }

            // Act
            rope.ApplyInPlace(q, k, position: 0, batchSize: 1, nHeads: 1, nKvHeads: 1, seqLen: 2);

            // Assert - positions 0 and 1 should have different rotations
            // Position 0 at identity (as tested above), position 1 should differ
            // We can't easily verify exact values without duplicating the rotation logic,
            // but we can check that they're different and reasonable
            Assert.NotEqual(q[0], q[4]); // First element of pos 0 vs pos 1
        }

        [Fact]
        public void ApplyInPlace_GQA_HandlesMultipleKvHeads()
        {
            // Arrange
            var rope = new RotaryEmbedding(maxSeqLen: 512, headDim: 4);
            
            // Test GQA scenario: 4 query heads, 2 KV heads
            // Q: (batch=1, nHeads=4, seqLen=1, headDim=4) = 16 elements
            // K: (batch=1, nKvHeads=2, seqLen=1, headDim=4) = 8 elements
            var q = new float[16];
            var k = new float[8];
            
            // Initialize with pattern
            for (int i = 0; i < 16; i++) q[i] = (float)i;
            for (int i = 0; i < 8; i++) k[i] = (float)i;

            // Act - should not throw
            rope.ApplyInPlace(q, k, position: 0, batchSize: 1, nHeads: 4, nKvHeads: 2, seqLen: 1);

            // Assert - all values should be modified (rotated)
            // At position 0, the rotation is identity, so values remain the same
            for (int i = 0; i < 16; i++)
                Assert.Equal((float)i, q[i], Tolerance);
        }

        [Fact]
        public void ApplyInPlace_MultipleBatches_ProcessesIndependently()
        {
            // Arrange
            var rope = new RotaryEmbedding(maxSeqLen: 512, headDim: 4);
            
            // Test with 2 batches
            // Q: (batch=2, nHeads=1, seqLen=1, headDim=4) = 8 elements
            var q = new float[] { 1f, 0f, 1f, 0f, 2f, 0f, 2f, 0f };
            var k = new float[] { 0f, 1f, 0f, 1f, 0f, 2f, 0f, 2f };

            var qOriginal = (float[])q.Clone();
            var kOriginal = (float[])k.Clone();

            // Act
            rope.ApplyInPlace(q, k, position: 0, batchSize: 2, nHeads: 1, nKvHeads: 1, seqLen: 1);

            // Assert - at position 0, rotation is identity
            for (int i = 0; i < 8; i++)
            {
                Assert.Equal(qOriginal[i], q[i], Tolerance);
                Assert.Equal(kOriginal[i], k[i], Tolerance);
            }
        }

        [Fact]
        public void ApplyInPlace_PositionOffset_AppliesCorrectly()
        {
            // Arrange
            var rope = new RotaryEmbedding(maxSeqLen: 512, headDim: 4);
            
            // Test incremental decoding with position offset
            var q = new float[4] { 1f, 0f, 1f, 0f };
            var k = new float[4] { 0f, 1f, 0f, 1f };

            var q10 = (float[])q.Clone();
            var k10 = (float[])k.Clone();

            // Act - apply at position 10
            rope.ApplyInPlace(q10, k10, position: 10, batchSize: 1, nHeads: 1, nKvHeads: 1, seqLen: 1);

            // Assert - should be different from position 0
            Assert.NotEqual(q[0], q10[0]);
        }

        [Fact]
        public void ApplyInPlace_ExceedsMaxSeqLen_ThrowsException()
        {
            // Arrange
            var rope = new RotaryEmbedding(maxSeqLen: 10, headDim: 4);
            var q = new float[4];
            var k = new float[4];

            // Act & Assert
            Assert.Throws<ArgumentException>(() =>
                rope.ApplyInPlace(q, k, position: 11, batchSize: 1, nHeads: 1, nKvHeads: 1, seqLen: 1));
        }

        [Fact]
        public void RoPE_PrecomputationConsistency()
        {
            // Arrange - create two RoPE instances with same parameters
            var rope1 = new RotaryEmbedding(maxSeqLen: 128, headDim: 8, theta: 10000f);
            var rope2 = new RotaryEmbedding(maxSeqLen: 128, headDim: 8, theta: 10000f);
            
            var q1 = new float[8] { 1f, 2f, 3f, 4f, 5f, 6f, 7f, 8f };
            var k1 = new float[8] { 8f, 7f, 6f, 5f, 4f, 3f, 2f, 1f };
            
            var q2 = (float[])q1.Clone();
            var k2 = (float[])k1.Clone();

            // Act
            rope1.ApplyInPlace(q1, k1, position: 5, batchSize: 1, nHeads: 1, nKvHeads: 1, seqLen: 1);
            rope2.ApplyInPlace(q2, k2, position: 5, batchSize: 1, nHeads: 1, nKvHeads: 1, seqLen: 1);

            // Assert - should produce identical results
            for (int i = 0; i < 8; i++)
            {
                Assert.Equal(q1[i], q2[i], Tolerance);
                Assert.Equal(k1[i], k2[i], Tolerance);
            }
        }
    }
}
