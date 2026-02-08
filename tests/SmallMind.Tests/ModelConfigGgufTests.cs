using System;
using System.Collections.Generic;
using Xunit;
using SmallMind.Transformers;

namespace SmallMind.Tests;

/// <summary>
/// Unit tests for ModelConfig GGUF metadata extraction, specifically vocab size fallback.
/// </summary>
public class ModelConfigGgufTests
{
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
