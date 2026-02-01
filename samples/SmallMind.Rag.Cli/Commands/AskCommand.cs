using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SmallMind.Rag.Indexing;
using SmallMind.Rag.Indexing.Sparse;

namespace SmallMind.Rag.Cli.Commands;

/// <summary>
/// Command for asking questions using indexed documents.
/// </summary>
internal static class AskCommand
{
    /// <summary>
    /// Executes the ask command.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    public static void Execute(string[] args)
    {
        try
        {
            var options = ParseArguments(args);

            Console.WriteLine($"Question: {options.Question}");
            Console.WriteLine();

            // Load index
            if (!Directory.Exists(options.IndexPath))
            {
                Console.Error.WriteLine($"Error: Index directory not found: {options.IndexPath}");
                Console.Error.WriteLine("Please run 'smrag ingest' first to create an index.");
                Environment.Exit(1);
                return;
            }

            var (invertedIndex, chunkStore, manifest) = IndexSerializer.LoadIndex(options.IndexPath);

            if (chunkStore.Count == 0)
            {
                Console.Error.WriteLine($"Error: Index is empty. Please run 'smrag ingest' first.");
                Environment.Exit(1);
                return;
            }

            // Create retriever
            var retriever = new Bm25Retriever(invertedIndex);

            // Retrieve chunks
            var results = retriever.Retrieve(options.Question, options.TopK, chunkStore);

            if (results == null || results.Count == 0)
            {
                Console.WriteLine("Insufficient evidence in the index to answer this question.");
                return;
            }

            Console.WriteLine($"Retrieved {results.Count} chunks");
            Console.WriteLine();

            // Display context
            Console.WriteLine("Context:");
            Console.WriteLine(new string('=', 80));
            Console.WriteLine();

            for (int i = 0; i < results.Count; i++)
            {
                var result = results[i];
                
                // Get the full chunk from the store
                if (!chunkStore.TryGetValue(result.ChunkId, out var chunk))
                {
                    Console.WriteLine($"[S{i + 1}] Warning: Chunk not found in store");
                    continue;
                }

                Console.WriteLine($"[S{i + 1}] {chunk.Title}");
                Console.WriteLine($"     Source: {chunk.SourceUri}");
                Console.WriteLine($"     Range: chars {chunk.CharStart}-{chunk.CharEnd}");
                Console.WriteLine($"     Score: {result.Score:F4}");
                Console.WriteLine();

                // Display excerpt (first 300 chars or full content if shorter)
                string excerpt = chunk.Text.Length > 300
                    ? chunk.Text.Substring(0, 300) + "..."
                    : chunk.Text;

                Console.WriteLine(excerpt);
                Console.WriteLine();
                Console.WriteLine(new string('-', 80));
                Console.WriteLine();
            }

            // Display answer placeholder
            Console.WriteLine("Answer:");
            Console.WriteLine(new string('=', 80));
            Console.WriteLine();
            Console.WriteLine("(No LLM integrated yet - context provided above)");
            Console.WriteLine();
            Console.WriteLine("To generate an answer, you can:");
            Console.WriteLine("  1. Use the retrieved context above as input to an LLM");
            Console.WriteLine("  2. Integrate SmallMind.Core for local generation");
            Console.WriteLine("  3. Use an external API (OpenAI, Anthropic, etc.)");
            Console.WriteLine();

            // Display citations
            Console.WriteLine("Citations:");
            Console.WriteLine(new string('=', 80));
            Console.WriteLine();

            for (int i = 0; i < results.Count; i++)
            {
                var result = results[i];
                
                if (!chunkStore.TryGetValue(result.ChunkId, out var chunk))
                    continue;

                Console.WriteLine($"[S{i + 1}] {chunk.Title}");
                Console.WriteLine($"      {chunk.SourceUri}");
                Console.WriteLine($"      Characters {chunk.CharStart}-{chunk.CharEnd}");
                if (chunk.Tags.Length > 0)
                {
                    Console.WriteLine($"      Tags: {string.Join(", ", chunk.Tags)}");
                }
                Console.WriteLine($"      Created: {chunk.CreatedUtc:yyyy-MM-dd HH:mm:ss} UTC");
                Console.WriteLine();
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Parses command-line arguments for the ask command.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>Parsed options.</returns>
    private static AskOptions ParseArguments(string[] args)
    {
        var options = new AskOptions
        {
            TopK = 5,
            MaxContextTokens = 2000,
            Deterministic = false
        };

        for (int i = 1; i < args.Length; i++)
        {
            string arg = args[i].ToLowerInvariant();

            switch (arg)
            {
                case "--index":
                    if (i + 1 >= args.Length)
                    {
                        throw new ArgumentException("Missing value for --index");
                    }
                    options.IndexPath = args[++i];
                    break;

                case "--question":
                    if (i + 1 >= args.Length)
                    {
                        throw new ArgumentException("Missing value for --question");
                    }
                    options.Question = args[++i];
                    break;

                case "--topk":
                    if (i + 1 >= args.Length)
                    {
                        throw new ArgumentException("Missing value for --topk");
                    }
                    if (!int.TryParse(args[++i], out int topK) || topK <= 0)
                    {
                        throw new ArgumentException("--topk must be a positive integer");
                    }
                    options.TopK = topK;
                    break;

                case "--maxcontexttokens":
                    if (i + 1 >= args.Length)
                    {
                        throw new ArgumentException("Missing value for --maxContextTokens");
                    }
                    if (!int.TryParse(args[++i], out int maxTokens) || maxTokens <= 0)
                    {
                        throw new ArgumentException("--maxContextTokens must be a positive integer");
                    }
                    options.MaxContextTokens = maxTokens;
                    break;

                case "--deterministic":
                    options.Deterministic = true;
                    break;

                default:
                    throw new ArgumentException($"Unknown argument: {args[i]}");
            }
        }

        // Validate required arguments
        if (string.IsNullOrEmpty(options.IndexPath))
        {
            throw new ArgumentException("Missing required argument: --index");
        }

        if (string.IsNullOrEmpty(options.Question))
        {
            throw new ArgumentException("Missing required argument: --question");
        }

        return options;
    }

    /// <summary>
    /// Options for the ask command.
    /// </summary>
    private class AskOptions
    {
        public string IndexPath { get; set; } = string.Empty;
        public string Question { get; set; } = string.Empty;
        public int TopK { get; set; }
        public int MaxContextTokens { get; set; }
        public bool Deterministic { get; set; }
    }
}
