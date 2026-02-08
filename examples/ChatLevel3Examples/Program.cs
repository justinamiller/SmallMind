using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using SmallMind.Abstractions;
using SmallMind.Engine;

namespace SmallMind.Examples
{
    /// <summary>
    /// Examples demonstrating Level 3 Chat features:
    /// - Messages-first design
    /// - Context policies
    /// - JSON schema validation
    /// - File-based persistence
    /// - Telemetry
    /// </summary>
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("SmallMind Level 3 Chat Examples");
            Console.WriteLine("================================");
            Console.WriteLine();

            // Example 1: Basic context policy usage
            Example1_ContextPolicies();
            Console.WriteLine();

            // Example 2: JSON schema validation
            Example2_JsonSchemaValidation();
            Console.WriteLine();

            // Example 3: File-based session persistence
            await Example3_SessionPersistence();
            Console.WriteLine();

            // Example 4: Telemetry
            Example4_Telemetry();
            Console.WriteLine();

            Console.WriteLine("All examples completed!");
        }

        static void Example1_ContextPolicies()
        {
            Console.WriteLine("Example 1: Context Policies");
            Console.WriteLine("---------------------------");

            // Create a simple tokenizer (in real usage, use actual tokenizer)
            var tokenizer = new SimpleTokenCounter();

            // Create a conversation
            var messages = new List<ChatMessageV3>
            {
                new() { Role = ChatRole.System, Content = "You are a helpful assistant." },
                new() { Role = ChatRole.User, Content = "What is AI?" },
                new() { Role = ChatRole.Assistant, Content = "AI stands for Artificial Intelligence." },
                new() { Role = ChatRole.User, Content = "Tell me more." },
                new() { Role = ChatRole.Assistant, Content = "AI involves machine learning and deep learning." },
                new() { Role = ChatRole.User, Content = "What are neural networks?" }
            };

            Console.WriteLine($"Original conversation: {messages.Count} messages");

            // Apply KeepLastNTurnsPolicy
            var policy = new KeepLastNTurnsPolicy(maxTurns: 2);
            var filtered = policy.Apply(messages, maxTokens: 1000, tokenizer);
            Console.WriteLine($"After KeepLastNTurnsPolicy(2): {filtered.Count} messages");
            Console.WriteLine($"  Deterministic: {policy.IsDeterministic}");

            // Apply SlidingWindowPolicy
            var slidingPolicy = new SlidingWindowPolicy();
            var slideFiltered = slidingPolicy.Apply(messages, maxTokens: 30, tokenizer);
            Console.WriteLine($"After SlidingWindowPolicy (budget=30): {slideFiltered.Count} messages");
        }

        static void Example2_JsonSchemaValidation()
        {
            Console.WriteLine("Example 2: JSON Schema Validation");
            Console.WriteLine("----------------------------------");

            var validator = new JsonSchemaValidator();

            // Define a schema
            var schema = @"{
                ""type"": ""object"",
                ""properties"": {
                    ""name"": { ""type"": ""string"", ""minLength"": 1 },
                    ""age"": { ""type"": ""number"", ""minimum"": 0, ""maximum"": 150 },
                    ""email"": { ""type"": ""string"" }
                },
                ""required"": [""name"", ""email""]
            }";

            // Valid JSON
            var validJson = @"{ ""name"": ""Alice"", ""age"": 30, ""email"": ""alice@example.com"" }";
            var validResult = validator.Validate(validJson, schema);
            Console.WriteLine($"Valid JSON: IsValid = {validResult.IsValid}");

            // Invalid JSON (missing required field)
            var invalidJson = @"{ ""name"": ""Bob"" }";
            var invalidResult = validator.Validate(invalidJson, schema);
            Console.WriteLine($"Invalid JSON: IsValid = {invalidResult.IsValid}");
            if (!invalidResult.IsValid)
            {
                Console.WriteLine($"  Errors:");
                foreach (var error in invalidResult.Errors)
                {
                    Console.WriteLine($"    - {error}");
                }
            }
        }

        static async Task Example3_SessionPersistence()
        {
            Console.WriteLine("Example 3: Session Persistence");
            Console.WriteLine("------------------------------");

            var storageDir = Path.Combine(Path.GetTempPath(), "smallmind-example-sessions");
            var store = new FileSessionStore(storageDir);

            // Create a session
            var session = new ChatSessionData
            {
                SessionId = "example-session-123",
                CreatedAt = DateTime.UtcNow,
                LastUpdatedAt = DateTime.UtcNow
            };

            session.Turns.Add(new ChatTurnData
            {
                UserMessage = "Hello!",
                AssistantMessage = "Hi! How can I help you today?",
                Timestamp = DateTime.UtcNow
            });

            session.Turns.Add(new ChatTurnData
            {
                UserMessage = "What's the weather?",
                AssistantMessage = "I don't have access to weather data, but you can check weather.com!",
                Timestamp = DateTime.UtcNow
            });

            // Save session
            await store.UpsertAsync(session);
            Console.WriteLine($"Saved session: {session.SessionId}");
            Console.WriteLine($"  Turns: {session.Turns.Count}");

            // Load session
            var loaded = await store.GetAsync("example-session-123");
            if (loaded != null)
            {
                Console.WriteLine($"Loaded session: {loaded.SessionId}");
                Console.WriteLine($"  Turns: {loaded.Turns.Count}");
                Console.WriteLine($"  First turn: {loaded.Turns[0].UserMessage}");
            }

            // Check existence
            var exists = await store.ExistsAsync("example-session-123");
            Console.WriteLine($"Session exists: {exists}");

            // Clean up
            await store.DeleteAsync("example-session-123");
            Console.WriteLine("Session deleted.");
        }

        static void Example4_Telemetry()
        {
            Console.WriteLine("Example 4: Telemetry");
            Console.WriteLine("--------------------");

            // Use console telemetry
            var telemetry = new ConsoleTelemetry();

            // Simulate request lifecycle
            telemetry.OnRequestStart("session-123", messageCount: 5);
            telemetry.OnFirstToken("session-123", elapsedMs: 42.5);
            telemetry.OnContextPolicyApplied("session-123", "KeepLastNTurns", originalTokens: 1000, finalTokens: 500);
            telemetry.OnKvCacheAccess("session-123", hit: true, cachedTokens: 250);
            telemetry.OnToolCall("session-123", toolName: "search", elapsedMs: 123.4);

            var usage = new UsageStats
            {
                PromptTokens = 100,
                CompletionTokens = 50,
                TimeToFirstTokenMs = 42.5,
                TokensPerSecond = 25.3
            };
            telemetry.OnRequestComplete("session-123", usage);

            Console.WriteLine();
            Console.WriteLine("Note: In production, you can implement your own IChatTelemetry");
            Console.WriteLine("to integrate with your monitoring system.");
        }

        // Simple token counter for demo purposes
        class SimpleTokenCounter : ITokenCounter
        {
            public int CountTokens(string text)
            {
                if (string.IsNullOrEmpty(text))
                    return 0;
                // Rough estimate: 1 token per 4 characters
                return (text.Length + 3) / 4;
            }
        }
    }
}
