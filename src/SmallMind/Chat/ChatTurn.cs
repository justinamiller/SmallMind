using System;
using System.Collections.Generic;

namespace SmallMind.Chat
{
    /// <summary>
    /// Represents a single turn in a chat conversation.
    /// </summary>
    public class ChatTurn
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
}
