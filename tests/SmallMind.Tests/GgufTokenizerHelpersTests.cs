using SmallMind.Tokenizers.Gguf;

namespace SmallMind.Tests
{
    /// <summary>
    /// Tests for GgufTokenizerHelpers utility class.
    /// Verifies byte token detection and parsing for GGUF tokenizers.
    /// </summary>
    public class GgufTokenizerHelpersTests
    {
        [Theory]
        [InlineData("<0x00>", true, 0x00)]
        [InlineData("<0x20>", true, 0x20)]  // Space character
        [InlineData("<0xFF>", true, 0xFF)]  // Max byte value
        [InlineData("<0x0A>", true, 0x0A)]  // Newline
        [InlineData("<0x41>", true, 0x41)]  // 'A'
        public void IsByteToken_ValidByteTokens_ReturnsTrueWithCorrectValue(string tokenStr, bool expectedResult, byte expectedValue)
        {
            // Act
            bool result = GgufTokenizerHelpers.IsByteToken(tokenStr, out byte byteValue);

            // Assert
            Assert.Equal(expectedResult, result);
            Assert.Equal(expectedValue, byteValue);
        }

        [Theory]
        [InlineData("hello")]              // Regular text
        [InlineData("<0x>")]                // Missing hex digits
        [InlineData("<0x1>")]               // Only one hex digit
        [InlineData("<0x123>")]             // Too many hex digits
        [InlineData("0x20")]                // Missing angle brackets
        [InlineData("<0x20")]               // Missing closing bracket
        [InlineData("0x20>")]               // Missing opening bracket
        [InlineData("<0xGG>")]              // Invalid hex characters
        [InlineData("<0x100>")]             // Value too large for byte
        [InlineData("")]                    // Empty string
        public void IsByteToken_InvalidTokens_ReturnsFalse(string tokenStr)
        {
            // Act
            bool result = GgufTokenizerHelpers.IsByteToken(tokenStr, out byte byteValue);

            // Assert
            Assert.False(result);
            Assert.Equal(0, byteValue);
        }

        [Fact]
        public void IsByteToken_LowercaseHex_ParsesCorrectly()
        {
            // Arrange
            string tokenStr = "<0xff>";

            // Act
            bool result = GgufTokenizerHelpers.IsByteToken(tokenStr, out byte byteValue);

            // Assert
            Assert.True(result);
            Assert.Equal(0xFF, byteValue);
        }

        [Fact]
        public void IsByteToken_MixedCaseHex_ParsesCorrectly()
        {
            // Arrange
            string tokenStr = "<0xAb>";

            // Act
            bool result = GgufTokenizerHelpers.IsByteToken(tokenStr, out byte byteValue);

            // Assert
            Assert.True(result);
            Assert.Equal(0xAB, byteValue);
        }

        [Theory]
        [InlineData("<0x00>")]  // Null
        [InlineData("<0x01>")]
        [InlineData("<0x7F>")]  // DEL
        [InlineData("<0x80>")]  // Extended ASCII start
        [InlineData("<0xFE>")]
        [InlineData("<0xFF>")]  // Max value
        public void IsByteToken_AllValidByteRanges_ParsesCorrectly(string tokenStr)
        {
            // Act
            bool result = GgufTokenizerHelpers.IsByteToken(tokenStr, out byte byteValue);

            // Assert
            Assert.True(result);
            Assert.InRange(byteValue, 0, 255);
        }

        [Fact]
        public void IsByteToken_CommonWhitespaceTokens_ParsesCorrectly()
        {
            // Arrange
            var testCases = new[]
            {
                ("<0x20>", (byte)0x20),  // Space
                ("<0x09>", (byte)0x09),  // Tab
                ("<0x0A>", (byte)0x0A),  // Line feed
                ("<0x0D>", (byte)0x0D)   // Carriage return
            };

            foreach (var (tokenStr, expectedByte) in testCases)
            {
                // Act
                bool result = GgufTokenizerHelpers.IsByteToken(tokenStr, out byte byteValue);

                // Assert
                Assert.True(result, $"Failed to parse {tokenStr}");
                Assert.Equal(expectedByte, byteValue);
            }
        }

        [Fact]
        public void ByteTokenLength_IsCorrect()
        {
            // Assert
            Assert.Equal(6, GgufTokenizerHelpers.ByteTokenLength);
        }

        [Theory]
        [InlineData("<0x20>", 6)]
        [InlineData("<0xFF>", 6)]
        [InlineData("<0x00>", 6)]
        public void IsByteToken_ValidTokensHaveCorrectLength(string tokenStr, int expectedLength)
        {
            // Assert
            Assert.Equal(expectedLength, tokenStr.Length);
            Assert.Equal(GgufTokenizerHelpers.ByteTokenLength, tokenStr.Length);

            // Act
            bool result = GgufTokenizerHelpers.IsByteToken(tokenStr, out _);

            // Assert
            Assert.True(result);
        }
    }
}
