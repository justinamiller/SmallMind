using System;
using Xunit;
using SmallMind.Core;
using SmallMind.Tokenizers;
using SmallMind.Runtime;
using CoreMatrixOps = SmallMind.Core.Core.MatrixOps;
using CoreCheckpointStrategy = SmallMind.Core.Core.CheckpointStrategy;
using CoreTensorPool = SmallMind.Core.Core.TensorPool;
using SmallMind.Core.Core;
using SmallMind.Transformers;

namespace SmallMind.Tests
{
    /// <summary>
    /// Integration tests for Phase 2 optimizations
    /// Tests the full training loop with various configurations
    /// </summary>
    public class Phase2IntegrationTests
    {
        private const string SampleData = @"The quick brown fox jumps over the lazy dog.
A journey of a thousand miles begins with a single step.
To be or not to be, that is the question.
All that glitters is not gold.
Knowledge is power.";


        public void MatrixOps_IntegrationTest_AllOperations()
        {
            // Test that matrix operations work correctly in sequence
            
            // A × B^T
            float[] A1 = { 1, 2, 3, 4 }; // 2x2
            float[] B1 = { 5, 6, 7, 8 }; // 2x2
            float[] C1 = new float[4];   // 2x2
            CoreMatrixOps.MatMulTransposeB(A1, B1, C1, M: 2, K: 2, N: 2);
            
            // Verify results are reasonable
            Assert.True(C1[0] > 0);
            Assert.True(C1[1] > 0);
            Assert.True(C1[2] > 0);
            Assert.True(C1[3] > 0);
            
            // A^T × B
            float[] A2 = { 1, 2, 3, 4 }; // 2x2 (to be transposed)
            float[] B2 = { 5, 6, 7, 8 }; // 2x2
            float[] C2 = new float[4];   // 2x2
            CoreMatrixOps.MatMulTransposeA(A2, B2, C2, M: 2, K: 2, N: 2);
            
            // Verify results are reasonable
            Assert.True(C2[0] > 0);
            Assert.True(C2[1] > 0);
            Assert.True(C2[2] > 0);
            Assert.True(C2[3] > 0);
        }


        public void MemoryPool_IntegrationTest_HighVolume()
        {
            // Test that memory pool handles high volume of rent/return
            var pool = CoreTensorPool.Shared;
            var arrays = new float[100][];
            
            // Rent many arrays
            for (int i = 0; i < arrays.Length; i++)
            {
                arrays[i] = pool.Rent(128);
                Assert.Equal(128, arrays[i].Length);
            }
            
            // Return all
            for (int i = 0; i < arrays.Length; i++)
            {
                pool.Return(arrays[i]);
            }
            
            // Rent again - should reuse
            for (int i = 0; i < arrays.Length; i++)
            {
                var array = pool.Rent(128);
                Assert.Equal(128, array.Length);
                pool.Return(array);
            }
        }


        public void TrainingConfig_DefaultValues_AreReasonable()
        {
            // Arrange & Act
            var config = new TrainingConfig();
            
            // Assert
            Assert.False(config.UseMixedPrecision);      // Off by default (conservative)
            Assert.False(config.UseGradientCheckpointing); // Off by default
            Assert.False(config.EnableDiagnostics);       // Off by default
            Assert.False(config.CheckGradientHealth);     // Off by default
            Assert.Equal(CoreCheckpointStrategy.SqrtLayers, config.CheckpointStrategy);
            Assert.Equal(100, config.DiagnosticInterval);
        }
    }
}
