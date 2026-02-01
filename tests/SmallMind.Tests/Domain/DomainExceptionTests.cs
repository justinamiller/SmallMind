using System;
using Xunit;
using SmallMind.Domain;

namespace SmallMind.Tests.Domain
{
    /// <summary>
    /// Tests for domain-specific exceptions.
    /// </summary>
    public class DomainExceptionTests
    {
        [Fact]
        public void DomainPolicyViolationException_StoresProperties()
        {
            // Arrange
            var message = "Test policy violation";
            var policyName = "TestPolicy";
            var violatingValue = 42;

            // Act
            var exception = new DomainPolicyViolationException(message, policyName, violatingValue);

            // Assert
            Assert.Equal(message, exception.Message);
            Assert.Equal(policyName, exception.PolicyName);
            Assert.Equal(violatingValue, exception.ViolatingValue);
            Assert.Equal("DOMAIN_POLICY_VIOLATION", exception.ErrorCode);
        }

        [Fact]
        public void DomainPolicyViolationException_WithInnerException_StoresInnerException()
        {
            // Arrange
            var message = "Test policy violation";
            var policyName = "TestPolicy";
            var violatingValue = 42;
            var innerException = new InvalidOperationException("Inner error");

            // Act
            var exception = new DomainPolicyViolationException(message, policyName, violatingValue, innerException);

            // Assert
            Assert.Equal(message, exception.Message);
            Assert.Equal(policyName, exception.PolicyName);
            Assert.Equal(violatingValue, exception.ViolatingValue);
            Assert.Equal(innerException, exception.InnerException);
        }

        [Fact]
        public void OutOfDomainException_StoresProperties()
        {
            // Arrange
            var message = "Content rejected";
            var rejectedContent = "test content";
            var reason = "Not in domain";

            // Act
            var exception = new OutOfDomainException(message, rejectedContent, reason);

            // Assert
            Assert.Equal(message, exception.Message);
            Assert.Equal(rejectedContent, exception.RejectedContent);
            Assert.Equal(reason, exception.Reason);
            Assert.Equal("OUT_OF_DOMAIN", exception.ErrorCode);
        }

        [Fact]
        public void OutOfDomainException_WithInnerException_StoresInnerException()
        {
            // Arrange
            var message = "Content rejected";
            var rejectedContent = "test content";
            var reason = "Not in domain";
            var innerException = new InvalidOperationException("Inner error");

            // Act
            var exception = new OutOfDomainException(message, rejectedContent, reason, innerException);

            // Assert
            Assert.Equal(message, exception.Message);
            Assert.Equal(rejectedContent, exception.RejectedContent);
            Assert.Equal(reason, exception.Reason);
            Assert.Equal(innerException, exception.InnerException);
        }
    }
}
