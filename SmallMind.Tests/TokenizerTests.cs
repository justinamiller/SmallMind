namespace SmallMind.Tests;

public class TokenizerTests
{
    [Fact]
    public void Tokenize_EmptyString_ReturnsEmpty()
    {
        var tokenizer = new Tokenizer();
        var tokens = tokenizer.Tokenize("");
        Assert.Empty(tokens);
    }

    [Fact]
    public void Tokenize_SimpleText_ReturnsTokens()
    {
        var tokenizer = new Tokenizer();
        var tokens = tokenizer.Tokenize("The cat sat");
        Assert.Equal(3, tokens.Length);
    }

    [Fact]
    public void Tokenize_SameWord_ReturnsSameToken()
    {
        var tokenizer = new Tokenizer();
        var tokens1 = tokenizer.Tokenize("cat");
        var tokens2 = tokenizer.Tokenize("cat");
        Assert.Equal(tokens1[0], tokens2[0]);
    }

    [Fact]
    public void Detokenize_Tokens_ReturnsText()
    {
        var tokenizer = new Tokenizer();
        var originalText = "the cat sat";
        var tokens = tokenizer.Tokenize(originalText);
        var detokenized = tokenizer.Detokenize(tokens);
        Assert.Equal(originalText, detokenized);
    }

    [Fact]
    public void VocabularySize_IncreasesWithNewWords()
    {
        var tokenizer = new Tokenizer();
        Assert.Equal(0, tokenizer.VocabularySize);
        
        tokenizer.Tokenize("the cat");
        Assert.Equal(2, tokenizer.VocabularySize);
        
        tokenizer.Tokenize("the dog");
        Assert.Equal(3, tokenizer.VocabularySize);
    }
}
