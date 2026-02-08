using System;
using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using SmallMind.Abstractions;
using SmallMind.Engine;

namespace SmallMind.Benchmarks
{
    /// <summary>
    /// Benchmarks for Level 3 chat features: context policies, JSON validation, etc.
    /// </summary>
    [MemoryDiagnoser]
    [SimpleJob(warmupCount: 3, iterationCount: 10)]
    public class ChatLevel3Benchmarks
    {
        private class MockTokenCounter : ITokenCounter
        {
            public int CountTokens(string text)
            {
                if (string.IsNullOrEmpty(text))
                    return 0;
                // Simple approximation: 1 token per 4 characters
                return (text.Length + 3) / 4;
            }
        }

        private List<ChatMessageV3> _shortConversation = null!;
        private List<ChatMessageV3> _longConversation = null!;
        private KeepLastNTurnsPolicy _keepLast5Policy = null!;
        private SlidingWindowPolicy _slidingWindowPolicy = null!;
        private MockTokenCounter _tokenizer = null!;
        private JsonSchemaValidator _jsonValidator = null!;
        private string _simpleJsonSchema = null!;
        private string _complexJsonSchema = null!;
        private string _validJson = null!;
        private string _invalidJson = null!;
        private FileSessionStore _fileStore = null!;
        private ChatSessionData _sessionData = null!;

        [GlobalSetup]
        public void Setup()
        {
            _tokenizer = new MockTokenCounter();
            _keepLast5Policy = new KeepLastNTurnsPolicy(maxTurns: 5);
            _slidingWindowPolicy = new SlidingWindowPolicy();

            // Setup short conversation (10 messages)
            _shortConversation = new List<ChatMessageV3>();
            _shortConversation.Add(new ChatMessageV3 { Role = ChatRole.System, Content = "You are a helpful assistant." });
            for (int i = 0; i < 4; i++)
            {
                _shortConversation.Add(new ChatMessageV3 { Role = ChatRole.User, Content = $"Question {i + 1}: What is AI?" });
                _shortConversation.Add(new ChatMessageV3 { Role = ChatRole.Assistant, Content = $"Answer {i + 1}: AI stands for Artificial Intelligence." });
            }

            // Setup long conversation (100 messages)
            _longConversation = new List<ChatMessageV3>();
            _longConversation.Add(new ChatMessageV3 { Role = ChatRole.System, Content = "You are a helpful assistant." });
            for (int i = 0; i < 50; i++)
            {
                _longConversation.Add(new ChatMessageV3 { Role = ChatRole.User, Content = $"Question {i + 1}: Tell me about topic {i + 1}." });
                _longConversation.Add(new ChatMessageV3 { Role = ChatRole.Assistant, Content = $"Answer {i + 1}: Here is information about topic {i + 1}." });
            }

            // Setup JSON validator
            _jsonValidator = new JsonSchemaValidator();
            
            _simpleJsonSchema = @"{
                ""type"": ""object"",
                ""properties"": {
                    ""name"": { ""type"": ""string"" },
                    ""age"": { ""type"": ""number"" }
                },
                ""required"": [""name""]
            }";

            _complexJsonSchema = @"{
                ""type"": ""object"",
                ""properties"": {
                    ""users"": {
                        ""type"": ""array"",
                        ""items"": {
                            ""type"": ""object"",
                            ""properties"": {
                                ""id"": { ""type"": ""integer"" },
                                ""name"": { ""type"": ""string"", ""minLength"": 1, ""maxLength"": 100 },
                                ""email"": { ""type"": ""string"" },
                                ""role"": { ""type"": ""string"", ""enum"": [""admin"", ""user"", ""guest""] },
                                ""active"": { ""type"": ""boolean"" }
                            },
                            ""required"": [""id"", ""name"", ""email""]
                        }
                    }
                }
            }";

            _validJson = @"{ ""name"": ""Alice"", ""age"": 30 }";
            _invalidJson = @"{ ""age"": 30 }";

            // Setup file store
            var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"benchmark_{Guid.NewGuid()}");
            _fileStore = new FileSessionStore(tempDir);

            _sessionData = new ChatSessionData
            {
                SessionId = "bench-session",
                CreatedAt = DateTime.UtcNow,
                LastUpdatedAt = DateTime.UtcNow
            };

            for (int i = 0; i < 10; i++)
            {
                _sessionData.Turns.Add(new ChatTurnData
                {
                    UserMessage = $"User message {i}",
                    AssistantMessage = $"Assistant response {i}",
                    Timestamp = DateTime.UtcNow
                });
            }
        }

        // ============================================================================
        // CONTEXT POLICY BENCHMARKS
        // ============================================================================

        [Benchmark]
        public void ContextPolicy_KeepLastN_ShortConversation()
        {
            var result = _keepLast5Policy.Apply(_shortConversation, maxTokens: 1000, _tokenizer);
        }

        [Benchmark]
        public void ContextPolicy_KeepLastN_LongConversation()
        {
            var result = _keepLast5Policy.Apply(_longConversation, maxTokens: 1000, _tokenizer);
        }

        [Benchmark]
        public void ContextPolicy_SlidingWindow_ShortConversation()
        {
            var result = _slidingWindowPolicy.Apply(_shortConversation, maxTokens: 500, _tokenizer);
        }

        [Benchmark]
        public void ContextPolicy_SlidingWindow_LongConversation()
        {
            var result = _slidingWindowPolicy.Apply(_longConversation, maxTokens: 500, _tokenizer);
        }

        // ============================================================================
        // JSON SCHEMA VALIDATION BENCHMARKS
        // ============================================================================

        [Benchmark]
        public void JsonValidation_SimpleSchema_Valid()
        {
            var result = _jsonValidator.Validate(_validJson, _simpleJsonSchema);
        }

        [Benchmark]
        public void JsonValidation_SimpleSchema_Invalid()
        {
            var result = _jsonValidator.Validate(_invalidJson, _simpleJsonSchema);
        }

        [Benchmark]
        public void JsonValidation_ComplexSchema()
        {
            var complexJson = @"{
                ""users"": [
                    { ""id"": 1, ""name"": ""Alice"", ""email"": ""alice@example.com"", ""role"": ""admin"", ""active"": true },
                    { ""id"": 2, ""name"": ""Bob"", ""email"": ""bob@example.com"", ""role"": ""user"", ""active"": true }
                ]
            }";
            var result = _jsonValidator.Validate(complexJson, _complexJsonSchema);
        }

        // ============================================================================
        // FILE SESSION STORE BENCHMARKS
        // ============================================================================

        [Benchmark]
        public async System.Threading.Tasks.Task FileStore_Upsert()
        {
            await _fileStore.UpsertAsync(_sessionData);
        }

        [Benchmark]
        public async System.Threading.Tasks.Task FileStore_Get()
        {
            await _fileStore.GetAsync("bench-session");
        }

        [Benchmark]
        public async System.Threading.Tasks.Task FileStore_Exists()
        {
            await _fileStore.ExistsAsync("bench-session");
        }
    }

    public class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("SmallMind Level 3 Chat Benchmarks");
            Console.WriteLine("==================================");
            Console.WriteLine();
            Console.WriteLine("Running benchmarks...");
            Console.WriteLine();

            var summary = BenchmarkRunner.Run<ChatLevel3Benchmarks>();

            Console.WriteLine();
            Console.WriteLine("Benchmark completed!");
        }
    }
}
