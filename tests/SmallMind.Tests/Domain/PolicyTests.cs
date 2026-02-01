using System;
using Xunit;
using SmallMind.Domain.Policies;
using SmallMind.Exceptions;

namespace SmallMind.Tests.Domain
{
    /// <summary>
    /// Tests for policy classes.
    /// </summary>
    public class PolicyTests
    {
        #region SamplingPolicy Tests

        [Fact]
        public void SamplingPolicy_Default_HasValidDefaults()
        {
            // Act
            var policy = SamplingPolicy.Default();

            // Assert
            Assert.False(policy.Deterministic);
            Assert.Null(policy.Seed);
            Assert.Equal(0.7f, policy.Temperature);
            Assert.Equal(0, policy.TopK);
            Assert.Equal(0f, policy.TopP);
        }

        [Fact]
        public void SamplingPolicy_CreateDeterministic_SetsDeterministicMode()
        {
            // Act
            var policy = SamplingPolicy.CreateDeterministic(12345);

            // Assert
            Assert.True(policy.Deterministic);
            Assert.Equal(12345, policy.Seed);
        }

        [Fact]
        public void SamplingPolicy_Validate_WithInvalidTemperature_Throws()
        {
            // Arrange
            var policy = SamplingPolicy.Default();
            policy.Temperature = 3.0f; // Too high

            // Act & Assert
            Assert.Throws<ValidationException>(() => policy.Validate());
        }

        [Fact]
        public void SamplingPolicy_GetEffectiveTemperature_InDeterministicMode_ReturnsLowValue()
        {
            // Arrange
            var policy = SamplingPolicy.CreateDeterministic();

            // Act
            policy.Validate();
            var temp = policy.GetEffectiveTemperature();

            // Assert
            Assert.True(temp < 0.1f);
        }

        [Fact]
        public void SamplingPolicy_GetEffectiveTopK_InDeterministicMode_ReturnsOne()
        {
            // Arrange
            var policy = SamplingPolicy.CreateDeterministic();

            // Act
            policy.Validate();
            var topK = policy.GetEffectiveTopK();

            // Assert
            Assert.Equal(1, topK);
        }

        #endregion

        #region OutputPolicy Tests

        [Fact]
        public void OutputPolicy_Default_HasValidDefaults()
        {
            // Act
            var policy = OutputPolicy.Default();

            // Assert
            Assert.Equal(OutputFormat.PlainText, policy.Format);
            Assert.Equal(10_000, policy.MaxCharacters);
            Assert.Equal(2, policy.MaxRetries);
        }

        [Fact]
        public void OutputPolicy_JsonOnly_SetsJsonFormat()
        {
            // Act
            var policy = OutputPolicy.JsonOnly();

            // Assert
            Assert.Equal(OutputFormat.JsonOnly, policy.Format);
        }

        [Fact]
        public void OutputPolicy_MatchRegex_SetsRegexFormat()
        {
            // Act
            var policy = OutputPolicy.MatchRegex(@"^\d{3}-\d{2}-\d{4}$");

            // Assert
            Assert.Equal(OutputFormat.RegexConstrained, policy.Format);
            Assert.Equal(@"^\d{3}-\d{2}-\d{4}$", policy.Regex);
        }

        [Fact]
        public void OutputPolicy_Validate_WithRegexConstrainedButNoRegex_Throws()
        {
            // Arrange
            var policy = new OutputPolicy
            {
                Format = OutputFormat.RegexConstrained,
                Regex = null
            };

            // Act & Assert
            Assert.Throws<ValidationException>(() => policy.Validate());
        }

        #endregion

        #region SafetyPolicy Tests

        [Fact]
        public void SafetyPolicy_Default_HasValidDefaults()
        {
            // Act
            var policy = SafetyPolicy.Default();

            // Assert
            Assert.True(policy.RejectOutOfDomain);
            Assert.Equal(0.0f, policy.MinConfidence);
            Assert.False(policy.DisallowUnknownTokens);
            Assert.False(policy.RequireCitations);
        }

        [Fact]
        public void SafetyPolicy_Strict_EnablesAllSafetyFeatures()
        {
            // Act
            var policy = SafetyPolicy.Strict();

            // Assert
            Assert.True(policy.RejectOutOfDomain);
            Assert.Equal(0.5f, policy.MinConfidence);
            Assert.True(policy.DisallowUnknownTokens);
            Assert.True(policy.RequireCitations);
        }

        [Fact]
        public void SafetyPolicy_Permissive_DisablesSafetyFeatures()
        {
            // Act
            var policy = SafetyPolicy.Permissive();

            // Assert
            Assert.False(policy.RejectOutOfDomain);
            Assert.Equal(0.0f, policy.MinConfidence);
            Assert.False(policy.DisallowUnknownTokens);
            Assert.False(policy.RequireCitations);
        }

        #endregion

        #region ProvenancePolicy Tests

        [Fact]
        public void ProvenancePolicy_Default_HasProvenanceDisabled()
        {
            // Act
            var policy = ProvenancePolicy.Default();

            // Assert
            Assert.False(policy.EnableProvenance);
            Assert.Equal(10, policy.MaxEvidenceItems);
        }

        [Fact]
        public void ProvenancePolicy_Enabled_EnablesProvenance()
        {
            // Act
            var policy = ProvenancePolicy.Enabled(20);

            // Assert
            Assert.True(policy.EnableProvenance);
            Assert.Equal(20, policy.MaxEvidenceItems);
            Assert.True(policy.TrackTokenProbabilities);
        }

        #endregion

        #region AllowedTokenPolicy Tests

        [Fact]
        public void AllowedTokenPolicy_Default_AllowsAllTokens()
        {
            // Act
            var policy = AllowedTokenPolicy.Default();

            // Assert
            Assert.Null(policy.AllowedTokenIds);
            Assert.Null(policy.AllowedCharacters);
            Assert.Null(policy.BlockedTokenIds);
            Assert.Null(policy.BlockedCharacters);
        }

        [Fact]
        public void AllowedTokenPolicy_AllowCharacters_SetsAllowedCharacters()
        {
            // Act
            var policy = AllowedTokenPolicy.AllowCharacters("abc123");

            // Assert
            Assert.Equal("abc123", policy.AllowedCharacters);
        }

        [Fact]
        public void AllowedTokenPolicy_BlockCharacters_SetsBlockedCharacters()
        {
            // Act
            var policy = AllowedTokenPolicy.BlockCharacters("xyz");

            // Assert
            Assert.Equal("xyz", policy.BlockedCharacters);
        }

        #endregion
    }
}
