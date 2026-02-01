using System;
using System.Text;
using Xunit;
using SmallMind.Tokenizers;

namespace SmallMind.Tests
{
    public class NewTokenizerTests
    {
        private const string SampleText = "Hello, World! 🌍";
        private const string AsciiText = "The quick brown fox jumps over the lazy dog.";
        
        #region CharTokenizer Tests

        [Fact]
        public void CharTokenizer_SpanBasedEncode_WorksCorrectly()
        {
            // Arrange
            var tokenizer = new CharTokenizer("abc123");
            string text = "abc";
            byte[] utf8 = Encoding.UTF8.GetBytes(text);
            int[] tokens = new int[10];

            // Act
            int count = tokenizer.Encode(utf8, tokens);

            // Assert
            Assert.Equal(3, count);
            Assert.True(count <= tokens.Length);
        }

        [Fact]
        public void CharTokenizer_SpanBasedDecode_WorksCorrectly()
        {
            // Arrange
            var tokenizer = new CharTokenizer("abc123");
            var tokens = tokenizer.Encode("abc");
            byte[] utf8Out = new byte[100];

            // Act
            int byteCount = tokenizer.Decode(tokens.ToArray().AsSpan(), utf8Out);
            string decoded = Encoding.UTF8.GetString(utf8Out, 0, byteCount);

            // Assert
            Assert.Equal("abc", decoded);
        }

        [Fact]
        public void CharTokenizer_RoundTrip_PreservesText()
        {
            // Arrange
            var tokenizer = new CharTokenizer(AsciiText);

            // Act
            var tokens = tokenizer.Encode(AsciiText);
            var decoded = tokenizer.Decode(tokens);

            // Assert
            Assert.Equal(AsciiText, decoded);
        }

        [Fact]
        public void CharTokenizer_Info_HasCorrectMetadata()
        {
            // Arrange & Act
            var tokenizer = new CharTokenizer("abc");

            // Assert
            Assert.Equal("CharTokenizer", tokenizer.Info.Name);
            Assert.Equal(3, tokenizer.Info.VocabSize);
            Assert.False(tokenizer.Info.SupportsByteFallback);
        }

        #endregion

        #region ByteFallbackTokenizer Tests

        [Fact]
        public void ByteFallbackTokenizer_ExtendedVocabSize_IncludesByteTokens()
        {
            // Arrange
            var innerTokenizer = new CharTokenizer("abc");
            var tokenizer = new ByteFallbackTokenizer(innerTokenizer);

            // Assert
            Assert.Equal(innerTokenizer.VocabSize + 256, tokenizer.VocabSize);
        }

        [Fact]
        public void ByteFallbackTokenizer_Info_ReflectsWrapper()
        {
            // Arrange
            var innerTokenizer = new CharTokenizer("abc");
            var tokenizer = new ByteFallbackTokenizer(innerTokenizer);

            // Assert
            Assert.Contains("ByteFallback", tokenizer.Info.Name);
            Assert.True(tokenizer.Info.SupportsByteFallback);
            Assert.Equal(-1, tokenizer.Info.UnkTokenId); // No UNK with byte fallback
        }

        [Fact]
        public void ByteFallbackTokenizer_UnknownCharacters_FallbackToBytes()
        {
            // Arrange
            var innerTokenizer = new CharTokenizer("abc"); // Limited vocab
            var tokenizer = new ByteFallbackTokenizer(innerTokenizer);
            string text = "abcxyz"; // xyz are unknown

            // Act
            var tokens = tokenizer.Encode(text);
            var decoded = tokenizer.Decode(tokens);

            // Assert - should preserve full text including unknown chars
            Assert.Equal(text, decoded);
        }

        [Fact]
        public void ByteFallbackTokenizer_RoundTrip_PreservesUTF8()
        {
            // Arrange
            var innerTokenizer = new CharTokenizer("Hello, World!");
            var tokenizer = new ByteFallbackTokenizer(innerTokenizer);
            string text = "Hello, 世界! 🌍"; // Mix of ASCII, Chinese, emoji

            // Act
            var tokens = tokenizer.Encode(text);
            var decoded = tokenizer.Decode(tokens);

            // Assert
            Assert.Equal(text, decoded);
        }

        #endregion

        #region Determinism Tests

        [Fact]
        public void CharTokenizer_Deterministic_SameInputProducesSameTokens()
        {
            // Arrange
            var tokenizer = new CharTokenizer(AsciiText);

            // Act
            var tokens1 = tokenizer.Encode("test");
            var tokens2 = tokenizer.Encode("test");

            // Assert
            Assert.Equal(tokens1, tokens2);
        }

        [Fact]
        public void ByteFallbackTokenizer_Deterministic_SameInputProducesSameTokens()
        {
            // Arrange
            var innerTokenizer = new CharTokenizer("test123");
            var tokenizer = new ByteFallbackTokenizer(innerTokenizer);
            string text = "test123xyz";

            // Act
            var tokens1 = tokenizer.Encode(text);
            var tokens2 = tokenizer.Encode(text);

            // Assert
            Assert.Equal(tokens1, tokens2);
        }

        #endregion

        #region Edge Cases

        [Fact]
        public void CharTokenizer_EmptyString_ReturnsEmptyTokens()
        {
            // Arrange
            var tokenizer = new CharTokenizer("abc");

            // Act
            var tokens = tokenizer.Encode("");

            // Assert
            Assert.Empty(tokens);
        }

        [Fact]
        public void CharTokenizer_SpanEncode_EmptyInput_ReturnsZero()
        {
            // Arrange
            var tokenizer = new CharTokenizer("abc");
            byte[] empty = Array.Empty<byte>();
            int[] tokens = new int[10];

            // Act
            int count = tokenizer.Encode(empty, tokens);

            // Assert
            Assert.Equal(0, count);
        }

        [Fact]
        public void CharTokenizer_DecodeToString_WorksCorrectly()
        {
            // Arrange
            var tokenizer = new CharTokenizer("abc123");
            var tokens = tokenizer.Encode("abc");

            // Act
            string decoded = tokenizer.DecodeToString(tokens.ToArray().AsSpan());

            // Assert
            Assert.Equal("abc", decoded);
        }

        [Fact]
        public void ByteFallbackTokenizer_EmptyString_ReturnsEmptyTokens()
        {
            // Arrange
            var innerTokenizer = new CharTokenizer("abc");
            var tokenizer = new ByteFallbackTokenizer(innerTokenizer);

            // Act
            var tokens = tokenizer.Encode("");

            // Assert
            Assert.Empty(tokens);
        }

        #endregion

        #region UTF-8 Round-Trip Tests

        [Fact]
        public void CharTokenizer_UTF8Emoji_RoundTrip()
        {
            // Arrange
            string text = "Hello 😀 World 🌍!";
            var tokenizer = new CharTokenizer(text);

            // Act
            var tokens = tokenizer.Encode(text);
            var decoded = tokenizer.Decode(tokens);

            // Assert
            Assert.Equal(text, decoded);
        }

        [Fact]
        public void CharTokenizer_UTF8AccentedChars_RoundTrip()
        {
            // Arrange
            string text = "Café résumé naïve";
            var tokenizer = new CharTokenizer(text);

            // Act
            var tokens = tokenizer.Encode(text);
            var decoded = tokenizer.Decode(tokens);

            // Assert
            Assert.Equal(text, decoded);
        }

        [Fact]
        public void CharTokenizer_UTF8Chinese_RoundTrip()
        {
            // Arrange
            string text = "你好世界";
            var tokenizer = new CharTokenizer(text);

            // Act
            var tokens = tokenizer.Encode(text);
            var decoded = tokenizer.Decode(tokens);

            // Assert
            Assert.Equal(text, decoded);
        }

        [Fact]
        public void ByteFallbackTokenizer_ByteLevelRoundTrip_ExactMatch()
        {
            // Arrange
            var innerTokenizer = new CharTokenizer("ab");
            var tokenizer = new ByteFallbackTokenizer(innerTokenizer);
            byte[] originalBytes = Encoding.UTF8.GetBytes("abcdefg🌍");

            // Act
            int[] tokens = new int[originalBytes.Length * 2];
            int tokenCount = tokenizer.Encode(originalBytes, tokens);
            
            byte[] decodedBytes = new byte[originalBytes.Length * 2];
            int byteCount = tokenizer.Decode(tokens.AsSpan(0, tokenCount), decodedBytes);

            // Assert
            Assert.Equal(originalBytes.Length, byteCount);
            Assert.Equal(originalBytes, decodedBytes.AsSpan(0, byteCount).ToArray());
        }

        #endregion

        #region Configuration Tests

        [Fact]
        public void TokenizerConfig_DefaultValues_AreCorrect()
        {
            // Arrange & Act
            var config = new TokenizerConfig();

            // Assert
            Assert.Equal(TokenizerKind.Char, config.Kind);
            Assert.Null(config.VocabPath);
            Assert.Null(config.MergesPath);
            Assert.Null(config.ModelPath);
        }

        [Fact]
        public void NewTokenizerFactory_CreateCharTokenizer_Success()
        {
            // Arrange
            var config = new TokenizerConfig
            {
                Kind = TokenizerKind.Char,
                TrainingText = "abc123"
            };

            // Act
            var tokenizer = NewTokenizerFactory.Create(config);

            // Assert
            Assert.IsType<CharTokenizer>(tokenizer);
            Assert.Equal(6, tokenizer.VocabSize);
        }

        [Fact]
        public void NewTokenizerFactory_CharTokenizer_NullTrainingText_ThrowsException()
        {
            // Arrange
            var config = new TokenizerConfig
            {
                Kind = TokenizerKind.Char,
                TrainingText = null
            };

            // Act & Assert
            Assert.Throws<TokenizationException>(() => NewTokenizerFactory.Create(config));
        }

        [Fact]
        public void NewTokenizerFactory_NullConfig_ThrowsException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => NewTokenizerFactory.Create(null!));
        }

        #endregion

        #region Special Tokens

        [Fact]
        public void TokenizerInfo_SpecialTokens_CanBeConfigured()
        {
            // Arrange
            var info = new TokenizerInfo(
                name: "Test",
                vocabSize: 100,
                bosTokenId: 1,
                eosTokenId: 2,
                padTokenId: 3,
                unkTokenId: 4,
                supportsByteFallback: true
            );

            // Assert
            Assert.Equal(1, info.BosTokenId);
            Assert.Equal(2, info.EosTokenId);
            Assert.Equal(3, info.PadTokenId);
            Assert.Equal(4, info.UnkTokenId);
            Assert.True(info.SupportsByteFallback);
        }

        #endregion
    }
}
