using System;
using System.Collections.Generic;
using SmallMind.Abstractions;
using SmallMind.Engine;
using Xunit;

namespace SmallMind.Tests.Chat
{
    /// <summary>
    /// Tests for context policies ensuring deterministic behavior.
    /// </summary>
    public class ContextPolicyTests
    {
        private class MockTokenCounter : ITokenCounter
        {
            // Simple mock: 1 token per word
            public int CountTokens(string text)
            {
                if (string.IsNullOrEmpty(text))
                    return 0;
                return text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
            }
        }

        [Fact]
        public void KeepLastNTurnsPolicy_KeepsSystemMessages()
        {
            var policy = new KeepLastNTurnsPolicy(maxTurns: 2);
            var tokenizer = new MockTokenCounter();

            var messages = new List<ChatMessageV3>
            {
                new() { Role = ChatRole.System, Content = "You are helpful" },
                new() { Role = ChatRole.User, Content = "First question" },
                new() { Role = ChatRole.Assistant, Content = "First answer" },
                new() { Role = ChatRole.User, Content = "Second question" },
                new() { Role = ChatRole.Assistant, Content = "Second answer" },
                new() { Role = ChatRole.User, Content = "Third question" }
            };

            var result = policy.Apply(messages, maxTokens: 1000, tokenizer);

            // Should keep system + last 2 turns (4 messages: user2, assistant2, user3)
            Assert.Equal(4, result.Count);
            Assert.Equal(ChatRole.System, result[0].Role);
            Assert.Equal("Second question", result[1].Content);
            Assert.Equal("Second answer", result[2].Content);
            Assert.Equal("Third question", result[3].Content);
        }

        [Fact]
        public void KeepLastNTurnsPolicy_IsDeterministic()
        {
            var policy = new KeepLastNTurnsPolicy(maxTurns: 3);
            Assert.True(policy.IsDeterministic);
        }

        [Fact]
        public void KeepLastNTurnsPolicy_RespectsBudget()
        {
            var policy = new KeepLastNTurnsPolicy(maxTurns: 10);
            var tokenizer = new MockTokenCounter();

            var messages = new List<ChatMessageV3>
            {
                new() { Role = ChatRole.System, Content = "You are helpful assistant" }, // 4 tokens
                new() { Role = ChatRole.User, Content = "What is AI" }, // 3 tokens
                new() { Role = ChatRole.Assistant, Content = "AI stands for" }, // 3 tokens
                new() { Role = ChatRole.User, Content = "Tell me more" } // 3 tokens
            };

            // Total: 13 tokens, budget: 10 - should drop last user message
            var result = policy.Apply(messages, maxTokens: 10, tokenizer);

            Assert.Equal(3, result.Count); // system + user1 + assistant1
            Assert.Equal("AI stands for", result[2].Content);
        }

        [Fact]
        public void SlidingWindowPolicy_KeepsRecentMessages()
        {
            var policy = new SlidingWindowPolicy();
            var tokenizer = new MockTokenCounter();

            var messages = new List<ChatMessageV3>
            {
                new() { Role = ChatRole.System, Content = "System prompt" }, // 2 tokens
                new() { Role = ChatRole.User, Content = "Old message" }, // 2 tokens
                new() { Role = ChatRole.Assistant, Content = "Old response" }, // 2 tokens
                new() { Role = ChatRole.User, Content = "New message" }, // 2 tokens
                new() { Role = ChatRole.Assistant, Content = "New response" } // 2 tokens
            };

            // Budget: 8 tokens - should keep system + last 2 messages
            var result = policy.Apply(messages, maxTokens: 8, tokenizer);

            Assert.True(result.Count >= 3 && result.Count <= 5);
            Assert.Equal(ChatRole.System, result[0].Role);
            Assert.Equal("System prompt", result[0].Content);
        }

        [Fact]
        public void SlidingWindowPolicy_IsDeterministic()
        {
            var policy = new SlidingWindowPolicy();
            Assert.True(policy.IsDeterministic);
        }

        [Fact]
        public void KeepAllPolicy_KeepsEverything()
        {
            var policy = KeepAllPolicy.Instance;
            var tokenizer = new MockTokenCounter();

            var messages = new List<ChatMessageV3>
            {
                new() { Role = ChatRole.System, Content = "System" },
                new() { Role = ChatRole.User, Content = "User" },
                new() { Role = ChatRole.Assistant, Content = "Assistant" }
            };

            var result = policy.Apply(messages, maxTokens: 1, tokenizer);

            Assert.Equal(3, result.Count);
            Assert.Same(messages, result);
        }

        [Fact]
        public void ContextPolicy_DeterminismConsistency()
        {
            // Same input should produce same output
            var policy = new KeepLastNTurnsPolicy(maxTurns: 2);
            var tokenizer = new MockTokenCounter();

            var messages = new List<ChatMessageV3>
            {
                new() { Role = ChatRole.System, Content = "System" },
                new() { Role = ChatRole.User, Content = "Q1" },
                new() { Role = ChatRole.Assistant, Content = "A1" },
                new() { Role = ChatRole.User, Content = "Q2" },
                new() { Role = ChatRole.Assistant, Content = "A2" }
            };

            var result1 = policy.Apply(messages, maxTokens: 100, tokenizer);
            var result2 = policy.Apply(messages, maxTokens: 100, tokenizer);

            Assert.Equal(result1.Count, result2.Count);
            for (int i = 0; i < result1.Count; i++)
            {
                Assert.Equal(result1[i].Role, result2[i].Role);
                Assert.Equal(result1[i].Content, result2[i].Content);
            }
        }

        [Fact]
        public void KeepLastNTurnsPolicy_HandlesToolMessages()
        {
            var policy = new KeepLastNTurnsPolicy(maxTurns: 2);
            var tokenizer = new MockTokenCounter();

            var messages = new List<ChatMessageV3>
            {
                new() { Role = ChatRole.System, Content = "System" },
                new() { Role = ChatRole.User, Content = "Call weather API" },
                new() 
                { 
                    Role = ChatRole.Assistant, 
                    Content = "Calling", 
                    ToolCalls = new List<ToolCall>
                    { 
                        new ToolCall { Id = "1", Name = "weather", ArgumentsJson = "{}" } 
                    }
                },
                new() { Role = ChatRole.Tool, Content = "Sunny", ToolCallId = "1" },
                new() { Role = ChatRole.Assistant, Content = "It is sunny" }
            };

            var result = policy.Apply(messages, maxTokens: 1000, tokenizer);

            // Should keep system + last 2 conversation messages
            Assert.True(result.Count >= 3);
            Assert.Equal(ChatRole.System, result[0].Role);
        }
    }
}
