using System.Collections.Generic;
using Xunit;
using SmallMind.Core.Utilities;

namespace SmallMind.Tests.Utilities
{
    /// <summary>
    /// Tests for GgufMetadataHelpers utility class.
    /// Verifies correct extraction of metadata values from GGUF model metadata.
    /// </summary>
    public class GgufMetadataHelpersTests
    {
        [Fact]
        public void ExtractMetadataInt_KeyExists_ReturnsValue()
        {
            // Arrange
            var metadata = new Dictionary<string, object>
            {
                { "vocab_size", 50257 }
            };

            // Act
            int result = GgufMetadataHelpers.ExtractMetadataInt(metadata, "vocab_size", 32000);

            // Assert
            Assert.Equal(50257, result);
        }

        [Fact]
        public void ExtractMetadataInt_KeyDoesNotExist_ReturnsDefault()
        {
            // Arrange
            var metadata = new Dictionary<string, object>
            {
                { "other_key", 123 }
            };

            // Act
            int result = GgufMetadataHelpers.ExtractMetadataInt(metadata, "vocab_size", 32000);

            // Assert
            Assert.Equal(32000, result);
        }

        [Fact]
        public void ExtractMetadataInt_NullMetadata_ReturnsDefault()
        {
            // Act
            int result = GgufMetadataHelpers.ExtractMetadataInt(null, "vocab_size", 32000);

            // Assert
            Assert.Equal(32000, result);
        }

        [Fact]
        public void ExtractMetadataInt_JsonElementNumber_ExtractsCorrectly()
        {
            // Arrange
            var jsonElement = System.Text.Json.JsonDocument.Parse("{\"value\":12345}").RootElement.GetProperty("value");
            var metadata = new Dictionary<string, object>
            {
                { "test_value", jsonElement }
            };

            // Act
            int result = GgufMetadataHelpers.ExtractMetadataInt(metadata, "test_value", 0);

            // Assert
            Assert.Equal(12345, result);
        }

        [Fact]
        public void ExtractMetadataInt_JsonElementString_ParsesCorrectly()
        {
            // Arrange
            var jsonElement = System.Text.Json.JsonDocument.Parse("{\"value\":\"768\"}").RootElement.GetProperty("value");
            var metadata = new Dictionary<string, object>
            {
                { "embed_dim", jsonElement }
            };

            // Act
            int result = GgufMetadataHelpers.ExtractMetadataInt(metadata, "embed_dim", 512);

            // Assert
            Assert.Equal(768, result);
        }

        [Fact]
        public void ExtractMetadataInt_JsonElementInvalidString_ReturnsDefault()
        {
            // Arrange
            var jsonElement = System.Text.Json.JsonDocument.Parse("{\"value\":\"not_a_number\"}").RootElement.GetProperty("value");
            var metadata = new Dictionary<string, object>
            {
                { "test_value", jsonElement }
            };

            // Act
            int result = GgufMetadataHelpers.ExtractMetadataInt(metadata, "test_value", 999);

            // Assert
            Assert.Equal(999, result);
        }

        [Theory]
        [InlineData(100, 100)]
        [InlineData(1024, 1024)]
        [InlineData(50257, 50257)]
        public void ExtractMetadataInt_ConvertibleTypes_ConvertsCorrectly(object value, int expected)
        {
            // Arrange
            var metadata = new Dictionary<string, object>
            {
                { "test_key", value }
            };

            // Act
            int result = GgufMetadataHelpers.ExtractMetadataInt(metadata, "test_key", 0);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void ExtractMetadataInt_LongValue_ConvertsToInt()
        {
            // Arrange
            var metadata = new Dictionary<string, object>
            {
                { "large_value", 2147483647L }  // Max int value as long
            };

            // Act
            int result = GgufMetadataHelpers.ExtractMetadataInt(metadata, "large_value", 0);

            // Assert
            Assert.Equal(2147483647, result);
        }

        [Fact]
        public void ExtractMetadataInt_RealWorldScenario_ExtractsVocabSize()
        {
            // Arrange: Simulates real GGUF metadata
            var metadata = new Dictionary<string, object>
            {
                { "llama.vocab_size", 32000 },
                { "llama.embedding_length", 4096 },
                { "llama.block_count", 32 },
                { "llama.attention.head_count", 32 }
            };

            // Act
            int vocabSize = GgufMetadataHelpers.ExtractMetadataInt(metadata, "llama.vocab_size", 50257);

            // Assert
            Assert.Equal(32000, vocabSize);
        }

        [Fact]
        public void ExtractMetadataInt_MultipleKeys_FallbackWorks()
        {
            // Arrange
            var metadata = new Dictionary<string, object>
            {
                { "vocab_size", 50257 }
            };

            // Act: Try llama.vocab_size first (doesn't exist), then vocab_size
            int result1 = GgufMetadataHelpers.ExtractMetadataInt(metadata, "llama.vocab_size", -1);
            int result2 = GgufMetadataHelpers.ExtractMetadataInt(metadata, "vocab_size", result1);

            // Assert
            Assert.Equal(-1, result1); // First key not found, returns default
            Assert.Equal(50257, result2); // Second key found
        }
    }
}
