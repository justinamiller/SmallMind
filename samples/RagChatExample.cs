using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using SmallMind.Chat;
using SmallMind.Retrieval;

namespace SmallMind.Samples
{
    /// <summary>
    /// Example demonstrating RAG (Retrieval-Augmented Generation) with multi-turn chat.
    /// Shows document ingestion, retrieval, citation, and conversation context.
    /// </summary>
    public class RagChatExample
    {
        public static async Task Main(string[] args)
        {
            Console.WriteLine("=== SmallMind RAG Chat Example ===\n");
            Console.WriteLine("This example demonstrates:");
            Console.WriteLine("1. Document ingestion and chunking");
            Console.WriteLine("2. In-memory lexical index (BM25-based retrieval)");
            Console.WriteLine("3. Multi-turn chat with RAG");
            Console.WriteLine("4. Citation and source tracking\n");

            // Note: This example shows the RAG infrastructure setup.
            // To run with an actual trained model, load your model first.
            Console.WriteLine("SETUP PHASE:");
            Console.WriteLine("-----------\n");

            // Step 1: Create retrieval index
            Console.WriteLine("Step 1: Creating in-memory lexical index...");
            var chunkingOptions = new ChunkingOptions
            {
                MaxChars = 500,
                OverlapChars = 50,
                MinChunkChars = 50,
                PreferParagraphBoundaries = true,
                PreferSentenceBoundaries = true
            };

            var retrievalIndex = new InMemoryLexicalIndex(chunkingOptions);
            Console.WriteLine("✓ Index created\n");

            // Step 2: Load and index sample documents
            Console.WriteLine("Step 2: Loading sample documents...");
            var documents = LoadSampleDocuments();
            
            foreach (var doc in documents)
            {
                Console.WriteLine($"  - Indexing: {doc.Title}");
                retrievalIndex.Upsert(doc);
            }
            Console.WriteLine($"✓ Indexed {documents.Count} documents\n");

            // Step 3: Create session store
            Console.WriteLine("Step 3: Creating session store...");
            var sessionStore = new InMemorySessionStore();
            Console.WriteLine("✓ Session store ready\n");

            // Step 4: Demonstrate retrieval
            Console.WriteLine("\nRETRIEVAL DEMONSTRATION:");
            Console.WriteLine("----------------------\n");

            DemonstrateRetrieval(retrievalIndex);

            // Step 5: Demonstrate RAG prompt building
            Console.WriteLine("\nRAG PROMPT DEMONSTRATION:");
            Console.WriteLine("------------------------\n");

            DemonstrateRagPromptBuilding(retrievalIndex);

            // Step 6: Show multi-turn chat structure
            Console.WriteLine("\nMULTI-TURN CHAT STRUCTURE:");
            Console.WriteLine("-------------------------\n");

            DemonstrateChatSession(sessionStore);

            Console.WriteLine("\n=== Example Complete ===");
            Console.WriteLine("\nTo run with a real model:");
            Console.WriteLine("1. Train SmallMind on your data");
            Console.WriteLine("2. Load the trained model and tokenizer");
            Console.WriteLine("3. Create ChatOrchestrator with model, tokenizer, and index");
            Console.WriteLine("4. Call orchestrator.AskAsync() for each user message\n");
        }

        /// <summary>
        /// Load sample documents from the docs directory.
        /// </summary>
        private static List<Document> LoadSampleDocuments()
        {
            var documents = new List<Document>();
            var docsPath = Path.Combine("samples", "docs");

            if (!Directory.Exists(docsPath))
            {
                Console.WriteLine($"Warning: Sample docs directory not found at {docsPath}");
                Console.WriteLine("Creating sample documents programmatically...\n");
                
                // Create sample documents in memory
                documents.Add(new Document
                {
                    Id = "doc1",
                    Title = "SmallMind Overview",
                    SourceUri = "internal://docs/overview",
                    Content = "SmallMind is a pure C# language model with no external dependencies. " +
                              "It uses a Transformer architecture with self-attention. " +
                              "The model supports training and inference on CPU. " +
                              "Key features include automatic differentiation and SIMD optimizations.",
                    Tags = new HashSet<string> { "overview", "architecture" }
                });

                documents.Add(new Document
                {
                    Id = "doc2",
                    Title = "Training Guide",
                    SourceUri = "internal://docs/training",
                    Content = "Training SmallMind involves loading data, tokenization, and running gradient descent. " +
                              "Important hyperparameters include learning rate, batch size, and number of layers. " +
                              "Use the Adam optimizer with gradient clipping. " +
                              "Monitor training loss to ensure convergence.",
                    Tags = new HashSet<string> { "training", "guide" }
                });

                documents.Add(new Document
                {
                    Id = "doc3",
                    Title = "RAG and Workflows",
                    SourceUri = "internal://docs/rag",
                    Content = "RAG combines retrieval with generation for knowledge-grounded responses. " +
                              "The workflow engine supports multi-step reasoning with budgets and validation. " +
                              "Chat sessions maintain conversation history across multiple turns. " +
                              "All components support deterministic execution for reproducibility.",
                    Tags = new HashSet<string> { "rag", "workflows", "chat" }
                });

                return documents;
            }

            // Load from files
            var files = Directory.GetFiles(docsPath, "*.txt");
            int docId = 1;

            foreach (var file in files)
            {
                var content = File.ReadAllText(file);
                var fileName = Path.GetFileNameWithoutExtension(file);

                var doc = new Document
                {
                    Id = $"doc{docId++}",
                    Title = FormatTitle(fileName),
                    SourceUri = $"file://{Path.GetFullPath(file)}",
                    Content = content,
                    Tags = new HashSet<string> { "documentation" }
                };

                documents.Add(doc);
            }

            return documents;
        }

        /// <summary>
        /// Format a filename as a title.
        /// </summary>
        private static string FormatTitle(string fileName)
        {
            // Convert underscores to spaces and title case
            var words = fileName.Replace('_', ' ').Split(' ');
            for (int i = 0; i < words.Length; i++)
            {
                if (words[i].Length > 0)
                {
                    words[i] = char.ToUpper(words[i][0]) + words[i].Substring(1);
                }
            }
            return string.Join(" ", words);
        }

        /// <summary>
        /// Demonstrate retrieval functionality.
        /// </summary>
        private static void DemonstrateRetrieval(InMemoryLexicalIndex index)
        {
            var queries = new[]
            {
                "How do I train the model?",
                "What is the architecture?",
                "Tell me about RAG"
            };

            var retrievalOptions = new RetrievalOptions
            {
                TopK = 3,
                Deterministic = true,
                IncludeSnippets = true,
                MaxSnippetChars = 200
            };

            foreach (var query in queries)
            {
                Console.WriteLine($"Query: {query}");
                var result = index.Search(query, retrievalOptions);

                if (result.Chunks.Count == 0)
                {
                    Console.WriteLine("  No results found.\n");
                    continue;
                }

                Console.WriteLine($"  Found {result.Chunks.Count} relevant chunks:");
                for (int i = 0; i < result.Chunks.Count; i++)
                {
                    var chunk = result.Chunks[i];
                    Console.WriteLine($"\n  [{i + 1}] Score: {chunk.Score:F4}");
                    Console.WriteLine($"      Source: {chunk.Citation.Title}");
                    Console.WriteLine($"      Text: {chunk.Text.Substring(0, Math.Min(100, chunk.Text.Length))}...");
                }
                Console.WriteLine();
            }
        }

        /// <summary>
        /// Demonstrate RAG prompt building.
        /// </summary>
        private static void DemonstrateRagPromptBuilding(InMemoryLexicalIndex index)
        {
            var query = "How do I train SmallMind?";
            Console.WriteLine($"Building RAG prompt for: \"{query}\"\n");

            // Retrieve relevant chunks
            var retrievalOptions = new RetrievalOptions
            {
                TopK = 3,
                Deterministic = true
            };

            var retrievalResult = index.Search(query, retrievalOptions);

            // Build prompt
            var promptOptions = new RagPromptOptions
            {
                MaxContextChars = 2000,
                MaxChunksToInclude = 3,
                IncludeSourcesSection = true
            };

            var prompt = RagPromptBuilder.Build(query, retrievalResult.Chunks, promptOptions);

            Console.WriteLine("Generated Prompt:");
            Console.WriteLine("================");
            Console.WriteLine(prompt);
            Console.WriteLine("================\n");
            Console.WriteLine($"Prompt length: {prompt.Length} characters");
            Console.WriteLine($"Chunks included: {retrievalResult.Chunks.Count}");
        }

        /// <summary>
        /// Demonstrate chat session structure.
        /// </summary>
        private static void DemonstrateChatSession(InMemorySessionStore sessionStore)
        {
            var sessionId = "demo-session-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            Console.WriteLine($"Creating chat session: {sessionId}\n");

            // Create a session
            var session = new ChatSession
            {
                SessionId = sessionId,
                CreatedAt = DateTime.UtcNow
            };

            // Simulate conversation turns
            var conversationExamples = new[]
            {
                ("What is SmallMind?", "SmallMind is a pure C# language model with Transformer architecture..."),
                ("How do I train it?", "To train SmallMind, prepare your text data and use the Training class..."),
                ("Can I use RAG with it?", "Yes! SmallMind supports RAG with the InMemoryLexicalIndex...")
            };

            foreach (var (userMsg, assistantMsg) in conversationExamples)
            {
                var turn = new ChatTurn
                {
                    UserMessage = userMsg,
                    AssistantMessage = assistantMsg,
                    Timestamp = DateTime.UtcNow,
                    Citations = new List<string> { "SmallMind Documentation" }
                };

                session.Turns.Add(turn);
                Console.WriteLine($"Turn {session.Turns.Count}:");
                Console.WriteLine($"  User: {userMsg}");
                Console.WriteLine($"  Assistant: {assistantMsg.Substring(0, Math.Min(60, assistantMsg.Length))}...");
                Console.WriteLine($"  Citations: {string.Join(", ", turn.Citations)}\n");
            }

            // Save to store
            sessionStore.UpsertAsync(session).Wait();
            Console.WriteLine($"✓ Session saved with {session.Turns.Count} turns");
            Console.WriteLine($"✓ Session store now contains {sessionStore.Count} session(s)");
        }
    }
}
