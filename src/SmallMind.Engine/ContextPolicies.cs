using System;
using System.Collections.Generic;
using System.Linq;
using SmallMind.Abstractions;

namespace SmallMind.Engine
{
    /// <summary>
    /// Context policy that keeps the last N turns, always pinning system messages.
    /// </summary>
    public sealed class KeepLastNTurnsPolicy : IContextPolicy
    {
        private readonly int _maxTurns;

        /// <summary>
        /// Initializes a new instance with the specified maximum turns.
        /// </summary>
        /// <param name="maxTurns">Maximum number of turns to keep (excludes system messages).</param>
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
            var systemMessages = new List<ChatMessageV3>();
            var conversationMessages = new List<ChatMessageV3>();

            foreach (var msg in messages)
            {
                if (msg.Role == ChatRole.System)
                    systemMessages.Add(msg);
                else
                    conversationMessages.Add(msg);
            }

            // Keep last N conversation messages
            var keptConversation = conversationMessages.Count > _maxTurns
                ? conversationMessages.Skip(conversationMessages.Count - _maxTurns).ToList()
                : conversationMessages;

            // Combine: system messages first, then conversation
            var result = new List<ChatMessageV3>(systemMessages.Count + keptConversation.Count);
            result.AddRange(systemMessages);
            result.AddRange(keptConversation);

            // Now apply token budget
            return FitToBudget(result, maxTokens, tokenizer);
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

            // Need to truncate - keep system messages, then as many conversation messages as fit
            var systemMessages = messages.Where(m => m.Role == ChatRole.System).ToList();
            var conversationMessages = messages.Where(m => m.Role != ChatRole.System).ToList();

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
            var result = new List<ChatMessageV3>(systemMessages);

            // Add conversation messages from most recent, going backwards
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
    /// Simple sliding window policy that keeps most recent messages within budget.
    /// Always pins system messages at the start.
    /// </summary>
    public sealed class SlidingWindowPolicy : IContextPolicy
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
            var systemMessages = new List<ChatMessageV3>();
            var conversationMessages = new List<ChatMessageV3>();

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
    public sealed class KeepAllPolicy : IContextPolicy
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
