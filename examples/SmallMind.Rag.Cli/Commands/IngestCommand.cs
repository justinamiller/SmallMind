using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using SmallMind.Rag;
using SmallMind.Rag.Indexing;
using SmallMind.Rag.Ingestion;

namespace SmallMind.Rag.Cli.Commands;

/// <summary>
/// Command for ingesting documents from a directory into a searchable index.
/// </summary>
internal static class IngestCommand
{
    /// <summary>
    /// Executes the ingest command.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    public static void Execute(string[] args)
    {
        try
        {
            var options = ParseArguments(args);

            Console.WriteLine($"Ingesting documents from {options.Path}...");
            Console.WriteLine();

            var stopwatch = Stopwatch.StartNew();

            // Create document ingestor
            var ingestor = new DocumentIngestor();
            var documents = ingestor.IngestDirectory(options.Path, string.Join(";", options.IncludePatterns));

            Console.WriteLine($"Found {documents.Count} documents");

            if (documents.Count == 0)
            {
                Console.WriteLine("No documents found. Nothing to ingest.");
                return;
            }

            // Read document contents
            var docContents = new Dictionary<string, string>();
            foreach (var doc in documents)
            {
                try
                {
                    string content = File.ReadAllText(doc.SourceUri);
                    docContents[doc.DocId] = content;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[WARNING] Failed to read {doc.SourceUri}: {ex.Message}");
                }
            }

            // Create RAG options
            var ragOptions = new RagOptions
            {
                IndexDirectory = options.IndexPath
            };

            // Create chunker and indexer
            var chunker = new Chunker();
            var indexer = new IncrementalIndexer(options.IndexPath, ingestor, chunker, ragOptions);

            if (options.Rebuild)
            {
                Console.WriteLine("Rebuilding index from scratch...");
                indexer.RebuildIndex(documents, docContents);
            }
            else
            {
                Console.WriteLine("Updating index incrementally...");
                indexer.UpdateIndex(documents, docContents);
            }

            stopwatch.Stop();

            Console.WriteLine($"Index saved to {options.IndexPath}");
            Console.WriteLine($"Time taken: {stopwatch.Elapsed.TotalSeconds:F2} seconds");
            Console.WriteLine();
            Console.WriteLine("Ingestion completed successfully.");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Parses command-line arguments for the ingest command.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>Parsed options.</returns>
    private static IngestOptions ParseArguments(string[] args)
    {
        var options = new IngestOptions
        {
            IncludePatterns = new[] { "*.txt", "*.md", "*.json", "*.log" }
        };

        for (int i = 1; i < args.Length; i++)
        {
            string arg = args[i].ToLowerInvariant();

            switch (arg)
            {
                case "--path":
                    if (i + 1 >= args.Length)
                    {
                        throw new ArgumentException("Missing value for --path");
                    }
                    options.Path = args[++i];
                    break;

                case "--index":
                    if (i + 1 >= args.Length)
                    {
                        throw new ArgumentException("Missing value for --index");
                    }
                    options.IndexPath = args[++i];
                    break;

                case "--include":
                    if (i + 1 >= args.Length)
                    {
                        throw new ArgumentException("Missing value for --include");
                    }
                    options.IncludePatterns = args[++i].Split(';', StringSplitOptions.RemoveEmptyEntries);
                    break;

                case "--rebuild":
                    options.Rebuild = true;
                    break;

                default:
                    throw new ArgumentException($"Unknown argument: {args[i]}");
            }
        }

        // Validate required arguments
        if (string.IsNullOrEmpty(options.Path))
        {
            throw new ArgumentException("Missing required argument: --path");
        }

        if (string.IsNullOrEmpty(options.IndexPath))
        {
            throw new ArgumentException("Missing required argument: --index");
        }

        if (!Directory.Exists(options.Path))
        {
            throw new ArgumentException($"Directory not found: {options.Path}");
        }

        return options;
    }

    /// <summary>
    /// Options for the ingest command.
    /// </summary>
    private class IngestOptions
    {
        public string Path { get; set; } = string.Empty;
        public string IndexPath { get; set; } = string.Empty;
        public string[] IncludePatterns { get; set; } = Array.Empty<string>();
        public bool Rebuild { get; set; }
    }
}
