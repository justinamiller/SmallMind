using SmallMind.Engine;
using Xunit;

namespace SmallMind.Tests.Chat
{
    /// <summary>
    /// Tests for JsonSchemaValidator ensuring basic JSON schema validation.
    /// </summary>
    public class JsonSchemaValidatorTests
    {
        private readonly JsonSchemaValidator _validator = new();

        [Fact]
        public void Validate_SimpleObject_ValidatesType()
        {
            var schema = @"{
                ""type"": ""object"",
                ""properties"": {
                    ""name"": { ""type"": ""string"" },
                    ""age"": { ""type"": ""number"" }
                }
            }";

            var validJson = @"{ ""name"": ""Alice"", ""age"": 30 }";
            var result = _validator.Validate(validJson, schema);

            Assert.True(result.IsValid);
            Assert.Empty(result.Errors);
        }

        [Fact]
        public void Validate_RequiredFields_EnforcesRequired()
        {
            var schema = @"{
                ""type"": ""object"",
                ""properties"": {
                    ""name"": { ""type"": ""string"" },
                    ""email"": { ""type"": ""string"" }
                },
                ""required"": [""name"", ""email""]
            }";

            var invalidJson = @"{ ""name"": ""Bob"" }";
            var result = _validator.Validate(invalidJson, schema);

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.Contains("email"));
        }

        [Fact]
        public void Validate_Array_ValidatesItems()
        {
            var schema = @"{
                ""type"": ""array"",
                ""items"": {
                    ""type"": ""string""
                }
            }";

            var validJson = @"[""apple"", ""banana"", ""cherry""]";
            var result = _validator.Validate(validJson, schema);

            Assert.True(result.IsValid);
        }

        [Fact]
        public void Validate_Array_InvalidItemType_Fails()
        {
            var schema = @"{
                ""type"": ""array"",
                ""items"": {
                    ""type"": ""number""
                }
            }";

            var invalidJson = @"[1, 2, ""three""]";
            var result = _validator.Validate(invalidJson, schema);

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.Contains("number"));
        }

        [Fact]
        public void Validate_Enum_ValidatesAllowedValues()
        {
            var schema = @"{
                ""type"": ""object"",
                ""properties"": {
                    ""status"": {
                        ""type"": ""string"",
                        ""enum"": [""active"", ""inactive"", ""pending""]
                    }
                }
            }";

            var validJson = @"{ ""status"": ""active"" }";
            var invalidJson = @"{ ""status"": ""deleted"" }";

            var validResult = _validator.Validate(validJson, schema);
            var invalidResult = _validator.Validate(invalidJson, schema);

            Assert.True(validResult.IsValid);
            Assert.False(invalidResult.IsValid);
            Assert.Contains(invalidResult.Errors, e => e.Contains("deleted") && e.Contains("enum"));
        }

        [Fact]
        public void Validate_MinMaxLength_EnforcesStringLength()
        {
            var schema = @"{
                ""type"": ""object"",
                ""properties"": {
                    ""username"": {
                        ""type"": ""string"",
                        ""minLength"": 3,
                        ""maxLength"": 20
                    }
                }
            }";

            var tooShortJson = @"{ ""username"": ""ab"" }";
            var tooLongJson = @"{ ""username"": ""verylongusernamethatexceedslimit"" }";
            var validJson = @"{ ""username"": ""alice"" }";

            var tooShortResult = _validator.Validate(tooShortJson, schema);
            var tooLongResult = _validator.Validate(tooLongJson, schema);
            var validResult = _validator.Validate(validJson, schema);

            Assert.False(tooShortResult.IsValid);
            Assert.False(tooLongResult.IsValid);
            Assert.True(validResult.IsValid);
        }

        [Fact]
        public void Validate_MinMaxItems_EnforcesArrayLength()
        {
            var schema = @"{
                ""type"": ""array"",
                ""items"": { ""type"": ""string"" },
                ""minItems"": 2,
                ""maxItems"": 5
            }";

            var tooFewJson = @"[""one""]";
            var tooManyJson = @"[""one"", ""two"", ""three"", ""four"", ""five"", ""six""]";
            var validJson = @"[""one"", ""two"", ""three""]";

            var tooFewResult = _validator.Validate(tooFewJson, schema);
            var tooManyResult = _validator.Validate(tooManyJson, schema);
            var validResult = _validator.Validate(validJson, schema);

            Assert.False(tooFewResult.IsValid);
            Assert.False(tooManyResult.IsValid);
            Assert.True(validResult.IsValid);
        }

        [Fact]
        public void Validate_MinMaxNumber_EnforcesRange()
        {
            var schema = @"{
                ""type"": ""object"",
                ""properties"": {
                    ""score"": {
                        ""type"": ""number"",
                        ""minimum"": 0,
                        ""maximum"": 100
                    }
                }
            }";

            var tooLowJson = @"{ ""score"": -10 }";
            var tooHighJson = @"{ ""score"": 150 }";
            var validJson = @"{ ""score"": 75 }";

            var tooLowResult = _validator.Validate(tooLowJson, schema);
            var tooHighResult = _validator.Validate(tooHighJson, schema);
            var validResult = _validator.Validate(validJson, schema);

            Assert.False(tooLowResult.IsValid);
            Assert.False(tooHighResult.IsValid);
            Assert.True(validResult.IsValid);
        }

        [Fact]
        public void Validate_NestedObject_ValidatesRecursively()
        {
            var schema = @"{
                ""type"": ""object"",
                ""properties"": {
                    ""user"": {
                        ""type"": ""object"",
                        ""properties"": {
                            ""name"": { ""type"": ""string"" },
                            ""age"": { ""type"": ""number"" }
                        },
                        ""required"": [""name""]
                    }
                }
            }";

            var validJson = @"{ ""user"": { ""name"": ""Alice"", ""age"": 30 } }";
            var invalidJson = @"{ ""user"": { ""age"": 30 } }";

            var validResult = _validator.Validate(validJson, schema);
            var invalidResult = _validator.Validate(invalidJson, schema);

            Assert.True(validResult.IsValid);
            Assert.False(invalidResult.IsValid);
        }

        [Fact]
        public void Validate_InvalidJson_ReturnsError()
        {
            var schema = @"{ ""type"": ""object"" }";
            var invalidJson = @"{ invalid json }";

            var result = _validator.Validate(invalidJson, schema);

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.Contains("Invalid JSON"));
        }

        [Fact]
        public void Validate_ComplexSchema_ValidatesCorrectly()
        {
            var schema = @"{
                ""type"": ""object"",
                ""properties"": {
                    ""id"": { ""type"": ""integer"" },
                    ""name"": { ""type"": ""string"", ""minLength"": 1 },
                    ""tags"": {
                        ""type"": ""array"",
                        ""items"": { ""type"": ""string"" }
                    },
                    ""metadata"": {
                        ""type"": ""object"",
                        ""properties"": {
                            ""created"": { ""type"": ""string"" }
                        }
                    }
                },
                ""required"": [""id"", ""name""]
            }";

            var validJson = @"{
                ""id"": 123,
                ""name"": ""Test Item"",
                ""tags"": [""tag1"", ""tag2""],
                ""metadata"": {
                    ""created"": ""2024-01-01""
                }
            }";

            var result = _validator.Validate(validJson, schema);

            Assert.True(result.IsValid);
            Assert.Empty(result.Errors);
        }

        [Fact]
        public void Validate_TypeMismatch_ReportsError()
        {
            var schema = @"{
                ""type"": ""object"",
                ""properties"": {
                    ""count"": { ""type"": ""number"" }
                }
            }";

            var invalidJson = @"{ ""count"": ""not a number"" }";
            var result = _validator.Validate(invalidJson, schema);

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.Contains("number"));
        }
    }
}
