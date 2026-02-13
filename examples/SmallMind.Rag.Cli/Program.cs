using System;
using SmallMind.Rag.Cli.Commands;

namespace SmallMind.Rag.Cli;

/// <summary>
/// Main entry point for the SmallMind RAG command-line tool.
/// </summary>
internal class Program
{
    /// <summary>
    /// Main entry point for the CLI application.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            return;
        }

        string command = args[0].ToLowerInvariant();

        switch (command)
        {
            case "ingest":
                IngestCommand.Execute(args);
                break;
            case "ask":
                AskCommand.Execute(args);
                break;
            case "help":
            case "--help":
            case "-h":
                PrintUsage();
                break;
            default:
                Console.Error.WriteLine($"Error: Unknown command '{args[0]}'");
                Console.Error.WriteLine();
                PrintUsage();
                Environment.Exit(1);
                break;
        }
    }

    /// <summary>
    /// Prints usage information for the CLI tool.
    /// </summary>
    static void PrintUsage()
    {
        Console.WriteLine("SmallMind RAG - Document Ingestion and Question Answering");
        Console.WriteLine();
        Console.WriteLine("Usage: smrag <command> [options]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine();
        Console.WriteLine("  ingest    Ingest documents from a directory into an index");
        Console.WriteLine("  ask       Ask a question using the indexed documents");
        Console.WriteLine("  help      Show this help message");
        Console.WriteLine();
        Console.WriteLine("Ingest Command:");
        Console.WriteLine("  smrag ingest --path <directory> --index <directory> [options]");
        Console.WriteLine();
        Console.WriteLine("  Required:");
        Console.WriteLine("    --path <directory>       Path to directory containing documents");
        Console.WriteLine("    --index <directory>      Path to store/update the index");
        Console.WriteLine();
        Console.WriteLine("  Optional:");
        Console.WriteLine("    --include <patterns>     Semicolon-separated file patterns (default: *.txt;*.md;*.json;*.log)");
        Console.WriteLine("    --rebuild                Rebuild index from scratch (default: incremental update)");
        Console.WriteLine();
        Console.WriteLine("  Example:");
        Console.WriteLine("    smrag ingest --path ./docs --index ./my-index --include \"*.txt;*.md\"");
        Console.WriteLine("    smrag ingest --path ./docs --index ./my-index --rebuild");
        Console.WriteLine();
        Console.WriteLine("Ask Command:");
        Console.WriteLine("  smrag ask --index <directory> --question \"<text>\" [options]");
        Console.WriteLine();
        Console.WriteLine("  Required:");
        Console.WriteLine("    --index <directory>      Path to the index directory");
        Console.WriteLine("    --question \"<text>\"      Question to ask");
        Console.WriteLine();
        Console.WriteLine("  Optional:");
        Console.WriteLine("    --topk <n>               Number of chunks to retrieve (default: 5)");
        Console.WriteLine("    --maxContextTokens <n>   Maximum context tokens (default: 2000)");
        Console.WriteLine("    --deterministic          Use deterministic retrieval");
        Console.WriteLine();
        Console.WriteLine("  Example:");
        Console.WriteLine("    smrag ask --index ./my-index --question \"What is SmallMind?\"");
        Console.WriteLine("    smrag ask --index ./my-index --question \"How does chunking work?\" --topk 10");
    }
}
