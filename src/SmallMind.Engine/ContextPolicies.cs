using SmallMind.Abstractions;

namespace SmallMind.Engine
{
    /// <summary>
    /// Context policy that keeps the last N turns, always pinning system messages.
    /// A "turn" consists of a user message and optionally its assistant response.
    /// </summary>
    internal sealed class KeepLastNTurnsPolicy : IContextPolicy
    {
        private readonly int _maxTurns;

        /// <summary>
        /// Initializes a new instance with the specified maximum turns.
        /// </summary>
        /// <param name="maxTurns">Maximum number of turns to keep (a turn = user + optional assistant).</param>
        public KeepLastNTurnsPolicy(int maxTurns)
        {
            if (maxTurns <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxTurns), "Must be positive");
            _maxTurns = maxTurns;
        }

        public bool IsDeterministic => true;

        public IReadOnlyList<ChatMessageV3> Apply(
            IReadOnlyList<ChatMessageV3> messages,
            int maxTokens,
            ITokenCounter tokenizer)
        {
            if (messages == null || messages.Count == 0)
                return Array.Empty<ChatMessageV3>();

            // Separate system messages from conversation messages
            // Pre-allocate capacity to minimize resize operations (performance optimization, avoiding LINQ overhead)
            // Using conservative estimates: system messages are typically few (4-8), most are conversation
            var systemMessages = new List<ChatMessageV3>(Math.Min(8, messages.Count));
            var conversationMessages = new List<ChatMessageV3>(messages.Count);

            foreach (var msg in messages)
            {
                if (msg.Role == ChatRole.System)
                    systemMessages.Add(msg);
                else
                    conversationMessages.Add(msg);
            }

            // Count turns and keep last N
            // A turn is: User (required) + Assistant (optional) + Tool messages (optional)
            var keptConversation = KeepLastTurns(conversationMessages, _maxTurns);

            // Combine: system messages first, then conversation
            var result = new List<ChatMessageV3>(systemMessages.Count + keptConversation.Count);
            result.AddRange(systemMessages);
            result.AddRange(keptConversation);

            // Now apply token budget
            return FitToBudget(result, maxTokens, tokenizer);
        }

        private static List<ChatMessageV3> KeepLastTurns(List<ChatMessageV3> messages, int maxTurns)
        {
            if (messages.Count == 0 || maxTurns <= 0)
                return new List<ChatMessageV3>();

            // Group messages into turns (user message starts a turn)
            var turns = new List<List<ChatMessageV3>>();
            List<ChatMessageV3>? currentTurn = null;

            foreach (var msg in messages)
            {
                if (msg.Role == ChatRole.User)
                {
                    // Start a new turn
                    currentTurn = new List<ChatMessageV3> { msg };
                    turns.Add(currentTurn);
                }
                else if (currentTurn != null)
                {
                    // Add to current turn (assistant or tool)
                    currentTurn.Add(msg);
                }
                // else: orphaned assistant/tool message, skip (shouldn't happen)
            }

            // Keep last N turns - optimized to avoid Skip().ToList() allocation
            int startIndex = turns.Count > maxTurns ? turns.Count - maxTurns : 0;
            int turnCount = turns.Count - startIndex;

            // Flatten back to message list - pre-calculate capacity to avoid resizes
            int messageCapacity = 0;
            for (int i = startIndex; i < turns.Count; i++)
            {
                messageCapacity += turns[i].Count;
            }

            var result = new List<ChatMessageV3>(messageCapacity);
            for (int i = startIndex; i < turns.Count; i++)
            {
                result.AddRange(turns[i]);
            }

            return result;
        }

        private static IReadOnlyList<ChatMessageV3> FitToBudget(
            List<ChatMessageV3> messages,
            int maxTokens,
            ITokenCounter tokenizer)
        {
            // Count total tokens
            int totalTokens = 0;
            foreach (var msg in messages)
            {
                totalTokens += tokenizer.CountTokens(msg.Content);
            }

            if (totalTokens <= maxTokens)
                return messages;

            // Need to truncate - keep system messages first
            // Replace LINQ with manual loops to avoid Where().ToList() allocation
            var systemMessages = new List<ChatMessageV3>(Math.Min(8, messages.Count));
            var conversationMessages = new List<ChatMessageV3>(messages.Count);
            
            for (int i = 0; i < messages.Count; i++)
            {
                if (messages[i].Role == ChatRole.System)
                    systemMessages.Add(messages[i]);
                else
                    conversationMessages.Add(messages[i]);
            }

            int systemTokens = 0;
            foreach (var msg in systemMessages)
            {
                systemTokens += tokenizer.CountTokens(msg.Content);
            }

            if (systemTokens >= maxTokens)
            {
                // Even system messages don't fit - just return them (will error downstream)
                return systemMessages;
            }

            int remainingBudget = maxTokens - systemTokens;

            // Add conversation messages in chronological order until budget exhausted
            var result = new List<ChatMessageV3>(systemMessages);
            for (int i = 0; i < conversationMessages.Count; i++)
            {
                int msgTokens = tokenizer.CountTokens(conversationMessages[i].Content);
                if (msgTokens <= remainingBudget)
                {
                    result.Add(conversationMessages[i]);
                    remainingBudget -= msgTokens;
                }
                else
                {
                    // Can't fit this message, stop
                    break;
                }
            }

            return result;
        }
    }

    /// <summary>
    /// Simple sliding window policy that keeps most recent messages within budget.
    /// Always pins system messages at the start.
    /// </summary>
    internal sealed class SlidingWindowPolicy : IContextPolicy
    {
        public bool IsDeterministic => true;

        public IReadOnlyList<ChatMessageV3> Apply(
            IReadOnlyList<ChatMessageV3> messages,
            int maxTokens,
            ITokenCounter tokenizer)
        {
            if (messages == null || messages.Count == 0)
                return Array.Empty<ChatMessageV3>();

            // Separate system messages from conversation messages
            // Pre-allocate capacity to minimize resize operations (performance optimization, avoiding LINQ overhead)
            // Using conservative estimates: system messages are typically few (4-8), most are conversation
            var systemMessages = new List<ChatMessageV3>(Math.Min(8, messages.Count));
            var conversationMessages = new List<ChatMessageV3>(messages.Count);

            foreach (var msg in messages)
            {
                if (msg.Role == ChatRole.System)
                    systemMessages.Add(msg);
                else
                    conversationMessages.Add(msg);
            }

            // Count system tokens
            int systemTokens = 0;
            foreach (var msg in systemMessages)
            {
                systemTokens += tokenizer.CountTokens(msg.Content);
            }

            if (systemTokens >= maxTokens)
            {
                // System messages alone exceed budget
                return systemMessages;
            }

            int remainingBudget = maxTokens - systemTokens;
            var result = new List<ChatMessageV3>(systemMessages);

            // Add conversation messages from most recent
            for (int i = conversationMessages.Count - 1; i >= 0; i--)
            {
                int msgTokens = tokenizer.CountTokens(conversationMessages[i].Content);
                if (msgTokens <= remainingBudget)
                {
                    result.Insert(systemMessages.Count, conversationMessages[i]);
                    remainingBudget -= msgTokens;
                }
                else
                {
                    break;
                }
            }

            return result;
        }
    }

    /// <summary>
    /// Policy that keeps all messages (no truncation).
    /// Will fail if messages exceed budget - use for testing or when budget is guaranteed.
    /// </summary>
    internal sealed class KeepAllPolicy : IContextPolicy
    {
        public static readonly KeepAllPolicy Instance = new KeepAllPolicy();

        public bool IsDeterministic => true;

        public IReadOnlyList<ChatMessageV3> Apply(
            IReadOnlyList<ChatMessageV3> messages,
            int maxTokens,
            ITokenCounter tokenizer)
        {
            return messages;
        }
    }

    /// <summary>
    /// Adapter to use SmallMind tokenizer as ITokenCounter.
    /// </summary>
    internal sealed class TokenizerAdapter : ITokenCounter
    {
        private readonly Tokenizers.ITokenizer _tokenizer;

        public TokenizerAdapter(Tokenizers.ITokenizer tokenizer)
        {
            _tokenizer = tokenizer ?? throw new ArgumentNullException(nameof(tokenizer));
        }

        public int CountTokens(string text)
        {
            if (string.IsNullOrEmpty(text))
                return 0;

            return _tokenizer.Encode(text).Count;
        }
    }
}
