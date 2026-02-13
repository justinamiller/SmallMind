using SmallMind.Core.Exceptions;
using SmallMind.Runtime.Batching;

namespace SmallMind.Tests.Batching
{
    public class BatchingOptionsTests
    {
        [Fact]
        public void DefaultValues_AreCorrect()
        {
            // Arrange & Act
            var options = new BatchingOptions();

            // Assert
            Assert.False(options.Enabled);
            Assert.Equal(8, options.MaxBatchSize);
            Assert.Equal(10, options.MaxBatchWaitMs);
            Assert.Equal(100, options.MaxTotalQueuedRequests);
            Assert.True(options.PrefillOnly);
        }

        [Fact]
        public void Validate_WithValidOptions_DoesNotThrow()
        {
            // Arrange
            var options = new BatchingOptions
            {
                Enabled = true,
                MaxBatchSize = 16,
                MaxBatchWaitMs = 20,
                MaxTotalQueuedRequests = 200,
                PrefillOnly = false
            };

            // Act & Assert
            options.Validate(); // Should not throw
        }

        [Fact]
        public void Validate_WithZeroMaxBatchSize_Throws()
        {
            // Arrange
            var options = new BatchingOptions { MaxBatchSize = 0 };

            // Act & Assert
            var ex = Assert.Throws<ValidationException>(() => options.Validate());
            Assert.Contains("MaxBatchSize", ex.Message);
        }

        [Fact]
        public void Validate_WithNegativeMaxBatchSize_Throws()
        {
            // Arrange
            var options = new BatchingOptions { MaxBatchSize = -1 };

            // Act & Assert
            var ex = Assert.Throws<ValidationException>(() => options.Validate());
            Assert.Contains("MaxBatchSize", ex.Message);
        }

        [Fact]
        public void Validate_WithNegativeMaxBatchWaitMs_Throws()
        {
            // Arrange
            var options = new BatchingOptions { MaxBatchWaitMs = -1 };

            // Act & Assert
            var ex = Assert.Throws<ValidationException>(() => options.Validate());
            Assert.Contains("MaxBatchWaitMs", ex.Message);
        }

        [Fact]
        public void Validate_WithZeroMaxBatchWaitMs_DoesNotThrow()
        {
            // Arrange
            var options = new BatchingOptions { MaxBatchWaitMs = 0 };

            // Act & Assert
            options.Validate(); // Should not throw
        }

        [Fact]
        public void Validate_WithZeroMaxTotalQueuedRequests_Throws()
        {
            // Arrange
            var options = new BatchingOptions { MaxTotalQueuedRequests = 0 };

            // Act & Assert
            var ex = Assert.Throws<ValidationException>(() => options.Validate());
            Assert.Contains("MaxTotalQueuedRequests", ex.Message);
        }

        [Fact]
        public void Clone_CreatesIndependentCopy()
        {
            // Arrange
            var original = new BatchingOptions
            {
                Enabled = true,
                MaxBatchSize = 32,
                MaxBatchWaitMs = 50,
                MaxTotalQueuedRequests = 500,
                PrefillOnly = false
            };

            // Act
            var clone = original.Clone();
            clone.MaxBatchSize = 64; // Modify clone

            // Assert
            Assert.Equal(32, original.MaxBatchSize); // Original unchanged
            Assert.Equal(64, clone.MaxBatchSize);
            Assert.Equal(original.Enabled, clone.Enabled);
            Assert.Equal(original.MaxBatchWaitMs, clone.MaxBatchWaitMs);
            Assert.Equal(original.MaxTotalQueuedRequests, clone.MaxTotalQueuedRequests);
        }

        [Fact]
        public void Clone_PreservesAllProperties()
        {
            // Arrange
            var original = new BatchingOptions
            {
                Enabled = true,
                MaxBatchSize = 16,
                MaxBatchWaitMs = 25,
                MaxTotalQueuedRequests = 300,
                PrefillOnly = false
            };

            // Act
            var clone = original.Clone();

            // Assert
            Assert.Equal(original.Enabled, clone.Enabled);
            Assert.Equal(original.MaxBatchSize, clone.MaxBatchSize);
            Assert.Equal(original.MaxBatchWaitMs, clone.MaxBatchWaitMs);
            Assert.Equal(original.MaxTotalQueuedRequests, clone.MaxTotalQueuedRequests);
            Assert.Equal(original.PrefillOnly, clone.PrefillOnly);
        }
    }
}
