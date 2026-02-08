using System;
using System.Collections.Generic;

namespace SmallMind.Abstractions
{
    /// <summary>
    /// Represents a single turn in a chat conversation (persisted data).
    /// </summary>
    public class ChatTurnData
    {
        /// <summary>
        /// The user's message.
        /// </summary>
        public string UserMessage { get; set; } = string.Empty;

        /// <summary>
        /// The assistant's response.
        /// </summary>
        public string AssistantMessage { get; set; } = string.Empty;

        /// <summary>
        /// When this turn occurred.
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Optional citations included in the assistant's response.
        /// </summary>
        public List<string> Citations { get; set; } = new List<string>();

        /// <summary>
        /// Optional structured output from the turn.
        /// </summary>
        public object? StructuredOutput { get; set; }

        /// <summary>
        /// Optional metadata for this turn.
        /// </summary>
        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
    }

    /// <summary>
    /// Represents a multi-turn chat session with persistent state (data model).
    /// Moved from SmallMind.Chat to SmallMind.Abstractions for unified chat pipeline.
    /// </summary>
    public class ChatSessionData
    {
        /// <summary>
        /// Unique session identifier.
        /// </summary>
        public string SessionId { get; set; } = string.Empty;

        /// <summary>
        /// When the session was created.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// When the session was last updated.
        /// </summary>
        public DateTime LastUpdatedAt { get; set; }

        /// <summary>
        /// Ordered list of conversation turns.
        /// </summary>
        public List<ChatTurnData> Turns { get; set; } = new List<ChatTurnData>();

        /// <summary>
        /// Optional session metadata.
        /// </summary>
        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
    }
}
