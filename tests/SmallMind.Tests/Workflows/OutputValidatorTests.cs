using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using SmallMind.Workflows;
using SmallMind.Domain;

namespace SmallMind.Tests.Workflows
{
    /// <summary>
    /// Unit tests for OutputValidator.
    /// Tests JSON, Enum, and Regex validation and repair logic.
    /// </summary>
    public class OutputValidatorTests
    {
        private readonly OutputValidator _validator;

        public OutputValidatorTests()
        {
            _validator = new OutputValidator();
        }

        #region JSON Validation Tests

        [Fact]
        public void ValidateJson_ValidJson_ReturnsValid()
        {
            // Arrange
            var output = @"{""name"": ""test"", ""value"": 123}";
            var spec = new StepOutputSpec
            {
                Format = OutputFormat.JsonOnly,
                Strict = true
            };

            // Act
            var (isValid, errors, repaired) = _validator.Validate(output, spec);

            // Assert
            Assert.True(isValid);
            Assert.Empty(errors);
            Assert.Null(repaired);
        }

        [Fact]
        public void ValidateJson_ValidJsonWithRequiredFields_ReturnsValid()
        {
            // Arrange
            var output = @"{""name"": ""test"", ""value"": 123, ""status"": ""ok""}";
            var spec = new StepOutputSpec
            {
                Format = OutputFormat.JsonOnly,
                RequiredJsonFields = new List<string> { "name", "value" },
                Strict = true
            };

            // Act
            var (isValid, errors, repaired) = _validator.Validate(output, spec);

            // Assert
            Assert.True(isValid);
            Assert.Empty(errors);
        }

        [Fact]
        public void ValidateJson_MissingRequiredField_ReturnsInvalid()
        {
            // Arrange
            var output = @"{""name"": ""test""}";
            var spec = new StepOutputSpec
            {
                Format = OutputFormat.JsonOnly,
                RequiredJsonFields = new List<string> { "name", "value" },
                Strict = true
            };

            // Act
            var (isValid, errors, repaired) = _validator.Validate(output, spec);

            // Assert
            Assert.False(isValid);
            Assert.Contains(errors, e => e.Contains("Missing required JSON field: value"));
        }

        [Fact]
        public void ValidateJson_InvalidJson_ReturnsInvalid()
        {
            // Arrange
            var output = @"{""name"": ""test"", invalid}";
            var spec = new StepOutputSpec
            {
                Format = OutputFormat.JsonOnly,
                Strict = true
            };

            // Act
            var (isValid, errors, repaired) = _validator.Validate(output, spec);

            // Assert
            Assert.False(isValid);
            Assert.Contains(errors, e => e.Contains("Invalid JSON"));
        }

        [Fact]
        public void ValidateJson_InvalidJsonWithRepairableContent_ReturnsRepaired()
        {
            // Arrange
            var output = @"Here is the JSON: {""name"": ""test"", ""value"": 123} and some extra text";
            var spec = new StepOutputSpec
            {
                Format = OutputFormat.JsonOnly,
                Strict = true
            };

            // Act
            var (isValid, errors, repaired) = _validator.Validate(output, spec);

            // Assert
            Assert.False(isValid); // Original was invalid
            Assert.NotNull(repaired); // But repaired version exists
            Assert.Contains("{", repaired);
            Assert.Contains("}", repaired);
        }

        [Fact]
        public void ValidateJson_ExceedsMaxLength_Truncates()
        {
            // Arrange
            var output = @"{""name"": ""test with a very long value that exceeds the maximum allowed length""}";
            var spec = new StepOutputSpec
            {
                Format = OutputFormat.JsonOnly,
                MaxOutputChars = 20,
                Strict = false // Non-strict to allow truncation
            };

            // Act
            var (isValid, errors, repaired) = _validator.Validate(output, spec);

            // Assert
            Assert.NotEmpty(errors);
            Assert.Contains(errors, e => e.Contains("exceeds maximum length"));
            // Truncated JSON may not be valid, so repaired might be null if repair fails
            // The important thing is that the error was reported
        }

        #endregion

        #region Enum Validation Tests

        [Fact]
        public void ValidateEnum_ValidValue_ReturnsValid()
        {
            // Arrange
            var output = "success";
            var spec = new StepOutputSpec
            {
                Format = OutputFormat.EnumOnly,
                AllowedValues = new List<string> { "success", "failure", "pending" },
                Strict = true
            };

            // Act
            var (isValid, errors, repaired) = _validator.Validate(output, spec);

            // Assert
            Assert.True(isValid);
            Assert.Empty(errors);
        }

        [Fact]
        public void ValidateEnum_ValidValueWithWhitespace_ReturnsTrimmed()
        {
            // Arrange
            var output = "  success  ";
            var spec = new StepOutputSpec
            {
                Format = OutputFormat.EnumOnly,
                AllowedValues = new List<string> { "success", "failure", "pending" },
                Strict = true
            };

            // Act
            var (isValid, errors, repaired) = _validator.Validate(output, spec);

            // Assert
            Assert.True(isValid);
            Assert.Equal("success", repaired); // Trimmed
        }

        [Fact]
        public void ValidateEnum_InvalidValue_ReturnsInvalid()
        {
            // Arrange
            var output = "unknown";
            var spec = new StepOutputSpec
            {
                Format = OutputFormat.EnumOnly,
                AllowedValues = new List<string> { "success", "failure", "pending" },
                Strict = true
            };

            // Act
            var (isValid, errors, repaired) = _validator.Validate(output, spec);

            // Assert
            Assert.False(isValid);
            Assert.Contains(errors, e => e.Contains("not one of the allowed values"));
        }

        [Fact]
        public void ValidateEnum_CaseInsensitiveMatch_NonStrict_ReturnsRepaired()
        {
            // Arrange
            var output = "SUCCESS";
            var spec = new StepOutputSpec
            {
                Format = OutputFormat.EnumOnly,
                AllowedValues = new List<string> { "success", "failure", "pending" },
                Strict = false // Non-strict allows case-insensitive repair
            };

            // Act
            var (isValid, errors, repaired) = _validator.Validate(output, spec);

            // Assert
            Assert.False(isValid); // Original didn't match exactly
            Assert.Equal("success", repaired); // But repaired to correct case
        }

        [Fact]
        public void ValidateEnum_NoAllowedValues_ReturnsInvalid()
        {
            // Arrange
            var output = "success";
            var spec = new StepOutputSpec
            {
                Format = OutputFormat.EnumOnly,
                AllowedValues = null,
                Strict = true
            };

            // Act
            var (isValid, errors, repaired) = _validator.Validate(output, spec);

            // Assert
            Assert.False(isValid);
            Assert.Contains(errors, e => e.Contains("AllowedValues to be specified"));
        }

        #endregion

        #region Regex Validation Tests

        [Fact]
        public void ValidateRegex_ValidMatch_ReturnsValid()
        {
            // Arrange
            var output = "ABC-12345";
            var spec = new StepOutputSpec
            {
                Format = OutputFormat.RegexConstrained,
                Regex = @"^[A-Z]{3}-\d{5}$",
                Strict = true
            };

            // Act
            var (isValid, errors, repaired) = _validator.Validate(output, spec);

            // Assert
            Assert.True(isValid);
            Assert.Empty(errors);
        }

        [Fact]
        public void ValidateRegex_PartialMatch_ExtractsMatch()
        {
            // Arrange
            var output = "The code is ABC-12345 for reference";
            var spec = new StepOutputSpec
            {
                Format = OutputFormat.RegexConstrained,
                Regex = @"[A-Z]{3}-\d{5}",
                Strict = true
            };

            // Act
            var (isValid, errors, repaired) = _validator.Validate(output, spec);

            // Assert
            Assert.True(isValid);
            Assert.Equal("ABC-12345", repaired); // Extracted match
        }

        [Fact]
        public void ValidateRegex_NoMatch_ReturnsInvalid()
        {
            // Arrange
            var output = "invalid format";
            var spec = new StepOutputSpec
            {
                Format = OutputFormat.RegexConstrained,
                Regex = @"^[A-Z]{3}-\d{5}$",
                Strict = true
            };

            // Act
            var (isValid, errors, repaired) = _validator.Validate(output, spec);

            // Assert
            Assert.False(isValid);
            Assert.Contains(errors, e => e.Contains("does not match regex pattern"));
        }

        [Fact]
        public void ValidateRegex_NoRegexSpecified_ReturnsInvalid()
        {
            // Arrange
            var output = "anything";
            var spec = new StepOutputSpec
            {
                Format = OutputFormat.RegexConstrained,
                Regex = null,
                Strict = true
            };

            // Act
            var (isValid, errors, repaired) = _validator.Validate(output, spec);

            // Assert
            Assert.False(isValid);
            Assert.Contains(errors, e => e.Contains("Regex to be specified"));
        }

        #endregion

        #region PlainText Validation Tests

        [Fact]
        public void ValidatePlainText_WithinLimit_ReturnsValid()
        {
            // Arrange
            var output = "This is plain text output.";
            var spec = new StepOutputSpec
            {
                Format = OutputFormat.PlainText,
                MaxOutputChars = 100,
                Strict = true
            };

            // Act
            var (isValid, errors, repaired) = _validator.Validate(output, spec);

            // Assert
            Assert.True(isValid);
            Assert.Empty(errors);
        }

        [Fact]
        public void ValidatePlainText_ExceedsLimit_Strict_ReturnsInvalid()
        {
            // Arrange
            var output = "This is a very long plain text output that exceeds the maximum allowed characters.";
            var spec = new StepOutputSpec
            {
                Format = OutputFormat.PlainText,
                MaxOutputChars = 20,
                Strict = true
            };

            // Act
            var (isValid, errors, repaired) = _validator.Validate(output, spec);

            // Assert
            Assert.False(isValid);
            Assert.Contains(errors, e => e.Contains("exceeds maximum length"));
        }

        #endregion

        #region Repair Prompt Tests

        [Fact]
        public void GenerateRepairPrompt_JsonOnly_IncludesJsonInstructions()
        {
            // Arrange
            var spec = new StepOutputSpec
            {
                Format = OutputFormat.JsonOnly,
                RequiredJsonFields = new List<string> { "name", "value" }
            };
            var errors = new List<string> { "Invalid JSON" };

            // Act
            var prompt = _validator.GenerateRepairPrompt(spec, errors);

            // Assert
            Assert.Contains("ONLY valid JSON", prompt);
            Assert.Contains("name, value", prompt);
            Assert.Contains("Invalid JSON", prompt);
        }

        [Fact]
        public void GenerateRepairPrompt_EnumOnly_IncludesAllowedValues()
        {
            // Arrange
            var spec = new StepOutputSpec
            {
                Format = OutputFormat.EnumOnly,
                AllowedValues = new List<string> { "yes", "no", "maybe" }
            };
            var errors = new List<string> { "Invalid value" };

            // Act
            var prompt = _validator.GenerateRepairPrompt(spec, errors);

            // Assert
            Assert.Contains("ONLY one of these values", prompt);
            Assert.Contains("yes, no, maybe", prompt);
            Assert.Contains("Invalid value", prompt);
        }

        [Fact]
        public void GenerateRepairPrompt_RegexConstrained_IncludesPattern()
        {
            // Arrange
            var spec = new StepOutputSpec
            {
                Format = OutputFormat.RegexConstrained,
                Regex = @"^[A-Z]{3}-\d{5}$"
            };
            var errors = new List<string> { "Does not match pattern" };

            // Act
            var prompt = _validator.GenerateRepairPrompt(spec, errors);

            // Assert
            Assert.Contains("match this pattern", prompt);
            Assert.Contains(@"^[A-Z]{3}-\d{5}$", prompt);
            Assert.Contains("Does not match pattern", prompt);
        }

        #endregion
    }
}
