using System;
using Xunit;
using SmallMind.Domain;
using SmallMind.Domain.Policies;
using SmallMind.Core.Exceptions;

namespace SmallMind.Tests.Domain
{
    /// <summary>
    /// Tests for DomainProfile validation and configuration.
    /// </summary>
    public class DomainProfileTests
    {
        [Fact]
        public void Default_CreatesValidProfile()
        {
            // Act
            var profile = DomainProfile.Default();

            // Assert
            Assert.NotNull(profile);
            Assert.Equal("Default", profile.Name);
            Assert.Equal("1.0", profile.Version);
            Assert.Equal(512, profile.MaxInputTokens);
            Assert.Equal(256, profile.MaxOutputTokens);
            Assert.NotNull(profile.MaxExecutionTime);
            Assert.NotNull(profile.AllowedTokens);
            Assert.NotNull(profile.Output);
            Assert.NotNull(profile.Sampling);
            Assert.NotNull(profile.Provenance);
            Assert.NotNull(profile.Safety);
        }

        [Fact]
        public void Validate_WithValidProfile_DoesNotThrow()
        {
            // Arrange
            var profile = DomainProfile.Default();

            // Act & Assert
            profile.Validate(); // Should not throw
        }

        [Fact]
        public void Validate_WithNullName_ThrowsValidationException()
        {
            // Arrange
            var profile = DomainProfile.Default();
            profile.Name = null!;

            // Act & Assert
            Assert.Throws<ValidationException>(() => profile.Validate());
        }

        [Fact]
        public void Validate_WithZeroMaxInputTokens_ThrowsValidationException()
        {
            // Arrange
            var profile = DomainProfile.Default();
            profile.MaxInputTokens = 0;

            // Act & Assert
            Assert.Throws<ValidationException>(() => profile.Validate());
        }

        [Fact]
        public void Validate_WithNegativeMaxOutputTokens_ThrowsValidationException()
        {
            // Arrange
            var profile = DomainProfile.Default();
            profile.MaxOutputTokens = -1;

            // Act & Assert
            Assert.Throws<ValidationException>(() => profile.Validate());
        }

        [Fact]
        public void Validate_WithRequireCitationsButProvenanceDisabled_ThrowsDomainPolicyViolationException()
        {
            // Arrange
            var profile = DomainProfile.Default();
            profile.Safety = new SafetyPolicy { RequireCitations = true };
            profile.Provenance = new ProvenancePolicy { EnableProvenance = false };

            // Act & Assert
            var ex = Assert.Throws<DomainPolicyViolationException>(() => profile.Validate());
            Assert.Contains("provenance", ex.Message.ToLower());
        }

        [Fact]
        public void Strict_CreatesStrictProfile()
        {
            // Act
            var profile = DomainProfile.Strict();

            // Assert
            Assert.Equal("Strict", profile.Name);
            Assert.Equal(256, profile.MaxInputTokens);
            Assert.Equal(128, profile.MaxOutputTokens);
            Assert.True(profile.Sampling.Deterministic);
            Assert.True(profile.Safety.RejectOutOfDomain);
            Assert.True(profile.Provenance.EnableProvenance);
        }

        [Fact]
        public void JsonOutput_CreatesJsonProfile()
        {
            // Act
            var profile = DomainProfile.JsonOutput();

            // Assert
            Assert.Equal("JsonOutput", profile.Name);
            Assert.Equal(OutputFormat.JsonOnly, profile.Output.Format);
            Assert.True(profile.Sampling.Deterministic);
        }

        [Fact]
        public void Permissive_CreatesPermissiveProfile()
        {
            // Act
            var profile = DomainProfile.Permissive();

            // Assert
            Assert.Equal("Permissive", profile.Name);
            Assert.Equal(2048, profile.MaxInputTokens);
            Assert.Equal(1024, profile.MaxOutputTokens);
            Assert.False(profile.Safety.RejectOutOfDomain);
        }
    }
}
