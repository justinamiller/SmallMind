using Xunit;
using SmallMind.Core.Utilities;

namespace SmallMind.Tests.Utilities
{
    /// <summary>
    /// Tests for ByteSizeFormatter utility class.
    /// </summary>
    public class ByteSizeFormatterTests
    {
        [Theory]
        [InlineData(0L, "0 B")]
        [InlineData(1L, "1 B")]
        [InlineData(512L, "512 B")]
        [InlineData(1023L, "1023 B")]
        public void FormatBytes_Bytes_ReturnsCorrectFormat(long bytes, string expected)
        {
            // Act
            string result = ByteSizeFormatter.FormatBytes(bytes);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData(1024L, "1.00 KB")]
        [InlineData(2048L, "2.00 KB")]
        [InlineData(1536L, "1.50 KB")]
        [InlineData(1048575L, "1024.00 KB")]
        public void FormatBytes_Kilobytes_ReturnsCorrectFormat(long bytes, string expected)
        {
            // Act
            string result = ByteSizeFormatter.FormatBytes(bytes);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData(1_048_576L, "1.00 MB")]
        [InlineData(2_097_152L, "2.00 MB")]
        [InlineData(5_242_880L, "5.00 MB")]
        [InlineData(1_073_741_823L, "1024.00 MB")]
        public void FormatBytes_Megabytes_ReturnsCorrectFormat(long bytes, string expected)
        {
            // Act
            string result = ByteSizeFormatter.FormatBytes(bytes);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData(1_073_741_824L, "1.00 GB")]
        [InlineData(2_147_483_648L, "2.00 GB")]
        [InlineData(4_294_967_296L, "4.00 GB")]
        [InlineData(10_737_418_240L, "10.00 GB")]
        [InlineData(1_099_511_627_775L, "1024.00 GB")]
        public void FormatBytes_Gigabytes_ReturnsCorrectFormat(long bytes, string expected)
        {
            // Act
            string result = ByteSizeFormatter.FormatBytes(bytes);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData(1_099_511_627_776L, "1.00 TB")]
        [InlineData(2_199_023_255_552L, "2.00 TB")]
        [InlineData(5_497_558_138_880L, "5.00 TB")]
        public void FormatBytes_Terabytes_ReturnsCorrectFormat(long bytes, string expected)
        {
            // Act
            string result = ByteSizeFormatter.FormatBytes(bytes);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void FormatBytes_RealWorldExample_1BParameterModel_FP32()
        {
            // Arrange: 1 billion parameters × 4 bytes each = 4 GB
            long bytes = 4_000_000_000L;

            // Act
            string result = ByteSizeFormatter.FormatBytes(bytes);

            // Assert
            Assert.Equal("3.73 GB", result);
        }

        [Fact]
        public void FormatBytes_RealWorldExample_7BParameterModel_Q4()
        {
            // Arrange: 7 billion parameters × 0.5 bytes each (Q4) = 3.5 GB
            long bytes = 3_500_000_000L;

            // Act
            string result = ByteSizeFormatter.FormatBytes(bytes);

            // Assert
            Assert.Equal("3.26 GB", result);
        }

        [Theory]
        [InlineData(0.0, "0 B")]
        [InlineData(1024.0, "1.00 KB")]
        [InlineData(1048576.0, "1.00 MB")]
        [InlineData(1073741824.0, "1.00 GB")]
        public void FormatBytes_DoubleOverload_ReturnsCorrectFormat(double bytes, string expected)
        {
            // Act
            string result = ByteSizeFormatter.FormatBytes(bytes);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void FormatBytes_DoubleOverload_TruncatesFractionalBytes()
        {
            // Arrange: 1.5 KB worth of bytes
            double bytes = 1536.75;

            // Act
            string result = ByteSizeFormatter.FormatBytes(bytes);

            // Assert: Should truncate to 1536 bytes = 1.50 KB
            Assert.Equal("1.50 KB", result);
        }

        [Fact]
        public void FormatBytes_BoundaryCase_ExactlyOneKB()
        {
            // Arrange
            long bytes = 1024L;

            // Act
            string result = ByteSizeFormatter.FormatBytes(bytes);

            // Assert
            Assert.Equal("1.00 KB", result);
        }

        [Fact]
        public void FormatBytes_BoundaryCase_ExactlyOneMB()
        {
            // Arrange
            long bytes = 1_048_576L;

            // Act
            string result = ByteSizeFormatter.FormatBytes(bytes);

            // Assert
            Assert.Equal("1.00 MB", result);
        }

        [Fact]
        public void FormatBytes_BoundaryCase_ExactlyOneGB()
        {
            // Arrange
            long bytes = 1_073_741_824L;

            // Act
            string result = ByteSizeFormatter.FormatBytes(bytes);

            // Assert
            Assert.Equal("1.00 GB", result);
        }

        [Fact]
        public void FormatBytes_BoundaryCase_ExactlyOneTB()
        {
            // Arrange
            long bytes = 1_099_511_627_776L;

            // Act
            string result = ByteSizeFormatter.FormatBytes(bytes);

            // Assert
            Assert.Equal("1.00 TB", result);
        }
    }
}
