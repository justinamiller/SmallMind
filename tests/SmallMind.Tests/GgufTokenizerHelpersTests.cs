using SmallMind.Tokenizers.Gguf;

namespace SmallMind.Tests
{
    /// <summary>
    /// Tests for GgufTokenizerHelpers utility class.
    /// Verifies byte token detection and parsing for GGUF tokenizers.
    /// </summary>
    public class GgufTokenizerHelpersTests
    {
        [Theory]
        [InlineData("<0x00>", true, 0x00)]
        [InlineData("<0x20>", true, 0x20)]  // Space character
        [InlineData("<0xFF>", true, 0xFF)]  // Max byte value
        [InlineData("<0x0A>", true, 0x0A)]  // Newline
        [InlineData("<0x41>", true, 0x41)]  // 'A'
        public void IsByteToken_ValidByteTokens_ReturnsTrueWithCorrectValue(string tokenStr, bool expectedResult, byte expectedValue)
        {
            // Act
            bool result = GgufTokenizerHelpers.IsByteToken(tokenStr, out byte byteValue);

            // Assert
            Assert.Equal(expectedResult, result);
            Assert.Equal(expectedValue, byteValue);
        }

        [Theory]
        [InlineData("hello")]              // Regular text
        [InlineData("<0x>")]                // Missing hex digits
        [InlineData("<0x1>")]               // Only one hex digit
        [InlineData("<0x123>")]             // Too many hex digits
        [InlineData("0x20")]                // Missing angle brackets
        [InlineData("<0x20")]               // Missing closing bracket
        [InlineData("0x20>")]               // Missing opening bracket
        [InlineData("<0xGG>")]              // Invalid hex characters
        [InlineData("<0x100>")]             // Value too large for byte
        [InlineData("")]                    // Empty string
        public void IsByteToken_InvalidTokens_ReturnsFalse(string tokenStr)
        {
            // Act
            bool result = GgufTokenizerHelpers.IsByteToken(tokenStr, out byte byteValue);

            // Assert
            Assert.False(result);
            Assert.Equal(0, byteValue);
        }

        [Fact]
        public void IsByteToken_LowercaseHex_ParsesCorrectly()
        {
            // Arrange
            string tokenStr = "<0xff>";

            // Act
            bool result = GgufTokenizerHelpers.IsByteToken(tokenStr, out byte byteValue);

            // Assert
            Assert.True(result);
            Assert.Equal(0xFF, byteValue);
        }

        [Fact]
        public void IsByteToken_MixedCaseHex_ParsesCorrectly()
        {
            // Arrange
            string tokenStr = "<0xAb>";

            // Act
            bool result = GgufTokenizerHelpers.IsByteToken(tokenStr, out byte byteValue);

            // Assert
            Assert.True(result);
            Assert.Equal(0xAB, byteValue);
        }

        [Theory]
        [InlineData("<0x00>")]  // Null
        [InlineData("<0x01>")]
        [InlineData("<0x7F>")]  // DEL
        [InlineData("<0x80>")]  // Extended ASCII start
        [InlineData("<0xFE>")]
        [InlineData("<0xFF>")]  // Max value
        public void IsByteToken_AllValidByteRanges_ParsesCorrectly(string tokenStr)
        {
            // Act
            bool result = GgufTokenizerHelpers.IsByteToken(tokenStr, out byte byteValue);

            // Assert
            Assert.True(result);
            Assert.InRange(byteValue, 0, 255);
        }

        [Fact]
        public void IsByteToken_CommonWhitespaceTokens_ParsesCorrectly()
        {
            // Arrange
            var testCases = new[]
            {
                ("<0x20>", (byte)0x20),  // Space
                ("<0x09>", (byte)0x09),  // Tab
                ("<0x0A>", (byte)0x0A),  // Line feed
                ("<0x0D>", (byte)0x0D)   // Carriage return
            };

            foreach (var (tokenStr, expectedByte) in testCases)
            {
                // Act
                bool result = GgufTokenizerHelpers.IsByteToken(tokenStr, out byte byteValue);

                // Assert
                Assert.True(result, $"Failed to parse {tokenStr}");
                Assert.Equal(expectedByte, byteValue);
            }
        }

        [Fact]
        public void ByteTokenLength_IsCorrect()
        {
            // Assert
            Assert.Equal(6, GgufTokenizerHelpers.ByteTokenLength);
        }

        [Theory]
        [InlineData("<0x20>", 6)]
        [InlineData("<0xFF>", 6)]
        [InlineData("<0x00>", 6)]
        public void IsByteToken_ValidTokensHaveCorrectLength(string tokenStr, int expectedLength)
        {
            // Assert
            Assert.Equal(expectedLength, tokenStr.Length);
            Assert.Equal(GgufTokenizerHelpers.ByteTokenLength, tokenStr.Length);

            // Act
            bool result = GgufTokenizerHelpers.IsByteToken(tokenStr, out _);

            // Assert
            Assert.True(result);
        }
    }

    /// <summary>
    /// Tests for SentencePiece space character (▁ U+2581) handling in GGUF tokenizer decode.
    /// Ensures that the GGUF tokenizers used for LLaMA/TinyLlama models correctly convert
    /// "▁" (word-leading space marker) to a regular space " " when decoding.
    /// </summary>
    public class GgufTokenizerSentencePieceDecodeTests
    {
        [Fact]
        public void GgufBpeTokenizer_Decode_ReplacesLeadingSpaceMarker_WithRegularSpace()
        {
            // Arrange: vocabulary simulating LLaMA-style tokens with "▁" prefix
            var vocab = new Dictionary<string, int>
            {
                ["▁Paris"] = 0,
                ["▁is"] = 1,
                ["▁the"] = 2,
                ["▁capital"] = 3,
            };
            var reverseVocab = new List<string> { "▁Paris", "▁is", "▁the", "▁capital" };
            var merges = new List<(string, string)>();
            var specialTokens = new SpecialTokens();

            var tokenizer = new SmallMind.Tokenizers.Gguf.GgufBpeTokenizer(
                vocab, reverseVocab, merges, specialTokens);

            // Act: decode tokens that map to SentencePiece-style tokens
            var decoded = tokenizer.Decode(new List<int> { 0, 1, 2, 3 });

            // Assert: "▁" should be converted to " " (regular space)
            Assert.Equal(" Paris is the capital", decoded);
            Assert.DoesNotContain('\u2581', decoded); // No "▁" chars remain
            Assert.Contains(' ', decoded);            // Regular spaces are present
        }

        [Fact]
        public void GgufBpeTokenizer_Decode_SpacePct_IsNonZero_AfterFix()
        {
            // Arrange: tokens including word-boundary markers
            var vocab = new Dictionary<string, int>
            {
                ["Hello"] = 0,
                ["▁world"] = 1,
            };
            var reverseVocab = new List<string> { "Hello", "▁world" };
            var merges = new List<(string, string)>();
            var specialTokens = new SpecialTokens();

            var tokenizer = new SmallMind.Tokenizers.Gguf.GgufBpeTokenizer(
                vocab, reverseVocab, merges, specialTokens);

            // Act
            var decoded = tokenizer.Decode(new List<int> { 0, 1 });

            // Assert
            Assert.Equal("Hello world", decoded);

            // Verify coherence check would pass (spacePct 5-25%)
            int spaceCount = decoded.Count(c => char.IsWhiteSpace(c));
            double spacePct = (double)spaceCount / decoded.Length;
            Assert.InRange(spacePct, 0.05, 0.25);
        }

        [Fact]
        public void GgufTokenTableTokenizer_Decode_ReplacesLeadingSpaceMarker_WithRegularSpace()
        {
            // Arrange: LLaMA-style vocabulary
            var vocab = new Dictionary<string, int>
            {
                ["▁The"] = 0,
                ["▁capital"] = 1,
                ["▁of"] = 2,
                ["▁France"] = 3,
                ["▁is"] = 4,
                ["▁Paris"] = 5,
                ["."] = 6,
            };
            var reverseVocab = new List<string>
                { "▁The", "▁capital", "▁of", "▁France", "▁is", "▁Paris", "." };
            var specialTokens = new SpecialTokens();

            var tokenizer = new SmallMind.Tokenizers.Gguf.GgufTokenTableTokenizer(
                vocab, reverseVocab, specialTokens);

            // Act
            var decoded = tokenizer.Decode(new List<int> { 0, 1, 2, 3, 4, 5, 6 });

            // Assert
            Assert.Equal(" The capital of France is Paris.", decoded);
            Assert.DoesNotContain('\u2581', decoded);
        }

        [Fact]
        public void GgufBpeTokenizer_Decode_MixedTokens_ByteAndSentencePiece_WorksCorrectly()
        {
            // Arrange: mix of byte tokens and SentencePiece tokens
            var vocab = new Dictionary<string, int>
            {
                ["<0x0A>"] = 0,  // newline byte token
                ["▁Hello"] = 1,  // SentencePiece word token
            };
            var reverseVocab = new List<string> { "<0x0A>", "▁Hello" };
            var merges = new List<(string, string)>();
            var specialTokens = new SpecialTokens();

            var tokenizer = new SmallMind.Tokenizers.Gguf.GgufBpeTokenizer(
                vocab, reverseVocab, merges, specialTokens);

            // Act
            var decoded = tokenizer.Decode(new List<int> { 0, 1 });

            // Assert: byte token decoded as '\n', SentencePiece token decoded with space
            Assert.Equal("\n Hello", decoded);
        }
    }

    /// <summary>
    /// Tests for GgufBpeTokenizer SentencePiece-style encoding (LLaMA/TinyLlama path).
    /// Validates the three fixes needed for coherent TinyLlama GGUF output:
    ///   1. Spaces encoded as ▁ (not &lt;0x20&gt; byte tokens) so BPE merges match
    ///   2. BOS token prepended when BosTokenId is configured
    ///   3. BOS token skipped in Decode so output.Substring(prompt.Length) gives the right slice
    /// </summary>
    public class GgufBpeTokenizerEncodeTests
    {
        /// <summary>
        /// Build a minimal LLaMA-style SentencePiece vocab + merge table that can encode "The".
        /// The vocab contains standalone ▁ (token 0) to trigger _isSentencePiece = true.
        /// </summary>
        private static (Dictionary<string, int> vocab, List<string> reverseVocab, List<(string, string)> merges) BuildLlamaMinimalVocab()
        {
            var vocab = new Dictionary<string, int>
            {
                ["<unk>"] = 0,
                ["<s>"]   = 1,   // BOS
                ["</s>"]  = 2,   // EOS
                ["▁"]     = 3,   // standalone ▁ → triggers _isSentencePiece = true
                ["T"]     = 4,
                ["h"]     = 5,
                ["e"]     = 6,
                ["▁T"]    = 7,
                ["▁Th"]   = 8,
                ["▁The"]  = 9,
            };
            var reverseVocab = new List<string>
                { "<unk>", "<s>", "</s>", "▁", "T", "h", "e", "▁T", "▁Th", "▁The" };

            // Merges in priority order (lowest rank = applied first)
            var merges = new List<(string, string)>
            {
                ("▁", "T"),    // rank 0 → produces "▁T"
                ("▁T", "h"),   // rank 1 → produces "▁Th"
                ("▁Th", "e"),  // rank 2 → produces "▁The"
            };

            return (vocab, reverseVocab, merges);
        }

        [Fact]
        public void GgufBpeTokenizer_Encode_SentencePiece_UsesSpaceMarker_NotByteToken()
        {
            // Arrange: minimal LLaMA vocab that triggers _isSentencePiece = true
            var (vocab, reverseVocab, merges) = BuildLlamaMinimalVocab();
            var specialTokens = new SpecialTokens { BosTokenId = -1 };

            var tokenizer = new SmallMind.Tokenizers.Gguf.GgufBpeTokenizer(
                vocab, reverseVocab, merges, specialTokens);

            // Act: encode "The" (no leading space)
            var tokens = tokenizer.Encode("The");

            // Assert: should produce [▁The] = [9], NOT individual byte tokens
            // Without BOS configured, no BOS token added
            Assert.Single(tokens);
            Assert.Equal(9, tokens[0]); // vocab["▁The"] = 9
        }

        [Fact]
        public void GgufBpeTokenizer_Encode_SentencePiece_PrependsBos_WhenConfigured()
        {
            // Arrange
            var (vocab, reverseVocab, merges) = BuildLlamaMinimalVocab();
            var specialTokens = new SpecialTokens { BosTokenId = 1 }; // <s> = 1

            var tokenizer = new SmallMind.Tokenizers.Gguf.GgufBpeTokenizer(
                vocab, reverseVocab, merges, specialTokens);

            // Act
            var tokens = tokenizer.Encode("The");

            // Assert: BOS (1) prepended, then ▁The (9)
            Assert.Equal(2, tokens.Count);
            Assert.Equal(1, tokens[0]); // BOS = <s>
            Assert.Equal(9, tokens[1]); // ▁The
        }

        [Fact]
        public void GgufBpeTokenizer_Encode_SentencePiece_DoesNotDuplicateBos()
        {
            // Arrange: verify that if BOS is already first token, it is not duplicated
            var (vocab, reverseVocab, merges) = BuildLlamaMinimalVocab();
            var specialTokens = new SpecialTokens { BosTokenId = 1 };

            var tokenizer = new SmallMind.Tokenizers.Gguf.GgufBpeTokenizer(
                vocab, reverseVocab, merges, specialTokens);

            // Act: encode twice, check BOS appears exactly once
            var tokens = tokenizer.Encode("The");

            int bosCount = 0;
            foreach (var t in tokens)
                if (t == 1) bosCount++;
            Assert.Equal(1, bosCount);
        }

        [Fact]
        public void GgufBpeTokenizer_Encode_WithLeadingSpace_DoesNotDoubleSpaceMarker()
        {
            // Arrange
            var (vocab, reverseVocab, merges) = BuildLlamaMinimalVocab();
            var specialTokens = new SpecialTokens { BosTokenId = -1 };

            var tokenizer = new SmallMind.Tokenizers.Gguf.GgufBpeTokenizer(
                vocab, reverseVocab, merges, specialTokens);

            // Act: text with a leading space
            var tokensNoLeadSpace = tokenizer.Encode("The");
            var tokensLeadSpace   = tokenizer.Encode(" The");

            // Assert: both should produce the same result (▁The = 9)
            // " The" → "▁The" (space replaced), already starts with ▁ → no double ▁
            Assert.Equal(tokensNoLeadSpace, tokensLeadSpace);
        }

        [Fact]
        public void GgufBpeTokenizer_Decode_SkipsBos_WhenBosConfigured()
        {
            // Arrange
            var (vocab, reverseVocab, merges) = BuildLlamaMinimalVocab();
            var specialTokens = new SpecialTokens { BosTokenId = 1 };

            var tokenizer = new SmallMind.Tokenizers.Gguf.GgufBpeTokenizer(
                vocab, reverseVocab, merges, specialTokens);

            // Act: decode [BOS, ▁The] — BOS should be stripped, "▁The" → " The"
            var decoded = tokenizer.Decode(new List<int> { 1, 9 });

            // Assert: output starts with " The" (no <s> prefix)
            Assert.Equal(" The", decoded);
        }

        [Fact]
        public void GgufBpeTokenizer_Decode_DoesNotSkipBos_WhenBosNotConfigured()
        {
            // Arrange: tokenizer without BOS configured
            var (vocab, reverseVocab, merges) = BuildLlamaMinimalVocab();
            var specialTokens = new SpecialTokens { BosTokenId = -1 };

            var tokenizer = new SmallMind.Tokenizers.Gguf.GgufBpeTokenizer(
                vocab, reverseVocab, merges, specialTokens);

            // Act: token 1 is "<s>" in reverseVocab — but BosTokenId = -1, so it's not skipped
            var decoded = tokenizer.Decode(new List<int> { 1, 9 });

            // Assert: "<s>" is included + " The"
            Assert.Equal("<s> The", decoded);
        }

        [Fact]
        public void GgufBpeTokenizer_EncodeDecodeRoundtrip_SentencePiece_IsCoherent()
        {
            // Full round-trip: encode produces valid token IDs, decode produces original text.
            // This mirrors what happens in run-gguf with a TinyLlama-style prompt.
            var (vocab, reverseVocab, merges) = BuildLlamaMinimalVocab();
            var specialTokens = new SpecialTokens { BosTokenId = 1, EosTokenId = 2 };

            var tokenizer = new SmallMind.Tokenizers.Gguf.GgufBpeTokenizer(
                vocab, reverseVocab, merges, specialTokens);

            // Encode "The"
            var encoded = tokenizer.Encode("The");
            // Expected: [BOS=1, ▁The=9]
            Assert.Equal(2, encoded.Count);
            Assert.Equal(1, encoded[0]);
            Assert.Equal(9, encoded[1]);

            // Decode: BOS stripped, ▁The → " The"
            var decoded = tokenizer.Decode(encoded);
            Assert.Equal(" The", decoded);

            // Coherence check: decoded text has alphabetic chars and reasonable spacing
            int alphaCount = decoded.Count(char.IsLetter);
            int spaceCount = decoded.Count(char.IsWhiteSpace);
            Assert.True((double)alphaCount / decoded.Length >= 0.4, "alphaPct < 40%");
            Assert.InRange((double)spaceCount / decoded.Length, 0.05, 0.40);
        }
    }
}
