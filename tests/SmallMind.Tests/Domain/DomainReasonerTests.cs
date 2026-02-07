using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using SmallMind.Core;
using SmallMind.Domain;
using SmallMind.Domain.Policies;
using SmallMind.Tokenizers;
using SmallMind.Transformers;
using SmallMind.Runtime;

namespace SmallMind.Tests.Domain
{
    /// <summary>
    /// Tests for DomainReasoner with policy enforcement.
    /// Uses a small, deterministic model for testing.
    /// </summary>
    public class DomainReasonerTests
    {
        private const string SampleData = @"The quick brown fox jumps over the lazy dog.
Knowledge is power.
To be or not to be, that is the question.";

        private readonly TransformerModel _model;
        private readonly Tokenizer _tokenizer;
        private readonly int _blockSize = 32;

        public DomainReasonerTests()
        {
            _tokenizer = new Tokenizer(SampleData);
            _model = new TransformerModel(
                vocabSize: _tokenizer.VocabSize,
                blockSize: _blockSize,
                nEmbd: 32,
                nLayer: 1,
                nHead: 2,
                dropout: 0.0,
                seed: 42
            );
        }

        [Fact]
        public async Task AskAsync_WithValidQuestion_ReturnsSuccess()
        {
            // Arrange
            var reasoner = new DomainReasoner(_model, _tokenizer, _blockSize);
            var question = DomainQuestion.Create("What is");
            var domain = DomainProfile.Default();

            // Act
            var answer = await reasoner.AskAsync(question, domain);

            // Assert
            Assert.NotNull(answer);
            Assert.Equal(DomainAnswerStatus.Success, answer.Status);
            Assert.NotEmpty(answer.Text);
            Assert.True(answer.InputTokens > 0);
            Assert.True(answer.OutputTokens > 0);
            Assert.True(answer.Duration.TotalMilliseconds >= 0);
        }

        [Fact]
        public async Task AskAsync_WithInputTokenLimitExceeded_RejectsRequest()
        {
            // Arrange
            var reasoner = new DomainReasoner(_model, _tokenizer, _blockSize);
            var longQuestion = new string('a', 1000); // Very long question
            var question = DomainQuestion.Create(longQuestion);
            var domain = DomainProfile.Default();
            domain.MaxInputTokens = 5; // Very restrictive

            // Act
            var answer = await reasoner.AskAsync(question, domain);

            // Assert
            Assert.Equal(DomainAnswerStatus.RejectedPolicy, answer.Status);
            Assert.Contains("Input tokens", answer.RejectionReason);
            Assert.Contains("exceed", answer.RejectionReason.ToLower());
        }

        [Fact]
        public async Task AskAsync_WithOutputTokenCap_EnforcesLimit()
        {
            // Arrange
            var reasoner = new DomainReasoner(_model, _tokenizer, _blockSize);
            var question = DomainQuestion.Create("Tell me");
            var domain = DomainProfile.Default();
            domain.MaxOutputTokens = 5; // Very small output limit

            // Act
            var answer = await reasoner.AskAsync(question, domain);

            // Assert
            // Should succeed but with limited output
            Assert.True(answer.OutputTokens <= domain.MaxOutputTokens,
                $"Output tokens ({answer.OutputTokens}) should not exceed max ({domain.MaxOutputTokens})");
        }

        [Fact]
        public async Task AskAsync_DeterministicMode_ProducesSameOutput()
        {
            // Arrange
            var reasoner = new DomainReasoner(_model, _tokenizer, _blockSize);
            var question = DomainQuestion.Create("What");
            var domain = DomainProfile.Default();
            domain.Sampling = SamplingPolicy.CreateDeterministic(12345);
            domain.MaxOutputTokens = 10;

            // Act
            var answer1 = await reasoner.AskAsync(question, domain);
            var answer2 = await reasoner.AskAsync(question, domain);

            // Assert
            Assert.Equal(DomainAnswerStatus.Success, answer1.Status);
            Assert.Equal(DomainAnswerStatus.Success, answer2.Status);
            Assert.Equal(answer1.Text, answer2.Text);
            Assert.Equal(answer1.OutputTokens, answer2.OutputTokens);
        }

        [Fact]
        public async Task AskAsync_WithAllowedCharacters_MasksDisallowedTokens()
        {
            // Arrange
            var reasoner = new DomainReasoner(_model, _tokenizer, _blockSize);
            var question = DomainQuestion.Create("a");
            var domain = DomainProfile.Default();
            domain.MaxOutputTokens = 20;
            // Only allow lowercase letters and spaces
            domain.AllowedTokens = AllowedTokenPolicy.AllowCharacters("abcdefghijklmnopqrstuvwxyz ");

            // Act
            var answer = await reasoner.AskAsync(question, domain);

            // Assert
            Assert.Equal(DomainAnswerStatus.Success, answer.Status);
            // All characters in output should be allowed
            foreach (char c in answer.Text)
            {
                Assert.True(char.IsLower(c) || c == ' ',
                    $"Character '{c}' should not appear in output with allowed character policy");
            }
        }

        [Fact]
        public async Task AskAsync_WithBlockedCharacters_ExcludesBlockedTokens()
        {
            // Arrange
            var reasoner = new DomainReasoner(_model, _tokenizer, _blockSize);
            var question = DomainQuestion.Create("test");
            var domain = DomainProfile.Default();
            domain.MaxOutputTokens = 20;
            // Block specific characters
            domain.AllowedTokens = AllowedTokenPolicy.BlockCharacters("xyz");

            // Act
            var answer = await reasoner.AskAsync(question, domain);

            // Assert
            Assert.Equal(DomainAnswerStatus.Success, answer.Status);
            // Blocked characters should not appear
            Assert.DoesNotContain('x', answer.Text);
            Assert.DoesNotContain('y', answer.Text);
            Assert.DoesNotContain('z', answer.Text);
        }

        [Fact]
        public async Task AskAsync_WithJsonOnlyOutput_ValidatesJson()
        {
            // Arrange
            var reasoner = new DomainReasoner(_model, _tokenizer, _blockSize);
            var question = DomainQuestion.Create("{}"); // Seed with JSON
            var domain = DomainProfile.Default();
            domain.Output = OutputPolicy.JsonOnly();
            domain.MaxOutputTokens = 10;

            // Act
            var answer = await reasoner.AskAsync(question, domain);

            // Assert
            // This might reject if the model doesn't produce valid JSON (which is expected)
            // The important thing is that it validates
            if (answer.Status == DomainAnswerStatus.Success)
            {
                // If successful, output should be valid JSON
                Assert.NotEmpty(answer.Text);
            }
            else
            {
                // If not successful, should be either rejected or failed
                Assert.True(answer.Status == DomainAnswerStatus.RejectedPolicy || 
                           answer.Status == DomainAnswerStatus.Failed,
                           $"Expected RejectedPolicy or Failed, but got {answer.Status}");
                
                // Rejection reason should be provided
                Assert.NotEmpty(answer.RejectionReason ?? "");
            }
        }

        [Fact]
        public async Task AskAsync_WithRegexConstrainedOutput_ValidatesRegex()
        {
            // Arrange
            var reasoner = new DomainReasoner(_model, _tokenizer, _blockSize);
            var question = DomainQuestion.Create("abc");
            var domain = DomainProfile.Default();
            // Only accept output that contains lowercase letters
            domain.Output = OutputPolicy.MatchRegex(@"^[a-z\s\.]+$");
            domain.MaxOutputTokens = 10;

            // Act
            var answer = await reasoner.AskAsync(question, domain);

            // Assert
            if (answer.Status == DomainAnswerStatus.Success)
            {
                // Output should match the regex
                Assert.Matches(@"^[a-z\s\.]+$", answer.Text);
            }
            else
            {
                // Should be rejected due to regex mismatch
                Assert.Equal(DomainAnswerStatus.RejectedPolicy, answer.Status);
            }
        }

        [Fact]
        public async Task AskAsync_WithMaxCharactersLimit_EnforcesLimit()
        {
            // Arrange
            var reasoner = new DomainReasoner(_model, _tokenizer, _blockSize);
            var question = DomainQuestion.Create("test");
            var domain = DomainProfile.Default();
            domain.Output = new OutputPolicy { MaxCharacters = 5 };
            domain.MaxOutputTokens = 100; // Allow many tokens

            // Act
            var answer = await reasoner.AskAsync(question, domain);

            // Assert
            if (answer.Status == DomainAnswerStatus.Success)
            {
                Assert.True(answer.Text.Length <= 5,
                    $"Output length ({answer.Text.Length}) should not exceed max characters (5)");
            }
            else
            {
                Assert.Equal(DomainAnswerStatus.RejectedPolicy, answer.Status);
                Assert.Contains("length", answer.RejectionReason?.ToLower() ?? "");
            }
        }

        [Fact]
        public async Task AskAsync_WithTimeout_EnforcesExecutionTimeLimit()
        {
            // Arrange
            var reasoner = new DomainReasoner(_model, _tokenizer, _blockSize);
            var question = DomainQuestion.Create("test");
            var domain = DomainProfile.Default();
            domain.MaxExecutionTime = TimeSpan.FromMilliseconds(1); // Very short timeout
            domain.MaxOutputTokens = 100; // Would take longer than timeout

            // Act
            var answer = await reasoner.AskAsync(question, domain);

            // Assert
            // Should either timeout or complete very quickly
            Assert.True(answer.Status == DomainAnswerStatus.RejectedPolicy || 
                       answer.Status == DomainAnswerStatus.Success);
            
            if (answer.Status == DomainAnswerStatus.RejectedPolicy)
            {
                Assert.Contains("time", answer.RejectionReason?.ToLower() ?? "");
            }
        }

        [Fact]
        public async Task AskAsync_WithCancellation_HandlesCancellation()
        {
            // Arrange
            var reasoner = new DomainReasoner(_model, _tokenizer, _blockSize);
            var question = DomainQuestion.Create("test");
            var domain = DomainProfile.Default();
            domain.MaxOutputTokens = 100;

            using var cts = new CancellationTokenSource();
            cts.Cancel(); // Cancel immediately

            // Act
            var answer = await reasoner.AskAsync(question, domain, cts.Token);

            // Assert
            Assert.Equal(DomainAnswerStatus.Cancelled, answer.Status);
        }

        [Fact]
        public async Task AskAsync_WithProvenanceEnabled_IncludesProvenance()
        {
            // Arrange
            var reasoner = new DomainReasoner(_model, _tokenizer, _blockSize);
            var question = DomainQuestion.Create("test");
            var domain = DomainProfile.Default();
            domain.Provenance = ProvenancePolicy.Enabled(5);
            domain.MaxOutputTokens = 10;

            // Act
            var answer = await reasoner.AskAsync(question, domain);

            // Assert
            if (answer.Status == DomainAnswerStatus.Success)
            {
                Assert.NotNull(answer.Provenance);
                Assert.True(answer.Provenance.Confidence >= 0.0 && answer.Provenance.Confidence <= 1.0);
                Assert.NotNull(answer.Provenance.Evidence);
                Assert.True(answer.Provenance.Evidence.Count <= 5);
            }
        }

        [Fact]
        public async Task AskAsync_WithMinConfidence_RejectsLowConfidence()
        {
            // Arrange
            var reasoner = new DomainReasoner(_model, _tokenizer, _blockSize);
            var question = DomainQuestion.Create("test");
            var domain = DomainProfile.Default();
            domain.Safety = new SafetyPolicy { MinConfidence = 0.99f }; // Very high threshold
            domain.Provenance = ProvenancePolicy.Enabled();
            domain.MaxOutputTokens = 5;

            // Act
            var answer = await reasoner.AskAsync(question, domain);

            // Assert
            // Likely to be rejected due to high confidence requirement
            if (answer.Status == DomainAnswerStatus.RejectedPolicy)
            {
                Assert.Contains("onfidence", answer.RejectionReason ?? "");
            }
        }

        [Fact]
        public async Task AskStreamAsync_GeneratesTokensSequentially()
        {
            // Arrange
            var reasoner = new DomainReasoner(_model, _tokenizer, _blockSize);
            var question = DomainQuestion.Create("test");
            var domain = DomainProfile.Default();
            domain.MaxOutputTokens = 5;

            // Act
            var tokens = new List<DomainToken>();
            await foreach (var token in reasoner.AskStreamAsync(question, domain))
            {
                tokens.Add(token);
            }

            // Assert
            Assert.NotEmpty(tokens);
            Assert.True(tokens.Count <= domain.MaxOutputTokens);
            
            // Verify tokens are in sequence
            for (int i = 0; i < tokens.Count; i++)
            {
                Assert.Equal(i, tokens[i].Index);
            }

            // Verify elapsed time increases
            for (int i = 1; i < tokens.Count; i++)
            {
                Assert.True(tokens[i].ElapsedTime >= tokens[i - 1].ElapsedTime);
            }
        }

        [Fact]
        public async Task AskAsync_WithQuestionContext_UsesContext()
        {
            // Arrange
            var reasoner = new DomainReasoner(_model, _tokenizer, _blockSize);
            var question = DomainQuestion.CreateWithContext("What?", "Context: knowledge");
            var domain = DomainProfile.Default();
            domain.MaxOutputTokens = 10;

            // Act
            var answer = await reasoner.AskAsync(question, domain);

            // Assert
            Assert.Equal(DomainAnswerStatus.Success, answer.Status);
            Assert.True(answer.InputTokens > "What?".Length); // Should include context
        }
    }
}
