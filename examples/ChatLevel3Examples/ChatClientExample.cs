using System;
using System.Collections.Generic;
using SmallMind.Abstractions;
using SmallMind.Engine;
using SmallMind;

namespace SmallMind.Examples
{
    /// <summary>
    /// Example demonstrating the Level 3 IChatClient public API.
    /// Shows how to use the clean, stable ChatClient interface.
    /// </summary>
    class ChatClientExample
    {
        static void Main(string[] args)
        {
            Console.WriteLine("SmallMind IChatClient Example");
            Console.WriteLine("=============================");
            Console.WriteLine();

            // Note: This example demonstrates the API usage pattern.
            // In a real application, you would use an actual model file.
            
            Console.WriteLine("Example 1: Basic Chat with IChatClient");
            Console.WriteLine("---------------------------------------");
            DemonstrateBasicChat();
            Console.WriteLine();

            Console.WriteLine("Example 2: Chat with Context Policy");
            Console.WriteLine("------------------------------------");
            DemonstrateWithContextPolicy();
            Console.WriteLine();

            Console.WriteLine("Example 3: Chat with Telemetry");
            Console.WriteLine("-------------------------------");
            DemonstrateWithTelemetry();
            Console.WriteLine();

            Console.WriteLine("All examples completed!");
        }

        static void DemonstrateBasicChat()
        {
            Console.WriteLine("// Create SmallMind engine");
            Console.WriteLine("var engine = SmallMindFactory.Create(new SmallMindOptions");
            Console.WriteLine("{");
            Console.WriteLine("    ModelPath = \"path/to/model.smq\",");
            Console.WriteLine("    EnableKvCache = true");
            Console.WriteLine("});");
            Console.WriteLine();

            Console.WriteLine("// Create chat client");
            Console.WriteLine("var client = engine.CreateChatClient(new ChatClientOptions");
            Console.WriteLine("{");
            Console.WriteLine("    EnableKvCache = true");
            Console.WriteLine("});");
            Console.WriteLine();

            Console.WriteLine("// Add system message");
            Console.WriteLine("client.AddSystemMessage(\"You are a helpful assistant.\");");
            Console.WriteLine();

            Console.WriteLine("// Send chat request");
            Console.WriteLine("var request = new ChatRequest");
            Console.WriteLine("{");
            Console.WriteLine("    Messages = new[]");
            Console.WriteLine("    {");
            Console.WriteLine("        new ChatMessageV3");
            Console.WriteLine("        {");
            Console.WriteLine("            Role = ChatRole.User,");
            Console.WriteLine("            Content = \"What is AI?\"");
            Console.WriteLine("        }");
            Console.WriteLine("    }");
            Console.WriteLine("};");
            Console.WriteLine();

            Console.WriteLine("var response = client.SendChat(request);");
            Console.WriteLine("Console.WriteLine($\"Response: {response.Message.Content}\");");
            Console.WriteLine("Console.WriteLine($\"Tokens: {response.Usage.TotalTokens}\");");
            Console.WriteLine("Console.WriteLine($\"Tokens/sec: {response.Usage.TokensPerSecond:F2}\");");
        }

        static void DemonstrateWithContextPolicy()
        {
            Console.WriteLine("// Create chat client with context policy");
            Console.WriteLine("var client = engine.CreateChatClient(new ChatClientOptions");
            Console.WriteLine("{");
            Console.WriteLine("    EnableKvCache = true,");
            Console.WriteLine("    DefaultContextPolicy = new KeepLastNTurnsPolicy(maxTurns: 5)");
            Console.WriteLine("});");
            Console.WriteLine();

            Console.WriteLine("// The context policy will automatically keep only the last 5 turns");
            Console.WriteLine("// when the conversation gets long, ensuring deterministic behavior.");
            Console.WriteLine();

            Console.WriteLine("// You can also override per-request:");
            Console.WriteLine("var request = new ChatRequest");
            Console.WriteLine("{");
            Console.WriteLine("    Messages = messages,");
            Console.WriteLine("    ContextPolicy = new SlidingWindowPolicy() // Override default");
            Console.WriteLine("};");
        }

        static void DemonstrateWithTelemetry()
        {
            Console.WriteLine("// Create chat client with telemetry");
            Console.WriteLine("var telemetry = new ConsoleTelemetry();");
            Console.WriteLine();

            Console.WriteLine("var client = engine.CreateChatClient(new ChatClientOptions");
            Console.WriteLine("{");
            Console.WriteLine("    EnableKvCache = true,");
            Console.WriteLine("    DefaultTelemetry = telemetry");
            Console.WriteLine("});");
            Console.WriteLine();

            Console.WriteLine("// Telemetry will log:");
            Console.WriteLine("// - Request start");
            Console.WriteLine("// - First token time (TTFT)");
            Console.WriteLine("// - Context policy actions");
            Console.WriteLine("// - KV cache hits/misses");
            Console.WriteLine("// - Request completion with usage stats");
            Console.WriteLine();

            Console.WriteLine("// Example output:");
            Console.WriteLine("// [session-123] Request started with 2 messages");
            Console.WriteLine("// [session-123] First token: 42.50ms (TTFT)");
            Console.WriteLine("// [session-123] Context policy 'KeepLastNTurns': 1000 → 500 tokens");
            Console.WriteLine("// [session-123] KV cache HIT: 250 cached tokens");
            Console.WriteLine("// [session-123] Completed: 100 prompt + 50 completion = 150 total");
            Console.WriteLine("// [session-123] Performance: 25.30 tok/s");
        }
    }
}
