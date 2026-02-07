using System;
using Xunit;
using SmallMind.Tokenizers;
using SmallMind.Transformers;
using SmallMind.Runtime;

namespace SmallMind.Tests
{
    /// <summary>
    /// Integration tests demonstrating tokenizer usage patterns.
    /// </summary>
    public class TokenizerIntegrationTests
    {
        private const string SampleText = "the quick brown fox jumps over the lazy dog.";

        [Fact]
        public void IntegrationTest_CharTokenizer_WorksEndToEnd()
        {
            // Arrange & Act
            var tokenizer = new CharTokenizer(SampleText);
            var tokens = tokenizer.Encode(SampleText);
            var decoded = tokenizer.Decode(tokens);

            // Assert
            Assert.True(tokenizer.VocabSize > 0);
            Assert.NotEmpty(tokens);
            Assert.Equal(SampleText, decoded);
        }

        [Fact]
        public void IntegrationTest_BpeTokenizer_WorksEndToEnd()
        {
            // Arrange
            string assetsPath = System.IO.Path.Combine("assets", "tokenizers", "default");
            
            // Skip if assets don't exist
            if (!System.IO.Directory.Exists(assetsPath))
            {
                return;
            }

            // Act
            var tokenizer = new BpeTokenizer(assetsPath);
            var tokens = tokenizer.Encode(SampleText);
            var decoded = tokenizer.Decode(tokens);

            // Assert
            Assert.True(tokenizer.VocabSize > 0);
            Assert.NotEmpty(tokens);
            Assert.Equal(SampleText, decoded);
        }

        [Fact]
        public void IntegrationTest_TokenizerFactory_AutoMode_Works()
        {
            // Arrange
            var options = new TokenizerOptions
            {
                Mode = TokenizerMode.Auto,
                TokenizerName = "default"
            };

            // Act
            var tokenizer = TokenizerFactory.Create(options, SampleText);
            var tokens = tokenizer.Encode(SampleText);
            var decoded = tokenizer.Decode(tokens);

            // Assert
            Assert.NotNull(tokenizer);
            Assert.True(tokenizer.VocabSize > 0);
            Assert.Equal(SampleText, decoded);
        }

        [Fact]
        public void IntegrationTest_BackwardsCompatibility_TokenizerClassWorks()
        {
            // Arrange & Act
            var tokenizer = new Tokenizer(SampleText);  // Old API
            var tokens = tokenizer.Encode(SampleText);
            var decoded = tokenizer.Decode(tokens);

            // Assert
            Assert.IsAssignableFrom<CharTokenizer>(tokenizer);
            Assert.Equal(SampleText, decoded);
        }

        [Fact]
        public void IntegrationTest_BpeTokenizer_CompressesBetterThanChar()
        {
            // Arrange
            string assetsPath = System.IO.Path.Combine("assets", "tokenizers", "default");
            
            // Skip if assets don't exist
            if (!System.IO.Directory.Exists(assetsPath))
            {
                return;
            }

            var charTokenizer = new CharTokenizer(SampleText);
            var bpeTokenizer = new BpeTokenizer(assetsPath);

            // Act
            var charTokens = charTokenizer.Encode(SampleText);
            var bpeTokens = bpeTokenizer.Encode(SampleText);

            // Assert - BPE should use fewer tokens
            Assert.True(bpeTokens.Count < charTokens.Count, 
                $"BPE should compress better: BPE={bpeTokens.Count}, Char={charTokens.Count}");
        }

        [Fact]
        public void IntegrationTest_TokenizerFactory_SupportsAllModes()
        {
            // Test Char mode
            var charOptions = new TokenizerOptions { Mode = TokenizerMode.Char };
            var charTokenizer = TokenizerFactory.Create(charOptions, SampleText);
            Assert.IsType<CharTokenizer>(charTokenizer);

            // Test Auto mode (will fall back to Char if BPE assets missing)
            var autoOptions = new TokenizerOptions { Mode = TokenizerMode.Auto };
            var autoTokenizer = TokenizerFactory.Create(autoOptions, SampleText);
            Assert.NotNull(autoTokenizer);

            // Test BPE mode with fallback
            var bpeOptions = new TokenizerOptions 
            { 
                Mode = TokenizerMode.Bpe,
                TokenizerName = "default",
                Strict = false 
            };
            var bpeTokenizer = TokenizerFactory.Create(bpeOptions, SampleText);
            Assert.NotNull(bpeTokenizer);
        }
    }
}
