namespace SmallMind.Benchmarks;

/// <summary>
/// Built-in prompt profiles for benchmarking.
/// </summary>
public static class PromptProfiles
{
    private const string BaseParagraph = 
        "The quick brown fox jumps over the lazy dog. " +
        "This is a sample text used for benchmarking purposes. " +
        "It contains common English words and punctuation. ";
    
    /// <summary>
    /// Get a prompt for the specified profile.
    /// Approximate token counts (character-level varies):
    /// - short: ~32 tokens (~150 chars)
    /// - med: ~256 tokens (~1200 chars)
    /// - long: ~1024 tokens (~4800 chars)
    /// </summary>
    public static string GetPrompt(string profile)
    {
        return profile.ToLowerInvariant() switch
        {
            "short" => GeneratePrompt(32),
            "med" or "medium" => GeneratePrompt(256),
            "long" => GeneratePrompt(1024),
            _ => throw new ArgumentException($"Unknown prompt profile: {profile}")
        };
    }
    
    private static string GeneratePrompt(int approximateTokens)
    {
        // Approximate: ~5 chars per token for English text
        int targetChars = approximateTokens * 5;
        
        // Build prompt by repeating base paragraph
        var builder = new System.Text.StringBuilder(targetChars);
        
        while (builder.Length < targetChars)
        {
            builder.Append(BaseParagraph);
        }
        
        // Trim to approximate target
        if (builder.Length > targetChars)
        {
            builder.Length = targetChars;
        }
        
        return builder.ToString();
    }
    
    /// <summary>
    /// Get a simple prompt for testing.
    /// </summary>
    public static string GetSimplePrompt()
    {
        return "Once upon a time";
    }
}
