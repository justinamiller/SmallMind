# SmallMind RAG CLI (`smrag`)

A command-line tool for document ingestion and question answering using the SmallMind RAG system.

## Features

- **Document Ingestion**: Scan directories and build searchable indexes
- **Incremental Updates**: Add new documents without rebuilding the entire index
- **Question Answering**: Retrieve relevant document chunks for questions
- **BM25 Retrieval**: Fast sparse retrieval using BM25 algorithm
- **Zero Dependencies**: No external command-line parsing libraries

## Building

```bash
dotnet build SmallMind.Rag.Cli.csproj
```

The output executable will be named `smrag`.

## Usage

### Ingest Documents

Build an index from documents in a directory:

```bash
smrag ingest --path ./docs --index ./my-index
```

**Options:**

- `--path <directory>` (required): Path to directory containing documents
- `--index <directory>` (required): Path to store/update the index
- `--include <patterns>` (optional): Semicolon-separated file patterns (default: `*.txt;*.md;*.json;*.log`)
- `--rebuild` (optional): Rebuild index from scratch instead of incremental update

**Examples:**

```bash
# Ingest all default file types
smrag ingest --path ./docs --index ./my-index

# Ingest only specific file types
smrag ingest --path ./docs --index ./my-index --include "*.txt;*.md"

# Rebuild entire index from scratch
smrag ingest --path ./docs --index ./my-index --rebuild
```

### Ask Questions

Ask a question using the indexed documents:

```bash
smrag ask --index ./my-index --question "What is SmallMind?"
```

**Options:**

- `--index <directory>` (required): Path to the index directory
- `--question "<text>"` (required): Question to ask
- `--topk <n>` (optional): Number of chunks to retrieve (default: 5)
- `--maxContextTokens <n>` (optional): Maximum context tokens (default: 2000)
- `--deterministic` (optional): Use deterministic retrieval

**Examples:**

```bash
# Ask a basic question
smrag ask --index ./my-index --question "What is SmallMind?"

# Retrieve more chunks for better context
smrag ask --index ./my-index --question "How does chunking work?" --topk 10

# Use deterministic retrieval
smrag ask --index ./my-index --question "Explain RAG" --deterministic
```

## Output

The `ask` command outputs:

1. **Question**: The query that was asked
2. **Retrieved Chunks**: Number of relevant chunks found
3. **Context**: Full text of retrieved chunks with metadata
4. **Answer**: Placeholder (LLM integration coming soon)
5. **Citations**: Source information for all retrieved chunks

## Architecture

The CLI is built with three main components:

- **Program.cs**: Main entry point and command routing
- **IngestCommand.cs**: Document ingestion and indexing
- **AskCommand.cs**: Question answering and retrieval

All argument parsing is done manually without external libraries, keeping the tool lightweight and dependency-free.

## Exit Codes

- `0`: Success
- `1`: Error (missing arguments, index not found, etc.)

## Performance

The CLI uses the SmallMind.Rag library which is optimized for:

- Memory efficiency with ArrayPool and Span<T>
- Fast BM25 scoring
- Incremental indexing to avoid full rebuilds
- Efficient chunking with markdown awareness

## Example Workflow

```bash
# 1. Ingest some documents
smrag ingest --path ./my-docs --index ./my-index

# 2. Ask questions
smrag ask --index ./my-index --question "What is the main topic?"

# 3. Add more documents incrementally
smrag ingest --path ./more-docs --index ./my-index

# 4. Ask another question with more context
smrag ask --index ./my-index --question "Tell me more" --topk 10
```

## Integration

The CLI can be integrated with LLM providers by:

1. Using the retrieved context as input to an LLM API
2. Integrating SmallMind.Core for local generation
3. Piping the context to other tools

Future versions may include built-in LLM integration.
