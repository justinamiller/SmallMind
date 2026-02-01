using System;
using System.Collections.Generic;
using SmallMind.Workflows;

namespace SmallMind.Chat
{
    /// <summary>
    /// Represents a multi-turn chat session with persistent state.
    /// </summary>
    public class ChatSession
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
        public List<ChatTurn> Turns { get; set; } = new List<ChatTurn>();

        /// <summary>
        /// Persistent state shared across turns (workflow state).
        /// </summary>
        public WorkflowState State { get; set; } = new WorkflowState();

        /// <summary>
        /// Optional session metadata.
        /// </summary>
        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
    }
}
