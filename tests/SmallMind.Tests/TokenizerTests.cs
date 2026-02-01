using System;
using System.Collections.Generic;
using System.IO;
using Xunit;
using SmallMind.Text;

namespace SmallMind.Tests
{
    public class TokenizerTests
    {
        private const string SampleText = "the quick brown fox jumps over the lazy dog.";
        
        [Fact]
        public void CharTokenizer_EncodeDecodeRoundTrip_PreservesText()
        {
            // Arrange
            var tokenizer = new CharTokenizer(SampleText);
            
            // Act
            var tokens = tokenizer.Encode(SampleText);
            var decoded = tokenizer.Decode(tokens);
            
            // Assert
            Assert.Equal(SampleText, decoded);
        }
        
        [Fact]
        public void CharTokenizer_VocabSize_MatchesUniqueCharacters()
        {
            // Arrange
            var text = "aabbccdd";
            var tokenizer = new CharTokenizer(text);
            
            // Act & Assert
            Assert.Equal(4, tokenizer.VocabSize); // a, b, c, d
        }
        
        [Fact]
        public void CharTokenizer_UnknownCharacter_IsSkipped()
        {
            // Arrange
            var tokenizer = new CharTokenizer("abc");
            
            // Act
            var tokens = tokenizer.Encode("abcxyz"); // x, y, z are unknown
            var decoded = tokenizer.Decode(tokens);
            
            // Assert
            Assert.Equal("abc", decoded); // Unknown chars are skipped
        }
        
        [Fact]
        public void Tokenizer_BackwardsCompatibility_WorksAsCharTokenizer()
        {
            // Arrange
            var tokenizer = new Tokenizer(SampleText);
            
            // Act
            var tokens = tokenizer.Encode(SampleText);
            var decoded = tokenizer.Decode(tokens);
            
            // Assert
            Assert.Equal(SampleText, decoded);
            Assert.IsAssignableFrom<CharTokenizer>(tokenizer);
        }
        
        [Fact]
        public void BpeTokenizer_LoadsFromValidAssets()
        {
            // Arrange
            string assetsPath = Path.Combine("assets", "tokenizers", "default");
            
            // Skip test if assets don't exist
            if (!Directory.Exists(assetsPath))
            {
                return;
            }
            
            // Act
            var tokenizer = new BpeTokenizer(assetsPath);
            
            // Assert
            Assert.True(tokenizer.VocabSize > 0);
        }
        
        [Fact]
        public void BpeTokenizer_EncodeDecodeRoundTrip_PreservesSimpleText()
        {
            // Arrange
            string assetsPath = Path.Combine("assets", "tokenizers", "default");
            
            // Skip test if assets don't exist
            if (!Directory.Exists(assetsPath))
            {
                return;
            }
            
            var tokenizer = new BpeTokenizer(assetsPath);
            string testText = "the cat and the hat";
            
            // Act
            var tokens = tokenizer.Encode(testText);
            var decoded = tokenizer.Decode(tokens);
            
            // Assert
            Assert.Equal(testText, decoded);
        }
        
        [Fact]
        public void BpeTokenizer_MissingDirectory_ThrowsException()
        {
            // Arrange
            string nonExistentPath = "/nonexistent/path/to/tokenizer";
            
            // Act & Assert
            var ex = Assert.Throws<TokenizationException>(() => new BpeTokenizer(nonExistentPath));
            Assert.Contains("not found", ex.Message);
        }
        
        [Fact]
        public void BpeTokenizer_MissingVocabFile_ThrowsException()
        {
            // Arrange
            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);
            
            try
            {
                // Create only merges.txt, not vocab.json
                File.WriteAllText(Path.Combine(tempDir, "merges.txt"), "a b\n");
                
                // Act & Assert
                var ex = Assert.Throws<TokenizationException>(() => new BpeTokenizer(tempDir));
                Assert.Contains("vocab.json", ex.Message);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }
        
        [Fact]
        public void TokenizerFactory_CharMode_CreatesCharTokenizer()
        {
            // Arrange
            var options = new TokenizerOptions { Mode = TokenizerMode.Char };
            
            // Act
            var tokenizer = TokenizerFactory.Create(options, SampleText);
            
            // Assert
            Assert.IsType<CharTokenizer>(tokenizer);
        }
        
        [Fact]
        public void TokenizerFactory_BpeMode_WithValidAssets_CreatesBpeTokenizer()
        {
            // Arrange
            string assetsPath = Path.Combine("assets", "tokenizers", "default");
            
            // Skip test if assets don't exist
            if (!Directory.Exists(assetsPath))
            {
                return;
            }
            
            var options = new TokenizerOptions 
            { 
                Mode = TokenizerMode.Bpe,
                TokenizerName = "default"
            };
            
            // Act
            var tokenizer = TokenizerFactory.Create(options);
            
            // Assert
            Assert.IsType<BpeTokenizer>(tokenizer);
        }
        
        [Fact]
        public void TokenizerFactory_BpeMode_MissingAssets_Strict_ThrowsException()
        {
            // Arrange
            var options = new TokenizerOptions 
            { 
                Mode = TokenizerMode.Bpe,
                TokenizerName = "nonexistent",
                Strict = true
            };
            
            // Act & Assert
            var ex = Assert.Throws<TokenizationException>(() => TokenizerFactory.Create(options));
            Assert.Contains("not found", ex.Message);
        }
        
        [Fact]
        public void TokenizerFactory_BpeMode_MissingAssets_NonStrict_FallsBackToChar()
        {
            // Arrange
            var options = new TokenizerOptions 
            { 
                Mode = TokenizerMode.Bpe,
                TokenizerName = "nonexistent",
                Strict = false
            };
            
            // Act
            var tokenizer = TokenizerFactory.Create(options, SampleText);
            
            // Assert
            Assert.IsType<CharTokenizer>(tokenizer);
        }
        
        [Fact]
        public void TokenizerFactory_AutoMode_WithBpeAssets_CreatesBpeTokenizer()
        {
            // Arrange
            string assetsPath = Path.Combine("assets", "tokenizers", "default");
            
            // Skip test if assets don't exist
            if (!Directory.Exists(assetsPath))
            {
                return;
            }
            
            var options = new TokenizerOptions 
            { 
                Mode = TokenizerMode.Auto,
                TokenizerName = "default"
            };
            
            // Act
            var tokenizer = TokenizerFactory.Create(options, SampleText);
            
            // Assert
            Assert.IsType<BpeTokenizer>(tokenizer);
        }
        
        [Fact]
        public void TokenizerFactory_AutoMode_WithoutBpeAssets_FallsBackToChar()
        {
            // Arrange
            var options = new TokenizerOptions 
            { 
                Mode = TokenizerMode.Auto,
                TokenizerName = "nonexistent"
            };
            
            // Act
            var tokenizer = TokenizerFactory.Create(options, SampleText);
            
            // Assert
            Assert.IsType<CharTokenizer>(tokenizer);
        }
        
        [Fact]
        public void TokenizerFactory_ExplicitPath_UsesProvidedPath()
        {
            // Arrange
            string assetsPath = Path.GetFullPath(Path.Combine("assets", "tokenizers", "default"));
            
            // Skip test if assets don't exist
            if (!Directory.Exists(assetsPath))
            {
                return;
            }
            
            var options = new TokenizerOptions 
            { 
                Mode = TokenizerMode.Auto,
                TokenizerPath = assetsPath
            };
            
            // Act
            var tokenizer = TokenizerFactory.Create(options);
            
            // Assert
            Assert.IsType<BpeTokenizer>(tokenizer);
        }
        
        [Fact]
        public void Tokenizer_DeterministicEncoding_SameInputProducesSameTokens()
        {
            // Arrange
            var tokenizer = new CharTokenizer(SampleText);
            
            // Act
            var tokens1 = tokenizer.Encode("test text");
            var tokens2 = tokenizer.Encode("test text");
            
            // Assert
            Assert.Equal(tokens1, tokens2);
        }
        
        [Fact]
        public void BpeTokenizer_DeterministicEncoding_SameInputProducesSameTokens()
        {
            // Arrange
            string assetsPath = Path.Combine("assets", "tokenizers", "default");
            
            // Skip test if assets don't exist
            if (!Directory.Exists(assetsPath))
            {
                return;
            }
            
            var tokenizer = new BpeTokenizer(assetsPath);
            string testText = "the cat sat on the mat";
            
            // Act
            var tokens1 = tokenizer.Encode(testText);
            var tokens2 = tokenizer.Encode(testText);
            
            // Assert
            Assert.Equal(tokens1, tokens2);
        }
        
        [Fact]
        public void TokenizerFactory_NullOptions_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => TokenizerFactory.Create(null!));
        }
        
        [Fact]
        public void CharTokenizer_EmptyText_ReturnsEmptyTokenList()
        {
            // Arrange
            var tokenizer = new CharTokenizer("abc");
            
            // Act
            var tokens = tokenizer.Encode("");
            
            // Assert
            Assert.Empty(tokens);
        }
        
        [Fact]
        public void BpeTokenizer_EmptyText_ReturnsEmptyTokenList()
        {
            // Arrange
            string assetsPath = Path.Combine("assets", "tokenizers", "default");
            
            // Skip test if assets don't exist
            if (!Directory.Exists(assetsPath))
            {
                return;
            }
            
            var tokenizer = new BpeTokenizer(assetsPath);
            
            // Act
            var tokens = tokenizer.Encode("");
            
            // Assert
            Assert.Empty(tokens);
        }
    }
}
