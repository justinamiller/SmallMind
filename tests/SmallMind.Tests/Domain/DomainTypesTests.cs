using System;
using System.Collections.Generic;
using Xunit;
using SmallMind.Domain;
using SmallMind.Exceptions;

namespace SmallMind.Tests.Domain
{
    /// <summary>
    /// Tests for domain question and answer types.
    /// </summary>
    public class DomainTypesTests
    {
        #region DomainQuestion Tests

        [Fact]
        public void DomainQuestion_Create_SetsQueryAndGeneratesRequestId()
        {
            // Act
            var question = DomainQuestion.Create("What is AI?");

            // Assert
            Assert.Equal("What is AI?", question.Query);
            Assert.NotNull(question.RequestId);
            Assert.NotEmpty(question.RequestId);
        }

        [Fact]
        public void DomainQuestion_CreateWithContext_SetsQueryAndContext()
        {
            // Act
            var question = DomainQuestion.CreateWithContext("What?", "Context here");

            // Assert
            Assert.Equal("What?", question.Query);
            Assert.Equal("Context here", question.Context);
            Assert.NotNull(question.RequestId);
        }

        [Fact]
        public void DomainQuestion_Validate_WithValidQuestion_DoesNotThrow()
        {
            // Arrange
            var question = DomainQuestion.Create("test");

            // Act & Assert
            question.Validate(); // Should not throw
        }

        [Fact]
        public void DomainQuestion_Validate_WithEmptyQuery_ThrowsValidationException()
        {
            // Arrange
            var question = new DomainQuestion { Query = "" };

            // Act & Assert
            Assert.Throws<ValidationException>(() => question.Validate());
        }

        [Fact]
        public void DomainQuestion_Validate_GeneratesRequestIdIfMissing()
        {
            // Arrange
            var question = new DomainQuestion { Query = "test", RequestId = null };

            // Act
            question.Validate();

            // Assert
            Assert.NotNull(question.RequestId);
            Assert.NotEmpty(question.RequestId);
        }

        #endregion

        #region DomainAnswer Tests

        [Fact]
        public void DomainAnswer_Success_CreatesSuccessfulAnswer()
        {
            // Arrange
            var text = "Generated text";
            var duration = TimeSpan.FromSeconds(1);
            var inputTokens = 10;
            var outputTokens = 20;

            // Act
            var answer = DomainAnswer.Success(text, duration, inputTokens, outputTokens);

            // Assert
            Assert.Equal(text, answer.Text);
            Assert.Equal(DomainAnswerStatus.Success, answer.Status);
            Assert.Equal(duration, answer.Duration);
            Assert.Equal(inputTokens, answer.InputTokens);
            Assert.Equal(outputTokens, answer.OutputTokens);
            Assert.Null(answer.RejectionReason);
        }

        [Fact]
        public void DomainAnswer_Rejected_CreatesRejectedAnswer()
        {
            // Arrange
            var status = DomainAnswerStatus.RejectedPolicy;
            var reason = "Policy violation";
            var duration = TimeSpan.FromMilliseconds(100);

            // Act
            var answer = DomainAnswer.Rejected(status, reason, duration);

            // Assert
            Assert.Equal(status, answer.Status);
            Assert.Equal(reason, answer.RejectionReason);
            Assert.Equal(duration, answer.Duration);
        }

        [Fact]
        public void DomainAnswer_Failed_CreatesFailedAnswer()
        {
            // Arrange
            var reason = "Internal error";
            var duration = TimeSpan.FromMilliseconds(50);

            // Act
            var answer = DomainAnswer.Failed(reason, duration);

            // Assert
            Assert.Equal(DomainAnswerStatus.Failed, answer.Status);
            Assert.Equal(reason, answer.RejectionReason);
            Assert.Equal(duration, answer.Duration);
        }

        #endregion

        #region DomainToken Tests

        [Fact]
        public void DomainToken_Create_SetsAllProperties()
        {
            // Arrange
            var text = "token";
            var tokenId = 42;
            var index = 5;
            var elapsed = TimeSpan.FromSeconds(1);
            var probability = 0.95f;

            // Act
            var token = DomainToken.Create(text, tokenId, index, elapsed, probability);

            // Assert
            Assert.Equal(text, token.Text);
            Assert.Equal(tokenId, token.TokenId);
            Assert.Equal(index, token.Index);
            Assert.Equal(elapsed, token.ElapsedTime);
            Assert.Equal(probability, token.Probability);
        }

        #endregion

        #region DomainProvenance Tests

        [Fact]
        public void DomainProvenance_Create_SetsPropertiesCorrectly()
        {
            // Arrange
            var confidence = 0.85;
            var evidence = new List<DomainEvidenceItem>
            {
                DomainEvidenceItem.Create(1, "test", 0.9f, 0),
                DomainEvidenceItem.Create(2, "data", 0.8f, 1)
            };

            // Act
            var provenance = DomainProvenance.Create(confidence, evidence);

            // Assert
            Assert.Equal(confidence, provenance.Confidence);
            Assert.Equal(2, provenance.Evidence.Count);
        }

        [Fact]
        public void DomainProvenance_Create_WithNullEvidence_UsesEmptyList()
        {
            // Act
            var provenance = DomainProvenance.Create(0.5, null!);

            // Assert
            Assert.NotNull(provenance.Evidence);
            Assert.Empty(provenance.Evidence);
        }

        #endregion

        #region DomainEvidenceItem Tests

        [Fact]
        public void DomainEvidenceItem_Create_SetsPropertiesCorrectly()
        {
            // Arrange
            var tokenId = 42;
            var tokenText = "test";
            var probability = 0.95f;
            var stepIndex = 10;

            // Act
            var evidence = DomainEvidenceItem.Create(tokenId, tokenText, probability, stepIndex);

            // Assert
            Assert.Equal(tokenId, evidence.TokenId);
            Assert.Equal(tokenText, evidence.TokenText);
            Assert.Equal(probability, evidence.Probability);
            Assert.Equal(stepIndex, evidence.StepIndex);
        }

        #endregion
    }
}
