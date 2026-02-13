namespace SmallMind.Showcase.Core.Models;

/// <summary>
/// Configuration for text generation.
/// </summary>
public sealed class GenerationConfig
{
    public float Temperature { get; set; } = 0.7f;
    public float TopP { get; set; } = 0.9f;
    public int TopK { get; set; } = 40;
    public int MaxTokens { get; set; } = 512;
    public int? Seed { get; set; }
    public List<string> StopSequences { get; set; } = new();
}
