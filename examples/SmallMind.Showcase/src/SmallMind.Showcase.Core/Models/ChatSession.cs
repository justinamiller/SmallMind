namespace SmallMind.Showcase.Core.Models;

/// <summary>
/// Represents a chat session with conversation history.
/// </summary>
public sealed class ChatSession
{
    public required string Id { get; init; }
    public required string Title { get; set; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public List<ChatMessage> Messages { get; init; } = new();
    public GenerationConfig Config { get; init; } = new();
    public string? ModelId { get; set; }
}
