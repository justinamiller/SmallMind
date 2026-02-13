using System;
using System.Threading;
using System.Threading.Tasks;
using SmallMind.Abstractions;
using SmallMind.Engine;

namespace SmallMind.QuickStart
{
    /// <summary>
    /// QuickStart guide demonstrating the stable SmallMind public API.
    /// This sample shows the "golden path" for using SmallMind:
    /// 1. Load a model
    /// 2. Generate text with streaming
    /// 3. Create a chat session
    /// 4. (Optional) Use RAG for document-based QA
    /// </summary>
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("===========================================");
            Console.WriteLine("SmallMind QuickStart - Stable API Demo");
            Console.WriteLine("===========================================\n");

            // Create engine with default options
            using var engine = SmallMind.Engine.SmallMind.Create(new SmallMindOptions
            {
                EnableDeterministicMode = false,
                EnableKvCache = true,
                EnableRag = true
            });

            Console.WriteLine("✓ SmallMind engine created");
            Console.WriteLine($"  - Streaming: {engine.Capabilities.SupportsStreaming}");
            Console.WriteLine($"  - KV Cache: {engine.Capabilities.SupportsKvCache}");
            Console.WriteLine($"  - Deterministic: {engine.Capabilities.SupportsDeterministicMode}");
            Console.WriteLine($"  - RAG: {engine.Capabilities.SupportsRag}");
            Console.WriteLine();

            // Example 1: Load a model (create a simple one for demo)
            Console.WriteLine("Example 1: Loading a model");
            Console.WriteLine("===========================");
            
            // Note: In production, you would load an actual .smq or .gguf file
            // For this demo, we'll show what the API looks like (commented out)
            
            /*
            var model = await engine.LoadModelAsync(new ModelLoadRequest
            {
                Path = "path/to/model.smq",
                Threads = Environment.ProcessorCount,
                AllowGgufImport = true // Allows .gguf files
            });

            Console.WriteLine($"✓ Model loaded: {model.Info.Name}");
            Console.WriteLine($"  - Vocab size: {model.Info.VocabSize}");
            Console.WriteLine($"  - Max context: {model.Info.MaxContextLength}");
            Console.WriteLine();
            */

            Console.WriteLine("  [Demo mode: Model loading requires an actual .smq file]");
            Console.WriteLine("  Example API usage:");
            Console.WriteLine("    var model = await engine.LoadModelAsync(new ModelLoadRequest");
            Console.WriteLine("    {");
            Console.WriteLine("        Path = \"model.smq\",");
            Console.WriteLine("        AllowGgufImport = true");
            Console.WriteLine("    });");
            Console.WriteLine();

            // Example 2: Streaming generation (demo)
            Console.WriteLine("Example 2: Streaming text generation");
            Console.WriteLine("=====================================");
            Console.WriteLine("  [Demo mode: Requires loaded model]");
            Console.WriteLine("  Example API usage:");
            Console.WriteLine("    await foreach (var token in engine.GenerateStreamingAsync(model, request))");
            Console.WriteLine("    {");
            Console.WriteLine("        if (token.Kind == TokenEventKind.Token)");
            Console.WriteLine("            Console.Write(token.Text.ToString());");
            Console.WriteLine("    }");
            Console.WriteLine();

            /*
            var request = new GenerationRequest
            {
                Prompt = "Once upon a time",
                Options = new GenerationOptions
                {
                    MaxNewTokens = 50,
                    Temperature = 0.8,
                    TopK = 40,
                    TopP = 0.95,
                    Mode = GenerationMode.Exploratory
                }
            };

            Console.Write("Generated: ");
            await foreach (var token in engine.GenerateStreamingAsync(model, request))
            {
                if (token.Kind == TokenEventKind.Token)
                {
                    Console.Write(token.Text.ToString());
                }
                else if (token.Kind == TokenEventKind.Completed)
                {
                    Console.WriteLine($"\n\n✓ Generated {token.GeneratedTokens} tokens");
                }
            }
            Console.WriteLine();
            */

            // Example 3: Chat session (demo)
            Console.WriteLine("Example 3: Chat session with KV cache");
            Console.WriteLine("=====================================");
            Console.WriteLine("  [Demo mode: Requires loaded model]");
            Console.WriteLine("  Example API usage:");
            Console.WriteLine("    using var chat = engine.CreateChatSession(model, new SessionOptions");
            Console.WriteLine("    {");
            Console.WriteLine("        SessionId = \"user-123\",");
            Console.WriteLine("        EnableKvCache = true");
            Console.WriteLine("    });");
            Console.WriteLine();
            Console.WriteLine("    await chat.AddSystemAsync(\"You are a helpful assistant.\");");
            Console.WriteLine();
            Console.WriteLine("    var response = await chat.SendAsync(");
            Console.WriteLine("        new ChatMessage { Role = ChatRole.User, Content = \"Hello!\" },");
            Console.WriteLine("        new GenerationOptions { MaxNewTokens = 50 });");
            Console.WriteLine();
            Console.WriteLine("    Console.WriteLine($\"Assistant: {response.Text}\");");
            Console.WriteLine();

            /*
            using var chat = engine.CreateChatSession(model, new SessionOptions
            {
                SessionId = "demo-session",
                EnableKvCache = true
            });

            await chat.AddSystemAsync("You are a helpful assistant.");

            var userMessage = new ChatMessage
            {
                Role = ChatRole.User,
                Content = "What is the capital of France?"
            };

            var response = await chat.SendAsync(userMessage, new GenerationOptions
            {
                MaxNewTokens = 50,
                Temperature = 0.7
            });

            Console.WriteLine($"User: {userMessage.Content}");
            Console.WriteLine($"Assistant: {response.Text}");
            Console.WriteLine($"✓ Session has {chat.Info.TurnCount} turns");
            Console.WriteLine();
            */

            // Example 4: RAG (demo)
            if (engine.Rag != null)
            {
                Console.WriteLine("Example 4: RAG (Document Q&A with Citations)");
                Console.WriteLine("============================================");
                Console.WriteLine("  [Demo mode: Requires documents to index]");
                Console.WriteLine("  Example API usage:");
                Console.WriteLine("    var index = await engine.Rag.BuildIndexAsync(new RagBuildRequest");
                Console.WriteLine("    {");
                Console.WriteLine("        SourcePaths = new[] { \"docs\" },");
                Console.WriteLine("        IndexDirectory = \"rag-index\",");
                Console.WriteLine("        ChunkSize = 512,");
                Console.WriteLine("        ChunkOverlap = 128");
                Console.WriteLine("    });");
                Console.WriteLine();
                Console.WriteLine("    var answer = await engine.Rag.AskAsync(model, new RagAskRequest");
                Console.WriteLine("    {");
                Console.WriteLine("        Query = \"What is SmallMind?\",");
                Console.WriteLine("        Index = index,");
                Console.WriteLine("        TopK = 5,");
                Console.WriteLine("        MinConfidence = 0.3");
                Console.WriteLine("    });");
                Console.WriteLine();
                Console.WriteLine("    Console.WriteLine($\"Answer: {answer.Answer}\");");
                Console.WriteLine("    foreach (var citation in answer.Citations)");
                Console.WriteLine("        Console.WriteLine($\"  - {citation.SourceUri}: {citation.Snippet}\");");
                Console.WriteLine();

                /*
                var index = await engine.Rag.BuildIndexAsync(new RagBuildRequest
                {
                    SourcePaths = new[] { "path/to/documents" },
                    IndexDirectory = "rag-index",
                    ChunkSize = 512,
                    ChunkOverlap = 128
                });

                var answer = await engine.Rag.AskAsync(model, new RagAskRequest
                {
                    Query = "What is SmallMind?",
                    Index = index,
                    TopK = 5,
                    MinConfidence = 0.3,
                    GenerationOptions = new GenerationOptions { MaxNewTokens = 200 }
                });

                Console.WriteLine($"Question: What is SmallMind?");
                Console.WriteLine($"Answer: {answer.Answer}");
                Console.WriteLine($"\nCitations ({answer.Citations.Length}):");
                foreach (var citation in answer.Citations)
                {
                    Console.WriteLine($"  [{citation.Confidence:F2}] {citation.SourceUri}");
                    Console.WriteLine($"      {citation.Snippet}");
                }
                Console.WriteLine();
                */
            }

            // Example 5: Deterministic generation
            Console.WriteLine("Example 5: Deterministic generation (for testing)");
            Console.WriteLine("=================================================");
            Console.WriteLine("  [Demo mode: Requires loaded model]");
            Console.WriteLine("  Example API usage:");
            Console.WriteLine("    var options = new GenerationOptions");
            Console.WriteLine("    {");
            Console.WriteLine("        Mode = GenerationMode.Deterministic,");
            Console.WriteLine("        Seed = 42,");
            Console.WriteLine("        MaxNewTokens = 10");
            Console.WriteLine("    };");
            Console.WriteLine();
            Console.WriteLine("    // Same seed + prompt = identical output");
            Console.WriteLine("    var result1 = await engine.GenerateAsync(model, new GenerationRequest");
            Console.WriteLine("    {");
            Console.WriteLine("        Prompt = \"Test\",");
            Console.WriteLine("        Options = options");
            Console.WriteLine("    });");
            Console.WriteLine();
            Console.WriteLine("    var result2 = await engine.GenerateAsync(model, new GenerationRequest");
            Console.WriteLine("    {");
            Console.WriteLine("        Prompt = \"Test\",");
            Console.WriteLine("        Options = options");
            Console.WriteLine("    });");
            Console.WriteLine();
            Console.WriteLine("    // result1.Text == result2.Text (guaranteed)");
            Console.WriteLine();

            Console.WriteLine("===========================================");
            Console.WriteLine("QuickStart Complete!");
            Console.WriteLine("===========================================");
            Console.WriteLine();
            Console.WriteLine("Next steps:");
            Console.WriteLine("  1. Train or download a model in .smq format");
            Console.WriteLine("  2. Update the ModelLoadRequest path to point to your model");
            Console.WriteLine("  3. Uncomment the example code above");
            Console.WriteLine("  4. Run this sample to see the API in action");
            Console.WriteLine();
            Console.WriteLine("For more details, see:");
            Console.WriteLine("  - docs/quickstart.md");
            Console.WriteLine("  - docs/api-contract.md");
            Console.WriteLine("  - docs/operational-notes.md");
        }
    }
}
