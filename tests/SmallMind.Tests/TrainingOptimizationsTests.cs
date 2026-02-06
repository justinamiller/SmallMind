using System;
using Xunit;
using SmallMind.Core;
using CoreMatrixOps = SmallMind.Core.MatrixOps;
using TrainingProfiler = SmallMind.Core.TrainingProfiler;
using MemoryTracker = SmallMind.Core.MemoryTracker;
using GradientDiagnostics = SmallMind.Core.GradientDiagnostics;
using CoreTensorPool = SmallMind.Core.Core.TensorPool;

namespace SmallMind.Tests
{
    public class TrainingOptimizationsTests
    {
        [Fact]
        public void MatrixOps_MatMulTransposeB_ComputesCorrectly()
        {
            // Arrange - Simple 2x3 @ 2x3^T = 2x2 multiplication
            // A = [1 2 3]    B = [1 2 3]^T = [1 4]
            //     [4 5 6]        [4 5 6]     [2 5]
            //                                [3 6]
            // Result should be: [14 32]
            //                   [32 77]
            float[] A = { 1, 2, 3, 4, 5, 6 }; // 2x3
            float[] B = { 1, 2, 3, 4, 5, 6 }; // 2x3 (to be transposed to 3x2)
            float[] C = new float[4]; // 2x2
            
            // Act
            MatrixOps.MatMulTransposeB(A, B, C, M: 2, K: 3, N: 2);
            
            // Assert
            Assert.Equal(14, C[0], precision: 5); // A[0,:] · B[0,:] = 1*1 + 2*2 + 3*3
            Assert.Equal(32, C[1], precision: 5); // A[0,:] · B[1,:] = 1*4 + 2*5 + 3*6
            Assert.Equal(32, C[2], precision: 5); // A[1,:] · B[0,:] = 4*1 + 5*2 + 6*3
            Assert.Equal(77, C[3], precision: 5); // A[1,:] · B[1,:] = 4*4 + 5*5 + 6*6
        }
        
        [Fact]
        public void MatrixOps_MatMulTransposeA_ComputesCorrectly()
        {
            // Arrange - Simple 3x2^T @ 3x2 = 2x2 multiplication
            // A = [1 2]^T = [1 3 5]    B = [1 2]
            //     [3 4]     [2 4 6]        [3 4]
            //     [5 6]                    [5 6]
            // Result should be: [35  44]
            //                   [44  56]
            float[] A = { 1, 2, 3, 4, 5, 6 }; // 3x2 (to be transposed to 2x3)
            float[] B = { 1, 2, 3, 4, 5, 6 }; // 3x2
            float[] C = new float[4]; // 2x2
            
            // Act
            MatrixOps.MatMulTransposeA(A, B, C, M: 2, K: 3, N: 2);
            
            // Assert
            Assert.Equal(35, C[0], precision: 5); // A^T[0,:] · B[:,0] = 1*1 + 3*3 + 5*5
            Assert.Equal(44, C[1], precision: 5); // A^T[0,:] · B[:,1] = 1*2 + 3*4 + 5*6
            Assert.Equal(44, C[2], precision: 5); // A^T[1,:] · B[:,0] = 2*1 + 4*3 + 6*5
            Assert.Equal(56, C[3], precision: 5); // A^T[1,:] · B[:,1] = 2*2 + 4*4 + 6*6
        }
        
        [Fact]
        public void MatrixOps_GELUDerivative_ReturnsReasonableValues()
        {
            // Arrange & Act
            float deriv0 = MatrixOps.GELUDerivative(0.0f);
            float deriv1 = MatrixOps.GELUDerivative(1.0f);
            float derivNeg1 = MatrixOps.GELUDerivative(-1.0f);
            
            // Assert - GELU derivative should be positive and bounded
            Assert.True(deriv0 > 0 && deriv0 < 1);
            Assert.True(deriv1 > 0 && deriv1 < 2);
            Assert.True(derivNeg1 > -1 && derivNeg1 < 1);
        }
        
        [Fact]
        public void TensorPool_RentAndReturn_ReusesArrays()
        {
            // Arrange
            var pool = new CoreTensorPool();
            
            // Act
            var array1 = pool.Rent(100);
            int length1 = array1.Length;
            pool.Return(array1);
            
            var array2 = pool.Rent(100);
            
            // Assert
            Assert.True(length1 >= 100); // Should be at least requested size
            Assert.Same(array1, array2); // Should be the same array (reused)
        }
        
        [Fact]
        public void TensorPool_Rent_AllocatesCorrectBucketSize()
        {
            // Arrange
            var pool = new CoreTensorPool();
            
            // Act
            var array = pool.Rent(100);
            
            // Assert - Should allocate bucket size (128) not exact size
            Assert.Equal(128, array.Length);
        }
        
        [Fact]
        public void TensorPool_Return_ClearsArray()
        {
            // Arrange
            var pool = new CoreTensorPool();
            var array = pool.Rent(64);
            array[0] = 42.0f;
            array[10] = 99.0f;
            
            // Act
            pool.Return(array, clearArray: true);
            
            // Assert
            Assert.Equal(0.0f, array[0]);
            Assert.Equal(0.0f, array[10]);
        }
        
        [Fact]
        public void TrainingProfiler_TracksOperations()
        {
            // Arrange
            var profiler = new TrainingProfiler();
            
            // Act
            using (profiler.Profile("TestOp1"))
            {
                System.Threading.Thread.Sleep(10); // Simulate work
            }
            
            using (profiler.Profile("TestOp2", bytes: 1000))
            {
                System.Threading.Thread.Sleep(5);
            }
            
            // Assert - Just check it doesn't throw and captures something
            profiler.PrintReport(); // Visual check in output
            profiler.Clear();
        }
        
        [Fact]
        public void MemoryTracker_TracksMemorySnapshots()
        {
            // Arrange
            var tracker = new MemoryTracker();
            
            // Act
            tracker.Snapshot("Start");
            
            // Allocate some memory
            var dummy = new float[10000];
            
            tracker.Snapshot("AfterAllocation");
            
            // Assert - Just check it doesn't throw
            tracker.PrintReport(); // Visual check in output
            tracker.Clear();
        }
        
        [Fact]
        public void GradientDiagnostics_DetectsNaN()
        {
            // Arrange
            float[] gradients = { 1.0f, float.NaN, 3.0f };
            
            // Act & Assert - Should not throw
            GradientDiagnostics.CheckGradients("TestGrads", gradients, verbose: true);
            
            var (norm, hasIssue) = GradientDiagnostics.GetGradientNorm(gradients);
            Assert.True(hasIssue);
        }
        
        [Fact]
        public void GradientDiagnostics_DetectsExplodingGradients()
        {
            // Arrange
            float[] gradients = new float[100];
            for (int i = 0; i < gradients.Length; i++)
            {
                gradients[i] = 50.0f; // Large gradients
            }
            
            // Act
            var (norm, hasIssue) = GradientDiagnostics.GetGradientNorm(gradients);
            
            // Assert
            Assert.True(norm > 100); // Should detect as exploding
            Assert.True(hasIssue);
        }
        
        [Fact]
        public void GradientDiagnostics_DetectsVanishingGradients()
        {
            // Arrange
            float[] gradients = new float[100];
            for (int i = 0; i < gradients.Length; i++)
            {
                gradients[i] = 1e-9f; // Very small gradients
            }
            
            // Act
            var (norm, hasIssue) = GradientDiagnostics.GetGradientNorm(gradients);
            
            // Assert
            Assert.True(norm < 1e-7); // Should detect as vanishing
            Assert.True(hasIssue);
        }
        
        [Fact]
        public void GradientDiagnostics_HealthyGradients_NoIssue()
        {
            // Arrange
            float[] gradients = { 0.01f, -0.02f, 0.015f, -0.005f };
            
            // Act
            var (norm, hasIssue) = GradientDiagnostics.GetGradientNorm(gradients);
            
            // Assert
            Assert.False(hasIssue);
            Assert.True(norm > 0 && norm < 100);
        }
    }
}
