using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace SmallMind.Engine
{
    /// <summary>
    /// Basic JSON schema validator supporting a subset of JSON Schema.
    /// Supports: type, required, properties, items, enum.
    /// </summary>
    public sealed class JsonSchemaValidator
    {
        /// <summary>
        /// Validates JSON against a schema.
        /// </summary>
        /// <param name="json">JSON string to validate.</param>
        /// <param name="schemaJson">JSON schema string.</param>
        /// <returns>Validation result with errors if any.</returns>
        public ValidationResult Validate(string json, string schemaJson)
        {
            try
            {
                var document = JsonDocument.Parse(json);
                var schema = JsonDocument.Parse(schemaJson);

                var errors = new List<string>();
                ValidateElement(document.RootElement, schema.RootElement, "$", errors);

                return new ValidationResult
                {
                    IsValid = errors.Count == 0,
                    Errors = errors
                };
            }
            catch (JsonException ex)
            {
                return new ValidationResult
                {
                    IsValid = false,
                    Errors = new List<string> { $"Invalid JSON: {ex.Message}" }
                };
            }
        }

        private void ValidateElement(JsonElement element, JsonElement schema, string path, List<string> errors)
        {
            // Validate type
            if (schema.TryGetProperty("type", out var typeProperty))
            {
                var expectedType = typeProperty.GetString();
                if (!ValidateType(element, expectedType))
                {
                    errors.Add($"{path}: Expected type '{expectedType}', got '{GetJsonType(element)}'");
                    return;
                }
            }

            // Validate based on type
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    ValidateObject(element, schema, path, errors);
                    break;
                case JsonValueKind.Array:
                    ValidateArray(element, schema, path, errors);
                    break;
                case JsonValueKind.String:
                    ValidateString(element, schema, path, errors);
                    break;
                case JsonValueKind.Number:
                    ValidateNumber(element, schema, path, errors);
                    break;
            }
        }

        private void ValidateObject(JsonElement element, JsonElement schema, string path, List<string> errors)
        {
            // Validate required properties
            if (schema.TryGetProperty("required", out var requiredArray))
            {
                foreach (var req in requiredArray.EnumerateArray())
                {
                    var propName = req.GetString();
                    if (propName != null && !element.TryGetProperty(propName, out _))
                    {
                        errors.Add($"{path}: Missing required property '{propName}'");
                    }
                }
            }

            // Validate properties
            if (schema.TryGetProperty("properties", out var propertiesObj))
            {
                foreach (var prop in element.EnumerateObject())
                {
                    if (propertiesObj.TryGetProperty(prop.Name, out var propSchema))
                    {
                        ValidateElement(prop.Value, propSchema, $"{path}.{prop.Name}", errors);
                    }
                }
            }
        }

        private void ValidateArray(JsonElement element, JsonElement schema, string path, List<string> errors)
        {
            // Validate items
            if (schema.TryGetProperty("items", out var itemsSchema))
            {
                int index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    ValidateElement(item, itemsSchema, $"{path}[{index}]", errors);
                    index++;
                }
            }

            // Validate minItems
            if (schema.TryGetProperty("minItems", out var minItems))
            {
                var min = minItems.GetInt32();
                var count = element.GetArrayLength();
                if (count < min)
                {
                    errors.Add($"{path}: Array length {count} is less than minItems {min}");
                }
            }

            // Validate maxItems
            if (schema.TryGetProperty("maxItems", out var maxItems))
            {
                var max = maxItems.GetInt32();
                var count = element.GetArrayLength();
                if (count > max)
                {
                    errors.Add($"{path}: Array length {count} exceeds maxItems {max}");
                }
            }
        }

        private void ValidateString(JsonElement element, JsonElement schema, string path, List<string> errors)
        {
            var value = element.GetString();
            if (value == null)
                return;

            // Validate enum
            if (schema.TryGetProperty("enum", out var enumArray))
            {
                var validValues = enumArray.EnumerateArray().Select(e => e.GetString()).ToList();
                if (!validValues.Contains(value))
                {
                    errors.Add($"{path}: Value '{value}' is not in enum: [{string.Join(", ", validValues)}]");
                }
            }

            // Validate minLength
            if (schema.TryGetProperty("minLength", out var minLength))
            {
                var min = minLength.GetInt32();
                if (value.Length < min)
                {
                    errors.Add($"{path}: String length {value.Length} is less than minLength {min}");
                }
            }

            // Validate maxLength
            if (schema.TryGetProperty("maxLength", out var maxLength))
            {
                var max = maxLength.GetInt32();
                if (value.Length > max)
                {
                    errors.Add($"{path}: String length {value.Length} exceeds maxLength {max}");
                }
            }

            // Validate pattern (basic regex support)
            if (schema.TryGetProperty("pattern", out var pattern))
            {
                var regex = new System.Text.RegularExpressions.Regex(pattern.GetString() ?? "");
                if (!regex.IsMatch(value))
                {
                    errors.Add($"{path}: String does not match pattern '{pattern.GetString()}'");
                }
            }
        }

        private void ValidateNumber(JsonElement element, JsonElement schema, string path, List<string> errors)
        {
            var value = element.GetDouble();

            // Validate minimum
            if (schema.TryGetProperty("minimum", out var minimum))
            {
                var min = minimum.GetDouble();
                if (value < min)
                {
                    errors.Add($"{path}: Value {value} is less than minimum {min}");
                }
            }

            // Validate maximum
            if (schema.TryGetProperty("maximum", out var maximum))
            {
                var max = maximum.GetDouble();
                if (value > max)
                {
                    errors.Add($"{path}: Value {value} exceeds maximum {max}");
                }
            }
        }

        private bool ValidateType(JsonElement element, string? expectedType)
        {
            if (expectedType == null)
                return true;

            return expectedType switch
            {
                "object" => element.ValueKind == JsonValueKind.Object,
                "array" => element.ValueKind == JsonValueKind.Array,
                "string" => element.ValueKind == JsonValueKind.String,
                "number" => element.ValueKind == JsonValueKind.Number,
                "integer" => element.ValueKind == JsonValueKind.Number && IsInteger(element),
                "boolean" => element.ValueKind == JsonValueKind.True || element.ValueKind == JsonValueKind.False,
                "null" => element.ValueKind == JsonValueKind.Null,
                _ => true
            };
        }

        private bool IsInteger(JsonElement element)
        {
            if (element.TryGetInt64(out _))
                return true;
            return false;
        }

        private string GetJsonType(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.Object => "object",
                JsonValueKind.Array => "array",
                JsonValueKind.String => "string",
                JsonValueKind.Number => "number",
                JsonValueKind.True => "boolean",
                JsonValueKind.False => "boolean",
                JsonValueKind.Null => "null",
                _ => "unknown"
            };
        }
    }

    /// <summary>
    /// Result of JSON schema validation.
    /// </summary>
    public sealed class ValidationResult
    {
        /// <summary>
        /// Gets whether the JSON is valid.
        /// </summary>
        public bool IsValid { get; init; }

        /// <summary>
        /// Gets validation errors (empty if valid).
        /// </summary>
        public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
    }
}
