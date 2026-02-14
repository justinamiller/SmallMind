namespace SmallMind.Runtime
{
    /// <summary>
    /// Represents a single turn in the conversation
    /// </summary>
    internal class ConversationTurn
    {
        public string Role { get; set; } = ""; // "user" or "assistant"
        public string Content { get; set; } = "";
        public DateTime Timestamp { get; set; }
    }
}
