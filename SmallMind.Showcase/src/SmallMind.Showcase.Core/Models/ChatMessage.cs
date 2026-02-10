namespace SmallMind.Showcase.Core.Models;

/// <summary>
/// Represents a single message in a chat conversation.
/// </summary>
public sealed class ChatMessage
{
    public required string Id { get; init; }
    public required ChatMessageRole Role { get; init; }
    public required string Content { get; set; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public int? TokenCount { get; set; }
    public TimeSpan? Duration { get; set; }
}

public enum ChatMessageRole
{
    System,
    User,
    Assistant
}
