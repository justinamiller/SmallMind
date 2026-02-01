using System;
using Xunit;
using SmallMind.Core.Rng;

namespace SmallMind.Tests
{
    public class DeterministicRngTests
    {
        [Fact]
        public void DeterministicRng_SameSeed_ProducesSameSequence()
        {
            // Arrange
            var rng1 = new DeterministicRng(42);
            var rng2 = new DeterministicRng(42);
            
            // Act & Assert - Generate 100 numbers and verify they match
            for (int i = 0; i < 100; i++)
            {
                Assert.Equal(rng1.NextULong(), rng2.NextULong());
            }
        }
        
        [Fact]
        public void DeterministicRng_DifferentSeeds_ProduceDifferentSequences()
        {
            // Arrange
            var rng1 = new DeterministicRng(42);
            var rng2 = new DeterministicRng(43);
            
            // Act
            var value1 = rng1.NextULong();
            var value2 = rng2.NextULong();
            
            // Assert
            Assert.NotEqual(value1, value2);
        }
        
        [Fact]
        public void DeterministicRng_NextDouble_InRange()
        {
            // Arrange
            var rng = new DeterministicRng(123);
            
            // Act & Assert - Generate 1000 doubles and verify range
            for (int i = 0; i < 1000; i++)
            {
                double value = rng.NextDouble();
                Assert.InRange(value, 0.0, 1.0);
                Assert.True(value < 1.0); // Should be [0.0, 1.0)
            }
        }
        
        [Fact]
        public void DeterministicRng_NextFloat_InRange()
        {
            // Arrange
            var rng = new DeterministicRng(456);
            
            // Act & Assert - Generate 1000 floats and verify range
            for (int i = 0; i < 1000; i++)
            {
                float value = rng.NextFloat();
                Assert.InRange(value, 0.0f, 1.0f);
                Assert.True(value < 1.0f); // Should be [0.0, 1.0)
            }
        }
        
        [Fact]
        public void DeterministicRng_Next_InRange()
        {
            // Arrange
            var rng = new DeterministicRng(789);
            int maxValue = 100;
            
            // Act & Assert - Generate 1000 integers and verify range
            for (int i = 0; i < 1000; i++)
            {
                int value = rng.Next(maxValue);
                Assert.InRange(value, 0, maxValue - 1);
            }
        }
        
        [Fact]
        public void DeterministicRng_NextWithRange_InRange()
        {
            // Arrange
            var rng = new DeterministicRng(999);
            int minValue = 50;
            int maxValue = 150;
            
            // Act & Assert - Generate 1000 integers and verify range
            for (int i = 0; i < 1000; i++)
            {
                int value = rng.Next(minValue, maxValue);
                Assert.InRange(value, minValue, maxValue - 1);
            }
        }
        
        [Fact]
        public void DeterministicRng_Next_ThrowsOnNonPositiveMax()
        {
            // Arrange
            var rng = new DeterministicRng(111);
            
            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => rng.Next(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => rng.Next(-10));
        }
        
        [Fact]
        public void DeterministicRng_NextWithRange_ThrowsOnInvalidRange()
        {
            // Arrange
            var rng = new DeterministicRng(222);
            
            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => rng.Next(100, 50)); // max < min
            Assert.Throws<ArgumentOutOfRangeException>(() => rng.Next(50, 50));  // max == min
        }
        
        [Fact]
        public void DeterministicRng_UniformDistribution_NextDouble()
        {
            // Arrange
            var rng = new DeterministicRng(333);
            int sampleCount = 10000;
            int buckets = 10;
            int[] bucketCounts = new int[buckets];
            
            // Act - Generate samples and count distribution
            for (int i = 0; i < sampleCount; i++)
            {
                double value = rng.NextDouble();
                int bucket = (int)(value * buckets);
                if (bucket == buckets) bucket--; // Handle edge case of 1.0 (shouldn't happen)
                bucketCounts[bucket]++;
            }
            
            // Assert - Each bucket should have roughly sampleCount/buckets samples
            // Allow 30% deviation for randomness
            int expectedPerBucket = sampleCount / buckets;
            double tolerance = expectedPerBucket * 0.3;
            
            for (int i = 0; i < buckets; i++)
            {
                Assert.InRange(bucketCounts[i], 
                    (int)(expectedPerBucket - tolerance), 
                    (int)(expectedPerBucket + tolerance));
            }
        }
        
        [Fact]
        public void DeterministicRng_Reproducibility_ComplexSequence()
        {
            // Arrange
            int seed = 12345;
            
            // Act - Generate two identical sequences using different operations
            var results1 = GenerateComplexSequence(seed);
            var results2 = GenerateComplexSequence(seed);
            
            // Assert
            Assert.Equal(results1.Length, results2.Length);
            for (int i = 0; i < results1.Length; i++)
            {
                Assert.Equal(results1[i], results2[i]);
            }
        }
        
        private double[] GenerateComplexSequence(int seed)
        {
            var rng = new DeterministicRng(seed);
            var results = new double[50];
            
            for (int i = 0; i < 50; i++)
            {
                if (i % 3 == 0)
                    results[i] = rng.NextDouble();
                else if (i % 3 == 1)
                    results[i] = rng.NextFloat();
                else
                    results[i] = rng.Next(1000);
            }
            
            return results;
        }
    }
}
