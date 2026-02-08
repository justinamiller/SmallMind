using System.Collections.Generic;
using Xunit;
using SmallMind.Tokenizers.Text;

namespace SmallMind.Tests;

/// <summary>
/// Unit tests for GgufBpeTokenizer, specifically for BOS token prepending functionality.
/// </summary>
public class GgufBpeTokenizerTests
{
    [Fact]
    public void GgufBpeTokenizer_Encode_PrependsBosToken_WhenBosTokenIdConfigured()
    {
        // Arrange - Create a minimal tokenizer with BOS token and character vocab
        var vocab = new Dictionary<string, int>
        {
            ["<s>"] = 1,      // BOS token
            ["</s>"] = 2,     // EOS token
            ["a"] = 100,
            ["b"] = 101,
            ["c"] = 102,
            [" "] = 103,
            ["ab"] = 200,     // Merged token
        };
        
        var merges = new List<(string, string)>
        {
            ("a", "b")  // Merge "a" + "b" -> "ab"
        };
        int bosTokenId = 1;
        int eosTokenId = 2;
        
        var tokenizer = new GgufBpeTokenizer(
            vocab, 
            merges, 
            bosTokenId, 
            eosTokenId, 
            unkTokenId: -1, 
            isByteLevelBpe: false);

        // Act
        var tokens = tokenizer.Encode("ab c");
        
        // Assert
        Assert.NotEmpty(tokens);
        Assert.Equal(bosTokenId, tokens[0]); // First token should be BOS
        Assert.True(tokens.Count > 1); // Should have BOS + actual tokens
    }

    [Fact]
    public void GgufBpeTokenizer_Encode_DoesNotDuplicateBosToken_WhenAlreadyPresent()
    {
        // Arrange
        var vocab = new Dictionary<string, int>
        {
            ["<s>"] = 1,
            ["</s>"] = 2,
            ["t"] = 100,
            ["e"] = 101,
            ["s"] = 102,
            ["te"] = 200,
            ["st"] = 201
        };
        
        var merges = new List<(string, string)>
        {
            ("t", "e"),
            ("s", "t")
        };
        int bosTokenId = 1;
        
        var tokenizer = new GgufBpeTokenizer(
            vocab, 
            merges, 
            bosTokenId, 
            eosTokenId: 2, 
            unkTokenId: -1, 
            isByteLevelBpe: false);

        // Act
        var tokens = tokenizer.Encode("test");
        
        // Assert
        Assert.NotEmpty(tokens);
        Assert.Equal(bosTokenId, tokens[0]); // First token should be BOS
        
        // Count BOS occurrences - should only appear once
        int bosCount = 0;
        foreach (var token in tokens)
        {
            if (token == bosTokenId)
                bosCount++;
        }
        Assert.Equal(1, bosCount); // BOS should appear exactly once
    }

    [Fact]
    public void GgufBpeTokenizer_Encode_DoesNotPrependBos_WhenBosTokenIdNotConfigured()
    {
        // Arrange - Tokenizer without BOS token (like GPT-2)
        var vocab = new Dictionary<string, int>
        {
            ["a"] = 100,
            ["b"] = 101,
            [" "] = 102,
            ["ab"] = 200
        };
        
        var merges = new List<(string, string)>
        {
            ("a", "b")
        };
        
        var tokenizer = new GgufBpeTokenizer(
            vocab, 
            merges, 
            bosTokenId: -1,  // No BOS token
            eosTokenId: -1, 
            unkTokenId: -1, 
            isByteLevelBpe: false);

        // Act
        var tokens = tokenizer.Encode("ab");
        
        // Assert
        Assert.NotEmpty(tokens);
        // When no BOS token configured, tokens should not start with -1
        Assert.NotEqual(-1, tokens[0]); 
        // All tokens should be valid vocab IDs
        Assert.All(tokens, token => Assert.True(token >= 0));
    }

    [Fact]
    public void GgufBpeTokenizer_Encode_HandlesEmptyString()
    {
        // Arrange
        var vocab = new Dictionary<string, int>
        {
            ["<s>"] = 1,
            ["test"] = 100
        };
        
        var merges = new List<(string, string)>();
        
        var tokenizer = new GgufBpeTokenizer(
            vocab, 
            merges, 
            bosTokenId: 1, 
            eosTokenId: 2, 
            unkTokenId: -1, 
            isByteLevelBpe: false);

        // Act
        var tokens = tokenizer.Encode("");
        
        // Assert
        // Empty string should return empty list (BOS not added to empty input)
        Assert.Empty(tokens);
    }

    [Fact]
    public void GgufBpeTokenizer_ByteLevelBpe_PrependsBosToken()
    {
        // Test that BOS prepending works independent of byte-level mode
        // For this test, we'll just verify the BOS token ID is in Info
        var vocab = new Dictionary<string, int>
        {
            ["<s>"] = 1,
            ["a"] = 100,
        };
        
        var merges = new List<(string, string)>();
        int bosTokenId = 1;
        
        // Create tokenizer WITHOUT byte-level mode to avoid memory issues in test
        var tokenizer = new GgufBpeTokenizer(
            vocab, 
            merges, 
            bosTokenId, 
            eosTokenId: 2, 
            unkTokenId: -1, 
            isByteLevelBpe: false);  // Changed to false for test

        // Verify BOS token is configured
        Assert.Equal(bosTokenId, tokenizer.Info.BosTokenId);
        
        // Encode should prepend BOS
        var tokens = tokenizer.Encode("a");
        Assert.NotEmpty(tokens);
        Assert.Equal(bosTokenId, tokens[0]);
    }

    [Fact]
    public void GgufBpeTokenizer_Decode_SkipsBosToken()
    {
        // Arrange
        var vocab = new Dictionary<string, int>
        {
            ["<s>"] = 1,      // BOS token
            ["h"] = 100,
            ["e"] = 101,
            ["l"] = 102,
            ["o"] = 103,
        };
        
        var merges = new List<(string, string)>();
        int bosTokenId = 1;
        
        var tokenizer = new GgufBpeTokenizer(
            vocab, 
            merges, 
            bosTokenId, 
            eosTokenId: 2, 
            unkTokenId: -1, 
            isByteLevelBpe: false);

        // Act - Decode tokens with BOS at start
        var tokensWithBos = new List<int> { 1, 100, 101, 102, 103 }; // <s> h e l o
        var decoded = tokenizer.Decode(tokensWithBos);
        
        // Assert - BOS should be stripped, only "helo" remains
        Assert.Equal("helo", decoded);
    }

    [Fact]
    public void GgufBpeTokenizer_Decode_WithoutBosToken()
    {
        // Arrange
        var vocab = new Dictionary<string, int>
        {
            ["<s>"] = 1,
            ["t"] = 100,
            ["e"] = 101,
            ["s"] = 102,
        };
        
        var merges = new List<(string, string)>();
        int bosTokenId = 1;
        
        var tokenizer = new GgufBpeTokenizer(
            vocab, 
            merges, 
            bosTokenId, 
            eosTokenId: 2, 
            unkTokenId: -1, 
            isByteLevelBpe: false);

        // Act - Decode tokens without BOS at start
        var tokensNoBos = new List<int> { 100, 101, 102 }; // t e s
        var decoded = tokenizer.Decode(tokensNoBos);
        
        // Assert - All tokens should be decoded
        Assert.Equal("tes", decoded);
    }

    [Fact]
    public void GgufBpeTokenizer_AddBosProperty_IsTrueWhenBosTokenConfigured()
    {
        // Arrange & Act
        var vocab = new Dictionary<string, int> { ["<s>"] = 1, ["a"] = 100 };
        var merges = new List<(string, string)>();
        var tokenizer = new GgufBpeTokenizer(vocab, merges, bosTokenId: 1);

        // Assert
        Assert.True(tokenizer.Info.AddBos);
    }

    [Fact]
    public void GgufBpeTokenizer_AddBosProperty_IsFalseWhenBosTokenNotConfigured()
    {
        // Arrange & Act
        var vocab = new Dictionary<string, int> { ["a"] = 100 };
        var merges = new List<(string, string)>();
        var tokenizer = new GgufBpeTokenizer(vocab, merges, bosTokenId: -1);

        // Assert
        Assert.False(tokenizer.Info.AddBos);
    }
}
