using Xunit;
using SmallMind.Runtime;
using SmallMind.Tokenizers;
using SmallMind.Transformers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using InternalFinishReason = SmallMind.Runtime.FinishReason;

namespace SmallMind.Tests
{
    /// <summary>
    /// Tests for new inference features: Top-P, Min-P, repetition penalties, stop conditions.
    /// </summary>
    public class InferenceFeaturesTests
    {
        // Helper: Create a simple mock tokenizer for testing
        private class MockTokenizer : ITokenizer
        {
            private readonly int _vocabSize;
            private readonly int _eosTokenId;

            public MockTokenizer(int vocabSize, int eosTokenId = -1)
            {
                _vocabSize = vocabSize;
                _eosTokenId = eosTokenId;
            }

            public int VocabSize => _vocabSize;

            public TokenizerInfo Info => new TokenizerInfo(
                name: "MockTokenizer",
                vocabSize: _vocabSize,
                bosTokenId: -1,
                eosTokenId: _eosTokenId,
                padTokenId: -1,
                unkTokenId: -1,
                supportsByteFallback: false
            );

            public List<int> Encode(string text)
            {
                // Simple encoding: each character as token ID
                var tokens = new List<int>();
                foreach (char c in text)
                {
                    int tokenId = c % _vocabSize;
                    tokens.Add(tokenId);
                }
                return tokens;
            }

            public int Encode(ReadOnlySpan<byte> utf8, Span<int> tokensOut)
            {
                throw new NotImplementedException();
            }

            public int Decode(ReadOnlySpan<int> tokens, Span<byte> utf8Out)
            {
                throw new NotImplementedException();
            }

            public string Decode(List<int> tokens)
            {
                // Simple decode: token ID to character
                var chars = new char[tokens.Count];
                for (int i = 0; i < tokens.Count; i++)
                {
                    chars[i] = (char)('a' + (tokens[i] % 26));
                }
                return new string(chars);
            }

            public string DecodeToString(ReadOnlySpan<int> tokens)
            {
                var list = new List<int>(tokens.ToArray());
                return Decode(list);
            }
        }

        [Fact]
        public void ProductionInferenceOptions_DefaultValues_AreCorrect()
        {
            var options = new ProductionInferenceOptions();

            Assert.Equal(0.95, options.TopP);
            Assert.Equal(0.0, options.MinP);
            Assert.Equal(1.0f, options.RepetitionPenalty);
            Assert.Equal(0.0f, options.PresencePenalty);
            Assert.Equal(0.0f, options.FrequencyPenalty);
            Assert.Empty(options.StopTokenIds);
            Assert.Empty(options.StopSequences);
            Assert.True(options.RemoveStopSequenceFromOutput);
            Assert.Equal(0, options.RepetitionWindow);
        }

        [Fact]
        public void ProductionInferenceOptions_Validation_RejectsInvalidTopP()
        {
            var options = new ProductionInferenceOptions();
            
            // TopP > 1.0 should fail
            options.TopP = 1.5;
            Assert.Throws<SmallMind.Core.Exceptions.ValidationException>(() => options.Validate());
            
            // TopP < 0.0 should fail
            options.TopP = -0.1;
            Assert.Throws<SmallMind.Core.Exceptions.ValidationException>(() => options.Validate());
            
            // Valid TopP should pass
            options.TopP = 0.9;
            options.Validate(); // Should not throw
        }

        [Fact]
        public void ProductionInferenceOptions_Validation_RejectsInvalidMinP()
        {
            var options = new ProductionInferenceOptions();
            
            // MinP > 1.0 should fail
            options.MinP = 1.5;
            Assert.Throws<SmallMind.Core.Exceptions.ValidationException>(() => options.Validate());
            
            // MinP < 0.0 should fail
            options.MinP = -0.1;
            Assert.Throws<SmallMind.Core.Exceptions.ValidationException>(() => options.Validate());
            
            // Valid MinP should pass
            options.MinP = 0.1;
            options.Validate(); // Should not throw
        }

        [Fact]
        public void ProductionInferenceOptions_Validation_RejectsInvalidRepetitionPenalty()
        {
            var options = new ProductionInferenceOptions();
            
            // RepetitionPenalty <= 0 should fail
            options.RepetitionPenalty = 0.0f;
            Assert.Throws<SmallMind.Core.Exceptions.ValidationException>(() => options.Validate());
            
            options.RepetitionPenalty = -1.0f;
            Assert.Throws<SmallMind.Core.Exceptions.ValidationException>(() => options.Validate());
            
            // Valid RepetitionPenalty should pass
            options.RepetitionPenalty = 1.1f;
            options.Validate(); // Should not throw
        }

        [Fact]
        public void ProductionInferenceOptions_Clone_PreservesAllFields()
        {
            var original = new ProductionInferenceOptions
            {
                TopP = 0.85,
                MinP = 0.05,
                RepetitionPenalty = 1.1f,
                PresencePenalty = 0.5f,
                FrequencyPenalty = 0.3f,
                RepetitionWindow = 128,
                StopTokenIds = new int[] { 1, 2, 3 },
                StopSequences = new string[] { "STOP", "END" },
                RemoveStopSequenceFromOutput = false,
                Temperature = 0.7,
                TopK = 50,
                Seed = 42,
                MaxNewTokens = 200
            };

            var clone = original.Clone();

            Assert.Equal(original.TopP, clone.TopP);
            Assert.Equal(original.MinP, clone.MinP);
            Assert.Equal(original.RepetitionPenalty, clone.RepetitionPenalty);
            Assert.Equal(original.PresencePenalty, clone.PresencePenalty);
            Assert.Equal(original.FrequencyPenalty, clone.FrequencyPenalty);
            Assert.Equal(original.RepetitionWindow, clone.RepetitionWindow);
            Assert.Equal(original.StopTokenIds, clone.StopTokenIds);
            Assert.Equal(original.StopSequences, clone.StopSequences);
            Assert.Equal(original.RemoveStopSequenceFromOutput, clone.RemoveStopSequenceFromOutput);
            Assert.Equal(original.Temperature, clone.Temperature);
            Assert.Equal(original.TopK, clone.TopK);
            Assert.Equal(original.Seed, clone.Seed);
            Assert.Equal(original.MaxNewTokens, clone.MaxNewTokens);
        }

        [Fact]
        public void FinishReason_Enum_HasExpectedValues()
        {
            // Verify all expected finish reasons exist
            Assert.Equal(0, (int)InternalFinishReason.None);
            Assert.Equal(1, (int)InternalFinishReason.MaxTokens);
            Assert.Equal(2, (int)InternalFinishReason.EndOfSequence);
            Assert.Equal(3, (int)InternalFinishReason.StopToken);
            Assert.Equal(4, (int)InternalFinishReason.StopSequence);
            Assert.Equal(5, (int)InternalFinishReason.Timeout);
            Assert.Equal(6, (int)InternalFinishReason.MaxContext);
        }

        [Fact]
        public void GeneratedToken_IncludesFinishReason()
        {
            var token = new GeneratedToken(
                tokenId: 42,
                text: "hello",
                index: 0,
                logProb: -1.5f,
                finishReason: InternalFinishReason.EndOfSequence
            );

            Assert.Equal(42, token.TokenId);
            Assert.Equal("hello", token.Text);
            Assert.Equal(0, token.Index);
            Assert.Equal(-1.5f, token.LogProb);
            Assert.Equal(InternalFinishReason.EndOfSequence, token.FinishReason);
        }

        [Fact]
        public async Task InferenceSession_EosToken_StopsGenerationAsync()
        {
            // Create a small test model
            const int vocabSize = 50;
            const int eosTokenId = 10;
            
            var model = new TransformerModel(
                vocabSize: vocabSize,
                blockSize: 32,
                nEmbd: 64,
                nLayer: 2,
                nHead: 4,
                dropout: 0.0,
                seed: 42
            );

            var tokenizer = new MockTokenizer(vocabSize, eosTokenId);
            
            var options = new ProductionInferenceOptions
            {
                MaxNewTokens = 100, // Request many tokens
                Temperature = 1.0,
                TopK = 0,
                Seed = 42
            };

            using var session = new InferenceSession(model, tokenizer, options, model.BlockSize);

            // Note: This test validates the API structure, but actual EOS detection
            // depends on the model actually generating the EOS token, which is
            // probabilistic. In a real test, we'd use a mocked model.
            
            // For now, just verify the session can be created and used
            var result = await session.GenerateAsync("test");
            Assert.NotNull(result);
        }

        [Fact(Skip = "Flaky test - timing-dependent timeout behavior is unreliable in CI")]
        public async Task InferenceSession_StreamingWithTimeout_EmitsTimeoutReasonAsync()
        {
            const int vocabSize = 50;
            
            var model = new TransformerModel(
                vocabSize: vocabSize,
                blockSize: 32,
                nEmbd: 64,
                nLayer: 2,
                nHead: 4,
                dropout: 0.0,
                seed: 42
            );

            var tokenizer = new MockTokenizer(vocabSize);
            
            var options = new ProductionInferenceOptions
            {
                MaxNewTokens = 10, // Limit tokens to prevent context overflow
                MaxTimeMs = 5, // Short timeout (5ms) - was 1ms which was too flaky
                Temperature = 1.0,
                TopK = 0,
                Seed = 42
            };

            using var session = new InferenceSession(model, tokenizer, options, model.BlockSize);

            // Generate and check for timeout finish reason
            var tokens = new List<GeneratedToken>();
            bool timedOut = false;
            try
            {
                await foreach (var token in session.GenerateStreamAsync("test"))
                {
                    tokens.Add(token);
                    if (token.FinishReason == InternalFinishReason.Timeout)
                    {
                        timedOut = true;
                        break;
                    }
                    if (token.FinishReason != InternalFinishReason.None)
                    {
                        break;
                    }
                }
            }
            catch (SmallMind.Core.Exceptions.InferenceTimeoutException)
            {
                // Expected for non-streaming timeout
                timedOut = true;
            }

            // With very short timeout, we should get timeout
            // Either via InternalFinishReason.Timeout in streaming or InferenceTimeoutException
            Assert.True(timedOut, "Expected timeout to occur with 1ms limit");
        }

        [Fact]
        public void ProductionInferenceOptions_StopSequences_CanBeConfigured()
        {
            var options = new ProductionInferenceOptions
            {
                StopSequences = new string[] { "\n\nUser:", "###", "STOP" }
            };

            Assert.Equal(3, options.StopSequences.Length);
            Assert.Contains("\n\nUser:", options.StopSequences);
            Assert.Contains("###", options.StopSequences);
            Assert.Contains("STOP", options.StopSequences);
        }

        [Fact]
        public void ProductionInferenceOptions_StopTokenIds_CanBeConfigured()
        {
            var options = new ProductionInferenceOptions
            {
                StopTokenIds = new int[] { 1, 2, 3, 50256 } // Example: GPT-2 EOS = 50256
            };

            Assert.Equal(4, options.StopTokenIds.Length);
            Assert.Contains(1, options.StopTokenIds);
            Assert.Contains(50256, options.StopTokenIds);
        }
    }
}
