using SmallMind.Server;
using SmallMind.Server.Services;

namespace SmallMind.IntegrationTests;

/// <summary>
/// Integration tests for the SmallMind.Server request validation and hardening controls.
/// These tests validate the server's production controls without requiring a loaded model.
/// </summary>
public class ServerValidationTests
{
    private static ServerOptions DefaultOptions() => new()
    {
        MaxCompletionTokens = 2048,
        MaxPromptTokens = 8192,
        MaxContextTokens = 4096,
        StrictLimits = true,
        DefaultMaxTokens = 100,
        MaxRequestBodySizeBytes = 1_048_576
    };

    [Fact]
    public void ValidateRequest_WithinLimits_Succeeds()
    {
        var validator = new RequestValidator(DefaultOptions());
        var result = validator.ValidateRequest(maxTokens: 100, promptLength: 500);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateRequest_ExceedsMaxCompletionTokens_Rejected()
    {
        var validator = new RequestValidator(DefaultOptions());
        var result = validator.ValidateRequest(maxTokens: 5000, promptLength: 100);
        Assert.False(result.IsValid);
        Assert.Contains("max_tokens", result.ErrorMessage!);
    }

    [Fact]
    public void ValidateRequest_ExceedsMaxPromptTokens_Rejected()
    {
        var validator = new RequestValidator(DefaultOptions());
        var result = validator.ValidateRequest(maxTokens: 100, promptLength: 10000);
        Assert.False(result.IsValid);
        Assert.Contains("Prompt", result.ErrorMessage!);
    }

    [Fact]
    public void ValidateRequest_ExceedsContextLimit_Rejected()
    {
        var validator = new RequestValidator(DefaultOptions());
        var result = validator.ValidateRequest(maxTokens: 2000, promptLength: 3000);
        Assert.False(result.IsValid);
        Assert.Contains("context", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateRequest_InvalidTemperature_Rejected()
    {
        var validator = new RequestValidator(DefaultOptions());
        var result = validator.ValidateRequest(maxTokens: 100, temperature: 3.0f);
        Assert.False(result.IsValid);
        Assert.Equal("temperature", result.ErrorParam);
    }

    [Fact]
    public void ValidateRequest_InvalidTopP_Rejected()
    {
        var validator = new RequestValidator(DefaultOptions());
        var result = validator.ValidateRequest(maxTokens: 100, topP: 1.5f);
        Assert.False(result.IsValid);
        Assert.Equal("top_p", result.ErrorParam);
    }

    [Fact]
    public void ValidateRequest_StrictLimitsDisabled_AllowsExcess()
    {
        var opts = DefaultOptions();
        opts.StrictLimits = false;
        var validator = new RequestValidator(opts);
        var result = validator.ValidateRequest(maxTokens: 5000, promptLength: 10000);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void EstimatePromptTokens_ReturnsReasonableEstimate()
    {
        var validator = new RequestValidator(DefaultOptions());
        // ~4 chars per token heuristic
        Assert.Equal(0, validator.EstimatePromptTokens(""));
        Assert.Equal(3, validator.EstimatePromptTokens("Hello world"));
        Assert.True(validator.EstimatePromptTokens("A longer prompt with many words") > 0);
    }

    [Fact]
    public void ServerOptions_DefaultValues_AreProductionSafe()
    {
        var opts = new ServerOptions();
        Assert.Equal(4, opts.MaxConcurrentRequests);
        Assert.Equal(32, opts.MaxQueueDepth);
        Assert.Equal(2048, opts.MaxCompletionTokens);
        Assert.Equal(8192, opts.MaxPromptTokens);
        Assert.Equal(5000, opts.PerTokenTimeoutMs);
        Assert.Equal(1_048_576, opts.MaxRequestBodySizeBytes);
        Assert.True(opts.StrictLimits);
        Assert.False(opts.EnableConsoleLogging);
    }
}
