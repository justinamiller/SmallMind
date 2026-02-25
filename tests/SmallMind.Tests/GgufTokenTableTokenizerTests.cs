using SmallMind.Tokenizers.Gguf;

namespace SmallMind.Tests;

/// <summary>
/// Unit tests for GgufTokenTableTokenizer covering:
///   - Special-token (no-▁-prefix) encoding fallback for chat-template markers
///   - Decode skipping of BOS / EOS / PAD / UNK tokens
/// </summary>
public class GgufTokenTableTokenizerTests
{
    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    /// <summary>Build a minimal SpecialTokens struct via the factory helper.</summary>
    private static GgufTokenTableTokenizer BuildTokenizer(
        Dictionary<string, int> vocab,
        int bosId = 1,
        int eosId = 2,
        int padId = -1,
        int unkId = -1)
    {
        // Build reverse vocab sized to the highest token ID (not vocab.Count, which can be
        // much smaller than the token IDs when special/added tokens have large IDs).
        int maxTokenId = vocab.Values.Count > 0 ? vocab.Values.Max() : 0;
        var reverseVocab = new List<string>(new string[maxTokenId + 1]);
        foreach (var kvp in vocab)
        {
            reverseVocab[kvp.Value] = kvp.Key;
        }

        var specialTokens = new SpecialTokens
        {
            BosTokenId = bosId,
            EosTokenId = eosId,
            PadTokenId = padId,
            UnkTokenId = unkId,
        };

        return new GgufTokenTableTokenizer(vocab, reverseVocab, specialTokens);
    }

    // ------------------------------------------------------------------
    // Encode – special-token fallback (no ▁ prefix)
    // ------------------------------------------------------------------

    [Fact]
    public void Encode_ChatUserToken_EncodesAsSingleToken_WhenInVocabWithoutPrefix()
    {
        // Arrange – simulate a Zephyr/TinyLlama chat vocab where <|user|> is a
        // high-ID added token stored WITHOUT the ▁ word-boundary prefix.
        var vocab = new Dictionary<string, int>
        {
            ["<s>"]        = 1,
            ["</s>"]       = 2,
            ["<|user|>"]   = 32001,
            ["▁Hello"]     = 1000,
            ["▁world"]     = 1001,
        };

        var tokenizer = BuildTokenizer(vocab, bosId: 1, eosId: 2);

        // Act – "<|user|>" must encode to a single token, not fall back to bytes
        var tokens = tokenizer.Encode("<|user|>");

        // Assert
        Assert.Single(tokens);
        Assert.Equal(32001, tokens[0]);
    }

    [Fact]
    public void Encode_ChatAssistantToken_EncodesAsSingleToken()
    {
        var vocab = new Dictionary<string, int>
        {
            ["<s>"]             = 1,
            ["</s>"]            = 2,
            ["<|assistant|>"]   = 32002,
            ["▁Paris"]          = 3000,
        };

        var tokenizer = BuildTokenizer(vocab, bosId: 1, eosId: 2);

        var tokens = tokenizer.Encode("<|assistant|>");

        Assert.Single(tokens);
        Assert.Equal(32002, tokens[0]);
    }

    [Fact]
    public void Encode_EosTokenInWord_EncodesAsSingleToken()
    {
        // EOS "</s>" appears inside a "word" after punctuation (Zephyr template).
        // e.g.  "France?</s>"  should produce [ ▁France, ?, </s> ]
        var vocab = new Dictionary<string, int>
        {
            ["<s>"]      = 1,
            ["</s>"]     = 2,
            ["▁France"]  = 3479,
            ["?"]        = 29973,
        };

        var tokenizer = BuildTokenizer(vocab, bosId: 1, eosId: 2);

        var tokens = tokenizer.Encode("France?</s>");

        // Should produce: ▁France(3479), ?(29973), </s>(2)
        Assert.Equal(3, tokens.Count);
        Assert.Equal(3479,  tokens[0]);
        Assert.Equal(29973, tokens[1]);
        Assert.Equal(2,     tokens[2]);
    }

    [Fact]
    public void Encode_ZephyrChatTemplate_AllSpecialTokensResolved()
    {
        // Simulate a full Zephyr-style prompt: "<|user|>\nHello</s>\n<|assistant|>\n"
        // After whitespace-splitting and word encoding all special tokens should be
        // single vocab entries, not byte-fallback garbage.
        var vocab = new Dictionary<string, int>
        {
            ["<s>"]           = 1,
            ["</s>"]          = 2,
            ["<|user|>"]      = 32001,
            ["<|assistant|>"] = 32002,
            ["▁Hello"]        = 500,
        };

        var tokenizer = BuildTokenizer(vocab, bosId: 1, eosId: 2);

        // The template string – \n characters are whitespace and are skipped by Encode.
        var tokens = tokenizer.Encode("<|user|>\nHello</s>\n<|assistant|>\n");

        // Expected: <|user|>(32001), ▁Hello(500), </s>(2), <|assistant|>(32002)
        Assert.Equal(4, tokens.Count);
        Assert.Equal(32001, tokens[0]);
        Assert.Equal(500,   tokens[1]);
        Assert.Equal(2,     tokens[2]);
        Assert.Equal(32002, tokens[3]);
    }

    [Fact]
    public void Encode_RegularWord_StillPrefersPrefixedToken()
    {
        // When both "▁Paris" and "Paris" exist in the vocab, the ▁-prefixed form
        // must win for a word at the start of a sentence (word boundary).
        var vocab = new Dictionary<string, int>
        {
            ["<s>"]     = 1,
            ["</s>"]    = 2,
            ["▁Paris"]  = 3479,
            ["Paris"]   = 9999,  // non-prefixed version also present
        };

        var tokenizer = BuildTokenizer(vocab, bosId: 1, eosId: 2);

        var tokens = tokenizer.Encode("Paris");

        Assert.Single(tokens);
        Assert.Equal(3479, tokens[0]); // ▁Paris wins over Paris
    }

    // ------------------------------------------------------------------
    // Decode – BOS / EOS / PAD / UNK filtering
    // ------------------------------------------------------------------

    [Fact]
    public void Decode_SkipsBosToken_AtStart()
    {
        var vocab = new Dictionary<string, int>
        {
            ["<s>"]     = 1,
            ["</s>"]    = 2,
            ["▁Hello"]  = 500,
            ["▁world"]  = 501,
        };
        var tokenizer = BuildTokenizer(vocab, bosId: 1, eosId: 2);

        // BOS at position 0 must be skipped
        var result = tokenizer.Decode(new List<int> { 1, 500, 501 });

        Assert.Equal(" Hello world", result);
    }

    [Fact]
    public void Decode_SkipsEosToken_AtEnd()
    {
        var vocab = new Dictionary<string, int>
        {
            ["<s>"]     = 1,
            ["</s>"]    = 2,
            ["▁Hi"]     = 300,
        };
        var tokenizer = BuildTokenizer(vocab, bosId: 1, eosId: 2);

        // EOS at the end (generated stop token) must be stripped
        var result = tokenizer.Decode(new List<int> { 300, 2 });

        Assert.Equal(" Hi", result);
    }

    [Fact]
    public void Decode_SkipsBosAndEos_BothPresent()
    {
        var vocab = new Dictionary<string, int>
        {
            ["<s>"]     = 1,
            ["</s>"]    = 2,
            ["▁Paris"]  = 3479,
        };
        var tokenizer = BuildTokenizer(vocab, bosId: 1, eosId: 2);

        // Typical inference context: [BOS, ▁Paris, EOS]
        var result = tokenizer.Decode(new List<int> { 1, 3479, 2 });

        Assert.Equal(" Paris", result);
    }

    [Fact]
    public void Decode_SkipsBos_InMiddleOfSequence()
    {
        // BOS appearing mid-sequence (unusual but must still be stripped)
        var vocab = new Dictionary<string, int>
        {
            ["<s>"]     = 1,
            ["</s>"]    = 2,
            ["▁a"]      = 10,
            ["▁b"]      = 11,
        };
        var tokenizer = BuildTokenizer(vocab, bosId: 1, eosId: 2);

        var result = tokenizer.Decode(new List<int> { 10, 1, 11 });

        Assert.Equal(" a b", result);
    }

    [Fact]
    public void Decode_ChatTemplateTokens_NotSkipped()
    {
        // Chat-marker tokens such as <|user|> and <|assistant|> are NOT structural
        // special tokens (BOS/EOS/PAD/UNK), so they should appear in the decoded text.
        var vocab = new Dictionary<string, int>
        {
            ["<s>"]           = 1,
            ["</s>"]          = 2,
            ["<|user|>"]      = 32001,
            ["<|assistant|>"] = 32002,
            ["▁Hello"]        = 500,
        };
        var tokenizer = BuildTokenizer(vocab, bosId: 1, eosId: 2);

        // BOS stripped, chat markers kept, EOS stripped
        var result = tokenizer.Decode(new List<int> { 1, 32001, 500, 32002, 2 });

        Assert.Equal("<|user|> Hello<|assistant|>", result);
    }
}
