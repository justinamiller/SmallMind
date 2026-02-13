namespace SmallMind.Server.Services;

/// <summary>
/// Validates incoming requests against configured server limits.
/// Provides production-grade validation for prompts, tokens, and parameters.
/// </summary>
public sealed class RequestValidator
{
    private readonly ServerOptions _options;

    public RequestValidator(ServerOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Validates a request's parameters against server limits.
    /// </summary>
    /// <param name="maxTokens">Requested max completion tokens</param>
    /// <param name="promptLength">Estimated prompt length in tokens (optional)</param>
    /// <param name="temperature">Temperature parameter</param>
    /// <param name="topP">Top-P parameter</param>
    /// <returns>Validation result with error details if invalid</returns>
    public ValidationResult ValidateRequest(
        int? maxTokens,
        int? promptLength = null,
        float? temperature = null,
        float? topP = null)
    {
        // Validate max tokens
        int requestedTokens = maxTokens ?? _options.DefaultMaxTokens;
        if (_options.StrictLimits && requestedTokens > _options.MaxCompletionTokens)
        {
            return ValidationResult.Fail(
                $"max_tokens exceeds server limit: requested {requestedTokens}, max {_options.MaxCompletionTokens}",
                "invalid_request_error",
                "max_tokens");
        }

        // Validate prompt length if provided
        if (promptLength.HasValue && _options.StrictLimits)
        {
            if (promptLength.Value > _options.MaxPromptTokens)
            {
                return ValidationResult.Fail(
                    $"Prompt exceeds maximum token limit: {promptLength.Value} > {_options.MaxPromptTokens}",
                    "invalid_request_error",
                    "prompt");
            }

            // Check total tokens (prompt + completion)
            int totalTokens = promptLength.Value + requestedTokens;
            if (totalTokens > _options.MaxContextTokens)
            {
                return ValidationResult.Fail(
                    $"Total tokens (prompt + completion) exceeds context limit: {totalTokens} > {_options.MaxContextTokens}",
                    "context_length_exceeded",
                    "max_tokens");
            }
        }

        // Validate temperature
        if (temperature.HasValue)
        {
            if (temperature.Value < 0 || temperature.Value > 2.0f)
            {
                return ValidationResult.Fail(
                    "temperature must be between 0 and 2.0",
                    "invalid_request_error",
                    "temperature");
            }
        }

        // Validate top_p
        if (topP.HasValue)
        {
            if (topP.Value < 0 || topP.Value > 1.0f)
            {
                return ValidationResult.Fail(
                    "top_p must be between 0 and 1.0",
                    "invalid_request_error",
                    "top_p");
            }
        }

        return ValidationResult.Success();
    }

    /// <summary>
    /// Estimates the number of tokens in a prompt (rough approximation).
    /// Uses a simple heuristic: ~4 characters per token.
    /// </summary>
    public int EstimatePromptTokens(string prompt)
    {
        if (string.IsNullOrEmpty(prompt))
            return 0;

        // Rough approximation: average of 4 characters per token
        return (int)Math.Ceiling(prompt.Length / 4.0);
    }
}

/// <summary>
/// Result of request validation.
/// </summary>
public readonly struct ValidationResult
{
    public bool IsValid { get; }
    public string? ErrorMessage { get; }
    public string? ErrorType { get; }
    public string? ErrorParam { get; }

    private ValidationResult(bool isValid, string? errorMessage, string? errorType, string? errorParam)
    {
        IsValid = isValid;
        ErrorMessage = errorMessage;
        ErrorType = errorType;
        ErrorParam = errorParam;
    }

    public static ValidationResult Success() => new(true, null, null, null);

    public static ValidationResult Fail(string message, string type, string? param = null) =>
        new(false, message, type, param);
}
