using SmallMind.Transformers;

namespace SmallMind.Tests;

/// <summary>
/// Unit tests for ModelConfig GGUF metadata extraction, specifically vocab size fallback.
/// </summary>
public class ModelConfigGgufTests
{
    // ---------------------------------------------------------------
    // a) llama.vocab_size present -> existing path unchanged
    // ---------------------------------------------------------------

    [Fact]
    public void ModelConfig_FromGgufMetadata_UsesLlamaVocabSize_WhenPresent()
    {
        // Arrange - GGUF metadata with llama.vocab_size
        var metadata = new Dictionary<string, object>
        {
            ["general.architecture"] = "llama",
            ["llama.vocab_size"] = 32000,
            ["llama.context_length"] = 2048,
            ["llama.embedding_length"] = 512,
            ["llama.block_count"] = 8,
            ["llama.attention.head_count"] = 8,
            ["llama.attention.head_count_kv"] = 8,
            ["llama.feed_forward_length"] = 2048,
            ["tokenizer.ggml.tokens"] = new object[] { "a", "b", "c" } // 3 tokens (should be ignored)
        };

        // Act
        var config = ModelConfig.FromGgufMetadata(metadata);

        // Assert
        Assert.Equal(32000, config.VocabSize); // Should use llama.vocab_size, not tokenizer count
    }

    // ---------------------------------------------------------------
    // b) missing llama.vocab_size, token list present -> infer from token count
    // ---------------------------------------------------------------

    [Fact]
    public void ModelConfig_FromGgufMetadata_InfersVocabSizeFromTokenizer_WhenLlamaVocabSizeMissing()
    {
        // Arrange - GGUF metadata without llama.vocab_size but with tokenizer.ggml.tokens
        var metadata = new Dictionary<string, object>
        {
            ["general.architecture"] = "llama",
            ["llama.context_length"] = 2048,
            ["llama.embedding_length"] = 512,
            ["llama.block_count"] = 8,
            ["llama.attention.head_count"] = 8,
            ["llama.attention.head_count_kv"] = 8,
            ["llama.feed_forward_length"] = 2048,
            ["tokenizer.ggml.tokens"] = new object[] { "a", "b", "c", "d", "e" } // 5 tokens
        };

        // Act
        var config = ModelConfig.FromGgufMetadata(metadata);

        // Assert
        Assert.Equal(5, config.VocabSize); // Should infer from tokenizer.ggml.tokens
    }

    [Fact]
    public void ModelConfig_FromGgufMetadata_HandlesStringArrayTokens()
    {
        // Arrange - Test with string[] instead of object[]
        var metadata = new Dictionary<string, object>
        {
            ["general.architecture"] = "llama",
            ["llama.context_length"] = 2048,
            ["llama.embedding_length"] = 512,
            ["llama.block_count"] = 8,
            ["llama.attention.head_count"] = 8,
            ["llama.attention.head_count_kv"] = 8,
            ["llama.feed_forward_length"] = 2048,
            ["tokenizer.ggml.tokens"] = new string[] { "a", "b", "c", "d" } // 4 tokens
        };

        // Act
        var config = ModelConfig.FromGgufMetadata(metadata);

        // Assert
        Assert.Equal(4, config.VocabSize);
    }

    /// <summary>
    /// Regression test: model has tokenizer.ggml.vocab_size but no llama.vocab_size or token list.
    /// Represents the shape of models like uk-fraud-chatbot-llama2-f16.gguf.
    /// </summary>
    [Fact]
    public void ModelConfig_FromGgufMetadata_InfersVocabSize_FromTokenizerGgmlVocabSize()
    {
        // Arrange: metadata has tokenizer.ggml.vocab_size but no llama.vocab_size
        var metadata = new Dictionary<string, object>
        {
            ["general.architecture"] = "llama",
            ["llama.context_length"] = 4096,
            ["llama.embedding_length"] = 4096,
            ["llama.block_count"] = 32,
            ["llama.attention.head_count"] = 32,
            ["llama.attention.head_count_kv"] = 32,
            ["llama.feed_forward_length"] = 11008,
            // No llama.vocab_size - simulates uk-fraud-chatbot-llama2-f16.gguf shape
            ["tokenizer.ggml.vocab_size"] = 32000,
        };

        // Act
        var config = ModelConfig.FromGgufMetadata(metadata);

        // Assert
        Assert.Equal(32000, config.VocabSize);
    }

    // ---------------------------------------------------------------
    // c) missing all vocab hints -> throws expected exception with useful message
    // ---------------------------------------------------------------

    [Fact]
    public void ModelConfig_FromGgufMetadata_ThrowsMissingMetadataException_WhenAllVocabHintsMissing()
    {
        // Arrange: no vocab size key, no tokenizer.ggml.vocab_size, no token list
        var metadata = new Dictionary<string, object>
        {
            ["general.architecture"] = "llama",
            ["llama.context_length"] = 2048,
            ["llama.embedding_length"] = 512,
            ["llama.block_count"] = 8,
            ["llama.attention.head_count"] = 8,
            ["llama.attention.head_count_kv"] = 8,
            ["llama.feed_forward_length"] = 2048,
            // No vocab size keys at all
        };

        // Act & Assert
        var ex = Assert.ThrowsAny<Exception>(() => ModelConfig.FromGgufMetadata(metadata));

        // Exception message should mention the keys that were tried
        Assert.Contains("llama.vocab_size", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("tokenizer.ggml.vocab_size", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("tokenizer.ggml.tokens", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ---------------------------------------------------------------
    // d) malformed vocab metadata -> throws validation exception
    // ---------------------------------------------------------------

    [Fact]
    public void ModelConfig_FromGgufMetadata_ThrowsValidation_WhenVocabSizeIsZero()
    {
        // Arrange: token list is empty (inferred vocab size = 0)
        var metadata = new Dictionary<string, object>
        {
            ["general.architecture"] = "llama",
            ["llama.context_length"] = 2048,
            ["llama.embedding_length"] = 512,
            ["llama.block_count"] = 8,
            ["llama.attention.head_count"] = 8,
            ["llama.attention.head_count_kv"] = 8,
            ["llama.feed_forward_length"] = 2048,
            ["tokenizer.ggml.tokens"] = new object[0], // Empty token list -> vocab size = 0
        };

        // Act & Assert
        var ex = Assert.ThrowsAny<Exception>(() => ModelConfig.FromGgufMetadata(metadata));
        Assert.Contains("vocab", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ModelConfig_FromGgufMetadata_ThrowsValidation_WhenVocabSizeIsUnreasonablyLarge()
    {
        // Arrange: tokenizer.ggml.vocab_size is set to an absurd value
        var metadata = new Dictionary<string, object>
        {
            ["general.architecture"] = "llama",
            ["llama.context_length"] = 2048,
            ["llama.embedding_length"] = 512,
            ["llama.block_count"] = 8,
            ["llama.attention.head_count"] = 8,
            ["llama.attention.head_count_kv"] = 8,
            ["llama.feed_forward_length"] = 2048,
            ["tokenizer.ggml.vocab_size"] = 5_000_000, // Way above sanity limit
        };

        // Act & Assert
        var ex = Assert.ThrowsAny<Exception>(() => ModelConfig.FromGgufMetadata(metadata));
        Assert.Contains("sanity", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ---------------------------------------------------------------
    // Additional: RoPE / defaults coverage
    // ---------------------------------------------------------------

    [Fact]
    public void ModelConfig_FromGgufMetadata_ExtractsRopeFreqBaseCorrectly()
    {
        // Arrange - GGUF metadata with custom RoPE freq base (SmolLM2 uses 100000)
        var metadata = new Dictionary<string, object>
        {
            ["general.architecture"] = "llama",
            ["llama.vocab_size"] = 49152,
            ["llama.context_length"] = 2048,
            ["llama.embedding_length"] = 576,
            ["llama.block_count"] = 30,
            ["llama.attention.head_count"] = 9,
            ["llama.attention.head_count_kv"] = 3,
            ["llama.feed_forward_length"] = 1536,
            ["llama.rope.freq_base"] = 100000.0  // SmolLM2 value
        };

        // Act
        var config = ModelConfig.FromGgufMetadata(metadata);

        // Assert
        Assert.Equal(100000.0, config.RopeFreqBase);
    }

    [Fact]
    public void ModelConfig_FromGgufMetadata_UsesDefaultRopeFreqBase_WhenMissing()
    {
        // Arrange - GGUF metadata without rope.freq_base
        var metadata = new Dictionary<string, object>
        {
            ["general.architecture"] = "llama",
            ["llama.vocab_size"] = 32000,
            ["llama.context_length"] = 2048,
            ["llama.embedding_length"] = 512,
            ["llama.block_count"] = 8,
            ["llama.attention.head_count"] = 8,
            ["llama.attention.head_count_kv"] = 8,
            ["llama.feed_forward_length"] = 2048
        };

        // Act
        var config = ModelConfig.FromGgufMetadata(metadata);

        // Assert
        Assert.Equal(10000.0, config.RopeFreqBase); // Default value
    }
}
