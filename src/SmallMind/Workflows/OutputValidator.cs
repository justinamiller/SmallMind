using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SmallMind.Workflows
{
    /// <summary>
    /// Validates and optionally repairs step outputs based on OutputSpec.
    /// </summary>
    public class OutputValidator
    {
        /// <summary>
        /// Validate output against the specification.
        /// </summary>
        public (bool isValid, List<string> errors, string? repairedOutput) Validate(
            string output, 
            StepOutputSpec spec)
        {
            var errors = new List<string>();
            string? repairedOutput = null;

            // Check max output chars
            if (output.Length > spec.MaxOutputChars)
            {
                errors.Add($"Output exceeds maximum length: {output.Length} > {spec.MaxOutputChars}");
                if (spec.Strict)
                {
                    return (false, errors, null);
                }
                // Truncate if non-strict
                repairedOutput = output.Substring(0, spec.MaxOutputChars);
            }

            switch (spec.Format)
            {
                case OutputFormat.JsonOnly:
                    return ValidateJson(repairedOutput ?? output, spec, errors);

                case OutputFormat.EnumOnly:
                    return ValidateEnum(repairedOutput ?? output, spec, errors);

                case OutputFormat.RegexConstrained:
                    return ValidateRegex(repairedOutput ?? output, spec, errors);

                case OutputFormat.PlainText:
                    // Plain text - just check length
                    return (errors.Count == 0, errors, repairedOutput);

                default:
                    errors.Add($"Unknown output format: {spec.Format}");
                    return (false, errors, null);
            }
        }

        private (bool isValid, List<string> errors, string? repairedOutput) ValidateJson(
            string output,
            StepOutputSpec spec,
            List<string> errors)
        {
            // Try to parse as JSON
            try
            {
                using var doc = JsonDocument.Parse(output);
                var root = doc.RootElement;

                // Check required fields
                if (spec.RequiredJsonFields != null && spec.RequiredJsonFields.Count > 0)
                {
                    foreach (var field in spec.RequiredJsonFields)
                    {
                        if (!HasJsonProperty(root, field))
                        {
                            errors.Add($"Missing required JSON field: {field}");
                        }
                    }
                }

                if (errors.Count > 0 && spec.Strict)
                {
                    return (false, errors, null);
                }

                return (errors.Count == 0, errors, null);
            }
            catch (JsonException ex)
            {
                errors.Add($"Invalid JSON: {ex.Message}");
                
                // Try to repair JSON by extracting first JSON object/array
                var repaired = TryRepairJson(output);
                if (repaired != null)
                {
                    // Recursively validate repaired JSON
                    var (isValid, _, _) = ValidateJson(repaired, spec, new List<string>());
                    if (isValid)
                    {
                        return (false, errors, repaired); // Not valid originally but repaired
                    }
                }

                return (false, errors, null);
            }
        }

        private bool HasJsonProperty(JsonElement element, string propertyName)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                return element.TryGetProperty(propertyName, out _);
            }
            return false;
        }

        private string? TryRepairJson(string output)
        {
            // Try to extract JSON by finding first { or [ and matching closing brace/bracket
            int startIndex = -1;
            char startChar = '\0';

            for (int i = 0; i < output.Length; i++)
            {
                if (output[i] == '{')
                {
                    startIndex = i;
                    startChar = '{';
                    break;
                }
                else if (output[i] == '[')
                {
                    startIndex = i;
                    startChar = '[';
                    break;
                }
            }

            if (startIndex == -1) return null;

            // Find matching closing bracket
            char endChar = startChar == '{' ? '}' : ']';
            int depth = 0;
            int endIndex = -1;

            for (int i = startIndex; i < output.Length; i++)
            {
                if (output[i] == startChar) depth++;
                else if (output[i] == endChar)
                {
                    depth--;
                    if (depth == 0)
                    {
                        endIndex = i;
                        break;
                    }
                }
            }

            if (endIndex == -1) return null;

            var extracted = output.Substring(startIndex, endIndex - startIndex + 1);
            
            // Verify it's valid JSON
            try
            {
                using var doc = JsonDocument.Parse(extracted);
                return extracted;
            }
            catch
            {
                return null;
            }
        }

        private (bool isValid, List<string> errors, string? repairedOutput) ValidateEnum(
            string output,
            StepOutputSpec spec,
            List<string> errors)
        {
            if (spec.AllowedValues == null || spec.AllowedValues.Count == 0)
            {
                errors.Add("EnumOnly format requires AllowedValues to be specified");
                return (false, errors, null);
            }

            var trimmed = output.Trim();

            // Case-sensitive match by default
            if (spec.AllowedValues.Contains(trimmed))
            {
                return (true, errors, trimmed != output ? trimmed : null);
            }

            errors.Add($"Output '{trimmed}' is not one of the allowed values: {string.Join(", ", spec.AllowedValues)}");

            if (spec.Strict)
            {
                return (false, errors, null);
            }

            // Try case-insensitive match as repair
            var match = spec.AllowedValues.FirstOrDefault(v =>
                string.Equals(v, trimmed, StringComparison.OrdinalIgnoreCase));

            if (match != null)
            {
                return (false, errors, match);
            }

            return (false, errors, null);
        }

        private (bool isValid, List<string> errors, string? repairedOutput) ValidateRegex(
            string output,
            StepOutputSpec spec,
            List<string> errors)
        {
            if (string.IsNullOrEmpty(spec.Regex))
            {
                errors.Add("RegexConstrained format requires Regex to be specified");
                return (false, errors, null);
            }

            try
            {
                var regex = new Regex(spec.Regex, RegexOptions.Singleline, TimeSpan.FromSeconds(1));
                var match = regex.Match(output);

                if (match.Success)
                {
                    // If match is not the entire output, extract just the matched portion
                    if (match.Value != output)
                    {
                        return (true, errors, match.Value);
                    }
                    return (true, errors, null);
                }

                errors.Add($"Output does not match regex pattern: {spec.Regex}");
                return (false, errors, null);
            }
            catch (RegexMatchTimeoutException)
            {
                errors.Add("Regex matching timed out");
                return (false, errors, null);
            }
            catch (ArgumentException ex)
            {
                errors.Add($"Invalid regex pattern: {ex.Message}");
                return (false, errors, null);
            }
        }

        /// <summary>
        /// Generate a repair prompt to guide the model to correct invalid output.
        /// </summary>
        public string GenerateRepairPrompt(StepOutputSpec spec, List<string> errors)
        {
            var prompt = "Your previous output was invalid. ";

            switch (spec.Format)
            {
                case OutputFormat.JsonOnly:
                    prompt += "You must return ONLY valid JSON. ";
                    if (spec.RequiredJsonFields != null && spec.RequiredJsonFields.Count > 0)
                    {
                        prompt += $"Required fields: {string.Join(", ", spec.RequiredJsonFields)}. ";
                    }
                    if (!string.IsNullOrEmpty(spec.JsonTemplate))
                    {
                        prompt += $"Example format:\n{spec.JsonTemplate}\n";
                    }
                    break;

                case OutputFormat.EnumOnly:
                    prompt += $"You must return ONLY one of these values: {string.Join(", ", spec.AllowedValues ?? new List<string>())}. ";
                    break;

                case OutputFormat.RegexConstrained:
                    prompt += $"Your output must match this pattern: {spec.Regex}. ";
                    break;
            }

            prompt += "\nErrors: " + string.Join("; ", errors);
            prompt += "\nPlease provide a corrected response now.";

            return prompt;
        }
    }
}
