using System;
using System.IO;
using Xunit;

namespace SmallMind.ModelRegistry.Tests
{
    /// <summary>
    /// Tests for FileHashUtility.
    /// </summary>
    public class FileHashUtilityTests
    {
        [Fact]
        public void ComputeSha256_WithKnownContent_ReturnsCorrectHash()
        {
            // Arrange
            string tempFile = Path.GetTempFileName();
            try
            {
                File.WriteAllText(tempFile, "hello world");
                string expectedHash = "b94d27b9934d3e08a52e52d7da7dabfac484efe37a5380ee9088f7ace2efcde9";

                // Act
                string actualHash = FileHashUtility.ComputeSha256(tempFile);

                // Assert
                Assert.Equal(expectedHash, actualHash);
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

        [Fact]
        public void VerifySha256_WithMatchingHash_ReturnsTrue()
        {
            // Arrange
            string tempFile = Path.GetTempFileName();
            try
            {
                File.WriteAllText(tempFile, "test content");
                string expectedHash = FileHashUtility.ComputeSha256(tempFile);

                // Act
                bool result = FileHashUtility.VerifySha256(tempFile, expectedHash);

                // Assert
                Assert.True(result);
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

        [Fact]
        public void VerifySha256_WithNonMatchingHash_ReturnsFalse()
        {
            // Arrange
            string tempFile = Path.GetTempFileName();
            try
            {
                File.WriteAllText(tempFile, "test content");
                string wrongHash = "0000000000000000000000000000000000000000000000000000000000000000";

                // Act
                bool result = FileHashUtility.VerifySha256(tempFile, wrongHash);

                // Assert
                Assert.False(result);
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

        [Fact]
        public void VerifySha256_IsCaseInsensitive()
        {
            // Arrange
            string tempFile = Path.GetTempFileName();
            try
            {
                File.WriteAllText(tempFile, "test");
                string lowerHash = FileHashUtility.ComputeSha256(tempFile);
                string upperHash = lowerHash.ToUpperInvariant();

                // Act
                bool resultLower = FileHashUtility.VerifySha256(tempFile, lowerHash);
                bool resultUpper = FileHashUtility.VerifySha256(tempFile, upperHash);

                // Assert
                Assert.True(resultLower);
                Assert.True(resultUpper);
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }
    }
}
