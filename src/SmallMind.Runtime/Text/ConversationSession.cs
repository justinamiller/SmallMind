using System.Text.Json;
using SmallMind.Tokenizers;

namespace SmallMind.Runtime
{
    /// <summary>
    /// Manages conversation sessions with context history for multi-turn interactions.
    /// Implements intelligent context window management and session persistence.
    /// </summary>
    [Obsolete("Use SmallMind.Engine.ChatSession instead. This class will be removed in a future version.")]
    internal class ConversationSession
    {
        private readonly int _maxContextTokens;
        private readonly List<ConversationTurn> _history;
        private readonly ITokenizer _tokenizer;

        public string SessionId { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime LastUpdatedAt { get; private set; }

        public ConversationSession(string sessionId, ITokenizer tokenizer, int maxContextTokens = 512)
        {
            SessionId = sessionId ?? Guid.NewGuid().ToString();
            _tokenizer = tokenizer;
            _maxContextTokens = maxContextTokens;
            _history = new List<ConversationTurn>();
            CreatedAt = DateTime.UtcNow;
            LastUpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Add a user input to the conversation history
        /// </summary>
        public void AddUserInput(string input)
        {
            var turn = new ConversationTurn
            {
                Role = "user",
                Content = input,
                Timestamp = DateTime.UtcNow
            };
            _history.Add(turn);
            LastUpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Add an assistant response to the conversation history
        /// </summary>
        public void AddAssistantResponse(string response)
        {
            var turn = new ConversationTurn
            {
                Role = "assistant",
                Content = response,
                Timestamp = DateTime.UtcNow
            };
            _history.Add(turn);
            LastUpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Get the conversation context as a formatted string with intelligent truncation
        /// </summary>
        public string GetContextString()
        {
            // Build full conversation history
            var contextParts = new List<string>();
            foreach (var turn in _history)
            {
                string prefix = turn.Role == "user" ? "User: " : "Assistant: ";
                contextParts.Add(prefix + turn.Content);
            }

            // Join and check if we need to truncate
            string fullContext = string.Join("\n", contextParts);
            var tokens = _tokenizer.Encode(fullContext);

            if (tokens.Count <= _maxContextTokens)
            {
                return fullContext;
            }

            // If too long, keep most recent turns that fit
            contextParts.Clear();
            int currentTokens = 0;

            for (int i = _history.Count - 1; i >= 0; i--)
            {
                var turn = _history[i];
                string prefix = turn.Role == "user" ? "User: " : "Assistant: ";
                string turnText = prefix + turn.Content + "\n";
                var turnTokens = _tokenizer.Encode(turnText);

                if (currentTokens + turnTokens.Count > _maxContextTokens)
                {
                    break;
                }

                contextParts.Insert(0, turnText.TrimEnd());
                currentTokens += turnTokens.Count;
            }

            return string.Join("\n", contextParts);
        }

        /// <summary>
        /// Get conversation history as a list of turns
        /// </summary>
        public List<ConversationTurn> GetHistory()
        {
            return new List<ConversationTurn>(_history);
        }

        /// <summary>
        /// Clear conversation history
        /// </summary>
        public void Clear()
        {
            _history.Clear();
            LastUpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Get a summary of the conversation (for display)
        /// </summary>
        public string GetSummary()
        {
            int userTurns = 0;
            int assistantTurns = 0;
            foreach (var turn in _history)
            {
                if (turn.Role == "user") userTurns++;
                else if (turn.Role == "assistant") assistantTurns++;
            }

            return $"Session {SessionId}: {_history.Count} turns ({userTurns} user, {assistantTurns} assistant)";
        }

        /// <summary>
        /// Save session to a JSON file
        /// </summary>
        public void SaveToFile(string filePath)
        {
            var sessionData = new SessionData
            {
                SessionId = SessionId,
                CreatedAt = CreatedAt,
                LastUpdatedAt = LastUpdatedAt,
                History = _history
            };

            var json = JsonSerializer.Serialize(sessionData, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(filePath, json);
        }

        /// <summary>
        /// Load session from a JSON file
        /// </summary>
        public static ConversationSession LoadFromFile(string filePath, ITokenizer tokenizer, int maxContextTokens = 512)
        {
            var json = File.ReadAllText(filePath);
            var sessionData = JsonSerializer.Deserialize<SessionData>(json);

            if (sessionData == null)
            {
                throw new InvalidOperationException("Failed to deserialize session data");
            }

            var session = new ConversationSession(sessionData.SessionId, tokenizer, maxContextTokens)
            {
                CreatedAt = sessionData.CreatedAt,
                LastUpdatedAt = sessionData.LastUpdatedAt
            };

            foreach (var turn in sessionData.History)
            {
                session._history.Add(turn);
            }

            return session;
        }

        // Internal data structures for serialization
        private class SessionData
        {
            public string SessionId { get; set; } = "";
            public DateTime CreatedAt { get; set; }
            public DateTime LastUpdatedAt { get; set; }
            public List<ConversationTurn> History { get; set; } = new();
        }
    }
}
