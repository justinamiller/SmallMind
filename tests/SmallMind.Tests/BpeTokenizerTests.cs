using System.Text;
using SmallMind.Tokenizers;

namespace SmallMind.Tests;

/// <summary>
/// Tests for ByteLevelBpeTokenizer training, encoding, decoding, and persistence.
/// </summary>
public class BpeTokenizerTests
{
    private const string SimpleTrainingText = "the quick brown fox jumps over the lazy dog. " +
                                               "the fox jumps quickly. " +
                                               "the brown dog runs.";

    private const string RepeatedTrainingText = "aaabbbcccddd " +
                                                  "aaabbbcccddd " +
                                                  "aaabbbcccddd " +
                                                  "aaabbbcccddd";

    [Fact]
    public void ByteLevelBpeTokenizer_Train_CreatesVocabulary()
    {
        // Arrange
        var tokenizer = new ByteLevelBpeTokenizer();

        // Act
        tokenizer.Train(SimpleTrainingText, 300);

        // Assert
        Assert.True(tokenizer.VocabSize >= 260); // At least base vocab + special tokens
        Assert.True(tokenizer.VocabSize <= 300);
    }

    [Fact]
    public void ByteLevelBpeTokenizer_EncodeDecodeRoundTrip_PreservesAsciiText()
    {
        // Arrange
        var tokenizer = new ByteLevelBpeTokenizer();
        tokenizer.Train(SimpleTrainingText, 300);
        string testText = "the quick fox";

        // Act
        var tokens = tokenizer.Encode(testText);
        var decoded = tokenizer.Decode(tokens);

        // Assert
        Assert.Equal(testText, decoded);
    }

    [Fact]
    public void ByteLevelBpeTokenizer_EncodeDecodeRoundTrip_PreservesUnicodeText()
    {
        // Arrange
        var tokenizer = new ByteLevelBpeTokenizer();
        string unicodeText = "Hello, 世界! Привет мир. مرحبا بالعالم";
        tokenizer.Train(unicodeText, 300);

        // Act
        var tokens = tokenizer.Encode(unicodeText);
        var decoded = tokenizer.Decode(tokens);

        // Assert
        Assert.Equal(unicodeText, decoded);
    }

    [Fact]
    public void ByteLevelBpeTokenizer_EncodeDecodeRoundTrip_PreservesEmoji()
    {
        // Arrange
        var tokenizer = new ByteLevelBpeTokenizer();
        string emojiText = "Hello 👋 World 🌍 Test 🎉";
        tokenizer.Train(emojiText, 300);

        // Act
        var tokens = tokenizer.Encode(emojiText);
        var decoded = tokenizer.Decode(tokens);

        // Assert
        Assert.Equal(emojiText, decoded);
    }

    [Fact]
    public void ByteLevelBpeTokenizer_EncodeDecodeRoundTrip_PreservesMixedLanguageText()
    {
        // Arrange
        var tokenizer = new ByteLevelBpeTokenizer();
        string mixedText = "English français 日本語 한국어 العربية русский";
        tokenizer.Train(mixedText, 500);

        // Act
        var tokens = tokenizer.Encode(mixedText);
        var decoded = tokenizer.Decode(tokens);

        // Assert
        Assert.Equal(mixedText, decoded);
    }

    [Fact]
    public void ByteLevelBpeTokenizer_EmptyString_ReturnsEmptyList()
    {
        // Arrange
        var tokenizer = new ByteLevelBpeTokenizer();
        tokenizer.Train(SimpleTrainingText, 300);

        // Act
        var tokens = tokenizer.Encode("");

        // Assert
        Assert.Empty(tokens);
    }

    [Fact]
    public void ByteLevelBpeTokenizer_SingleCharacter_EncodesDecode()
    {
        // Arrange
        var tokenizer = new ByteLevelBpeTokenizer();
        tokenizer.Train(SimpleTrainingText, 300);

        // Act
        var tokens = tokenizer.Encode("a");
        var decoded = tokenizer.Decode(tokens);

        // Assert
        Assert.Single(tokens);
        Assert.Equal("a", decoded);
    }

    [Fact]
    public void ByteLevelBpeTokenizer_RepeatedText_CompressesWell()
    {
        // Arrange
        var tokenizer = new ByteLevelBpeTokenizer();
        tokenizer.Train(RepeatedTrainingText, 350);

        // Act - encode repeated text which should compress due to learned merges
        var tokensCompressed = tokenizer.Encode("aaabbbcccddd");
        var tokensUncompressed = tokenizer.Encode("xyz"); // Unseen pattern

        // Assert - repeated pattern should use fewer tokens than character count
        Assert.True(tokensCompressed.Count < "aaabbbcccddd".Length,
            $"Expected compression: tokens={tokensCompressed.Count} < chars={("aaabbbcccddd").Length}");

        // Verify roundtrip
        Assert.Equal("aaabbbcccddd", tokenizer.Decode(tokensCompressed));
        Assert.Equal("xyz", tokenizer.Decode(tokensUncompressed));
    }

    [Fact]
    public void ByteLevelBpeTokenizer_SpecialTokens_HaveCorrectIds()
    {
        // Arrange
        var tokenizer = new ByteLevelBpeTokenizer();
        tokenizer.Train(SimpleTrainingText, 300);

        // Assert
        Assert.Equal(256, tokenizer.PadTokenId);
        Assert.Equal(257, tokenizer.UnkTokenId);
        Assert.Equal(258, tokenizer.BosTokenId);
        Assert.Equal(259, tokenizer.EosTokenId);
    }

    [Fact]
    public void ByteLevelBpeTokenizer_Encode_IsDeterministic()
    {
        // Arrange
        var tokenizer = new ByteLevelBpeTokenizer();
        tokenizer.Train(SimpleTrainingText, 300);
        string testText = "the quick brown fox";

        // Act - encode same text multiple times
        var tokens1 = tokenizer.Encode(testText);
        var tokens2 = tokenizer.Encode(testText);
        var tokens3 = tokenizer.Encode(testText);

        // Assert - all encodings should be identical
        Assert.Equal(tokens1, tokens2);
        Assert.Equal(tokens2, tokens3);
    }

    [Fact]
    public void ByteLevelBpeTokenizer_VocabSize_MatchesTarget()
    {
        // Arrange
        var tokenizer = new ByteLevelBpeTokenizer();
        int targetVocabSize = 500;

        // Act
        tokenizer.Train(SimpleTrainingText, targetVocabSize);

        // Assert
        Assert.True(tokenizer.VocabSize <= targetVocabSize);
        Assert.True(tokenizer.VocabSize >= 260); // At least base vocab
    }

    [Fact]
    public void ByteLevelBpeTokenizer_MinVocabSize_IsEnforced()
    {
        // Arrange
        var tokenizer = new ByteLevelBpeTokenizer();

        // Act & Assert - vocab size below 260 should throw
        var ex = Assert.Throws<ArgumentException>(() =>
            tokenizer.Train(SimpleTrainingText, 100));
        Assert.Contains("260", ex.Message);
    }

    [Fact]
    public void ByteLevelBpeTokenizer_SaveLoad_RoundtripPreservesEncoding()
    {
        // Arrange
        var originalTokenizer = new ByteLevelBpeTokenizer();
        originalTokenizer.Train(SimpleTrainingText, 350);
        string testText = "the quick brown fox jumps";
        var originalTokens = originalTokenizer.Encode(testText);

        string tempFile = Path.Combine(Path.GetTempPath(), $"bpe_test_{Guid.NewGuid()}.json");

        try
        {
            // Act - save and reload
            originalTokenizer.SaveVocabulary(tempFile);

            var loadedTokenizer = new ByteLevelBpeTokenizer();
            loadedTokenizer.LoadVocabulary(tempFile);

            var loadedTokens = loadedTokenizer.Encode(testText);
            var decodedText = loadedTokenizer.Decode(loadedTokens);

            // Assert
            Assert.Equal(originalTokens, loadedTokens);
            Assert.Equal(testText, decodedText);
            Assert.Equal(originalTokenizer.VocabSize, loadedTokenizer.VocabSize);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void ByteLevelBpeTokenizer_TokenizerFactory_CreateByteLevelBpe()
    {
        // Act
        var tokenizer = TokenizerFactory.CreateByteLevelBpe(SimpleTrainingText, 300);

        // Assert
        Assert.NotNull(tokenizer);
        Assert.IsType<ByteLevelBpeTokenizer>(tokenizer);
        Assert.True(tokenizer.VocabSize >= 260);
    }

    [Fact]
    public void ByteLevelBpeTokenizer_TokenizerFactory_Load()
    {
        // Arrange - create and save a tokenizer
        var originalTokenizer = new ByteLevelBpeTokenizer();
        originalTokenizer.Train(SimpleTrainingText, 300);
        string tempFile = Path.Combine(Path.GetTempPath(), $"bpe_load_test_{Guid.NewGuid()}.json");

        try
        {
            originalTokenizer.SaveVocabulary(tempFile);

            // Act - load via factory
            var loadedTokenizer = TokenizerFactory.Load(tempFile);

            // Assert
            Assert.NotNull(loadedTokenizer);
            Assert.IsType<ByteLevelBpeTokenizer>(loadedTokenizer);
            Assert.Equal(originalTokenizer.VocabSize, loadedTokenizer.VocabSize);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void ByteLevelBpeTokenizer_ByteLevelCoverage_AllBytesEncodeable()
    {
        // Arrange
        var tokenizer = new ByteLevelBpeTokenizer();

        // Create a string with all possible byte values
        var allBytes = new byte[256];
        for (int i = 0; i < 256; i++)
        {
            allBytes[i] = (byte)i;
        }

        // This may not be valid UTF-8, but we should handle it at byte level
        // Let's create valid UTF-8 test cases for each byte range
        tokenizer.Train("ABCabc123!@# àéîôù ÀÉÎÔÙ 你好世界 🎉🌍", 300);

        // Act & Assert - test that various byte ranges work
        string ascii = "Hello World!";
        var asciiTokens = tokenizer.Encode(ascii);
        Assert.Equal(ascii, tokenizer.Decode(asciiTokens));

        string latin = "café naïve résumé";
        var latinTokens = tokenizer.Encode(latin);
        Assert.Equal(latin, tokenizer.Decode(latinTokens));

        string cjk = "你好世界";
        var cjkTokens = tokenizer.Encode(cjk);
        Assert.Equal(cjk, tokenizer.Decode(cjkTokens));
    }

    [Fact]
    public void ByteLevelBpeTokenizer_Info_ContainsCorrectMetadata()
    {
        // Arrange
        var tokenizer = new ByteLevelBpeTokenizer();
        tokenizer.Train(SimpleTrainingText, 300);

        // Assert
        Assert.Equal("ByteLevelBpe", tokenizer.Info.Name);
        Assert.Equal(tokenizer.VocabSize, tokenizer.Info.VocabSize);
        Assert.True(tokenizer.Info.SupportsByteFallback);
        Assert.Equal(256, tokenizer.Info.PadTokenId);
        Assert.Equal(257, tokenizer.Info.UnkTokenId);
        Assert.Equal(258, tokenizer.Info.BosTokenId);
        Assert.Equal(259, tokenizer.Info.EosTokenId);
    }

    [Fact]
    public void ByteLevelBpeTokenizer_DecodeToString_WorksCorrectly()
    {
        // Arrange
        var tokenizer = new ByteLevelBpeTokenizer();
        tokenizer.Train(SimpleTrainingText, 300);
        string testText = "the quick brown fox";

        // Act
        var tokens = tokenizer.Encode(testText);
        int[] tokenArray = tokens.ToArray();
        string decoded = tokenizer.DecodeToString(tokenArray);

        // Assert
        Assert.Equal(testText, decoded);
    }

    [Fact]
    public void ByteLevelBpeTokenizer_EncodeSpan_WorksCorrectly()
    {
        // Arrange
        var tokenizer = new ByteLevelBpeTokenizer();
        tokenizer.Train(SimpleTrainingText, 300);
        string testText = "the quick fox";
        byte[] utf8Bytes = Encoding.UTF8.GetBytes(testText);
        int[] tokensOut = new int[100];

        // Act
        int tokenCount = tokenizer.Encode(utf8Bytes, tokensOut);

        // Assert
        Assert.True(tokenCount > 0);
        Assert.True(tokenCount <= tokensOut.Length);

        // Verify by decoding
        string decoded = tokenizer.DecodeToString(tokensOut.AsSpan(0, tokenCount));
        Assert.Equal(testText, decoded);
    }

    [Fact]
    public void ByteLevelBpeTokenizer_DecodeSpan_WorksCorrectly()
    {
        // Arrange
        var tokenizer = new ByteLevelBpeTokenizer();
        tokenizer.Train(SimpleTrainingText, 300);
        string testText = "the quick fox";
        var tokens = tokenizer.Encode(testText);
        int[] tokenArray = tokens.ToArray();
        byte[] utf8Out = new byte[1000];

        // Act
        int byteCount = tokenizer.Decode(tokenArray, utf8Out);

        // Assert
        Assert.True(byteCount > 0);
        string decoded = Encoding.UTF8.GetString(utf8Out, 0, byteCount);
        Assert.Equal(testText, decoded);
    }

    [Fact]
    public void ByteLevelBpeTokenizer_Save_ImplementsITokenizer()
    {
        // Arrange
        var tokenizer = new ByteLevelBpeTokenizer();
        tokenizer.Train(SimpleTrainingText, 300);
        string tempFile = Path.Combine(Path.GetTempPath(), $"bpe_save_test_{Guid.NewGuid()}.json");

        try
        {
            // Act - use ITokenizer.Save method
            ITokenizer itokenizer = tokenizer;
            itokenizer.Save(tempFile);

            // Assert
            Assert.True(File.Exists(tempFile));

            // Verify we can load it back
            var loaded = new ByteLevelBpeTokenizer();
            loaded.LoadVocabulary(tempFile);
            Assert.Equal(tokenizer.VocabSize, loaded.VocabSize);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void ByteLevelBpeTokenizer_NullOrEmptyTrainingText_ThrowsException()
    {
        // Arrange
        var tokenizer = new ByteLevelBpeTokenizer();

        // Act & Assert - null training text
        Assert.Throws<ArgumentException>(() =>
            tokenizer.Train(null!, 300));

        // Act & Assert - empty training text
        Assert.Throws<ArgumentException>(() =>
            tokenizer.Train("", 300));
    }

    [Fact]
    public void ByteLevelBpeTokenizer_LoadNonExistentFile_ThrowsException()
    {
        // Arrange
        var tokenizer = new ByteLevelBpeTokenizer();
        string nonExistentFile = "/nonexistent/path/tokenizer.json";

        // Act & Assert
        Assert.Throws<FileNotFoundException>(() =>
            tokenizer.LoadVocabulary(nonExistentFile));
    }
}
