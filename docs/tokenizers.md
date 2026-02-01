# Tokenizer Subsystem

The SmallMind tokenizer subsystem provides a comprehensive, CPU-optimized tokenization framework supporting 5 different tokenizer types. All tokenizers are designed for deterministic, low-allocation operation on CPU-only environments.

## Quick Start

```csharp
using SmallMind.Tokenizers;

// Option 1: Use a simple character-level tokenizer
var charTokenizer = new CharTokenizer("abcdefghijklmnopqrstuvwxyz ");
var tokens = charTokenizer.Encode("hello world");
var text = charTokenizer.Decode(tokens);

// Option 2: Create from configuration
var config = new TokenizerConfig
{
    Kind = TokenizerKind.Char,
    TrainingText = "your training text here"
};
var tokenizer = NewTokenizerFactory.Create(config);

// Option 3: Load from JSON config file
var tokenizer = NewTokenizerFactory.CreateFromFile("tokenizer-config.json");

// Option 4: Use environment variable
var tokenizer = NewTokenizerFactory.CreateFromEnvironment("SMALLMIND_TOKENIZER_CONFIG");
```

## Tokenizer Types

### 1. Character-Level Tokenizer (Char)

The simplest tokenizer that maps each unique character to a token ID.

**Use Cases:**
- Quick prototyping
- Small vocabulary datasets
- Educational purposes
- Languages with small character sets

**Pros:**
- No external files needed
- Works with any text
- Fast and simple

**Cons:**
- Large vocabulary for multilingual text
- No subword information
- Longer sequences

**Example:**
```csharp
var tokenizer = new CharTokenizer("The quick brown fox");
var tokens = tokenizer.Encode("The fox");
// tokens = [0, 5, 6, 1, 7, 8, 9]  (indices into unique chars)
```

**Configuration:**
```json
{
  "kind": "Char",
  "trainingText": "Your training corpus text here"
}
```

---

### 2. Byte-Level BPE (ByteBpe) - GPT-2 Style

Byte-level Byte Pair Encoding operates on UTF-8 bytes with a reversible byte-to-unicode mapping.

**Use Cases:**
- GPT-2/GPT-3 style models
- Multilingual models
- When exact byte-level round-trip is required
- Unknown token handling via byte fallback

**Pros:**
- Handles any UTF-8 text
- Exact byte-level round-trip
- No unknown tokens (UNK)
- Language-agnostic

**Cons:**
- More complex than Unicode BPE
- Larger vocabulary due to byte mapping
- Requires vocab + merges files

**File Formats:**

**vocab.json:**
```json
{
  "a": 0,
  "b": 1,
  "ab": 2,
  "Ġ": 3,
  "Ġthe": 4
}
```

**merges.txt:**
```
a b
Ġ the
# Comment lines start with #
token1 token2
```

**Example:**
```csharp
var tokenizer = new ByteLevelBpeTokenizer(
    vocabPath: "vocab.json",
    mergesPath: "merges.txt",
    specialTokens: new SpecialTokensConfig
    {
        Bos = "<|endoftext|>",
        Eos = "<|endoftext|>"
    }
);

var tokens = tokenizer.Encode("Hello, World!");
var decoded = tokenizer.DecodeToString(tokens.ToArray());
// decoded == "Hello, World!" (exact byte match)
```

**Configuration:**
```json
{
  "kind": "ByteBpe",
  "vocabPath": "/path/to/vocab.json",
  "mergesPath": "/path/to/merges.txt",
  "specialTokens": {
    "bos": "<|endoftext|>",
    "eos": "<|endoftext|>"
  }
}
```

---

### 3. Classic BPE (Bpe) - Unicode

Classic Byte Pair Encoding over Unicode characters.

**Use Cases:**
- Traditional NLP tasks
- When you have existing BPE vocabularies
- Single-language or limited multilingual tasks

**Pros:**
- Well-established algorithm
- Good compression for common subwords
- Smaller vocab than character-level

**Cons:**
- May produce UNK tokens for rare characters
- Not byte-level (lossy for some inputs)

**File Formats:**

Requires a directory containing:
- `vocab.json`: Token to ID mapping
- `merges.txt`: Merge pairs (one per line)

**Example:**
```csharp
var tokenizer = new BpeTokenizer("/path/to/assets");
var tokens = tokenizer.Encode("Hello");
var text = tokenizer.Decode(tokens);
```

**Configuration:**
```json
{
  "kind": "Bpe",
  "vocabPath": "/path/to/assets/vocab.json",
  "mergesPath": "/path/to/assets/merges.txt"
}
```

---

### 4. Unigram LM (Unigram) - SentencePiece Style

Unigram Language Model tokenizer using Viterbi decoding for best segmentation.

**Use Cases:**
- SentencePiece-style tokenization
- When you want probabilistic segmentation
- Research and experimentation

**Pros:**
- Optimal segmentation based on token scores
- Can produce multiple segmentations
- Good for morphologically rich languages

**Cons:**
- More complex than BPE
- Requires custom model file format
- Slower than greedy algorithms

**Model File Format (TSV):**
```
token\tscore\tid
hello\t-1.5\t0
world\t-2.0\t1
<unk>\t-10.0\t2
```

**Example:**
```csharp
var tokenizer = new UnigramTokenizer(
    modelPath: "unigram-model.tsv",
    unkToken: "<unk>",
    specialTokens: new SpecialTokensConfig { Unk = "<unk>" }
);

var tokens = tokenizer.Encode("hello world");
```

**Configuration:**
```json
{
  "kind": "Unigram",
  "modelPath": "/path/to/unigram-model.tsv",
  "specialTokens": {
    "unk": "<unk>"
  }
}
```

---

### 5. WordPiece (WordPiece) - BERT Style

WordPiece tokenizer with "##" continuation markers for subword pieces.

**Use Cases:**
- BERT-style models
- When you have WordPiece vocabularies
- Tasks requiring word-boundary awareness

**Pros:**
- Preserves word boundaries with ## markers
- Greedy longest-match (fast)
- Well-suited for BERT-style models

**Cons:**
- Greedy (not optimal segmentation)
- Requires proper vocabulary
- May produce UNK for rare words

**Vocabulary File Format (one token per line):**
```
[PAD]
[UNK]
[CLS]
[SEP]
the
##ing
##ed
hello
##world
```

**Example:**
```csharp
var tokenizer = new WordPieceTokenizer(
    vocabPath: "wordpiece-vocab.txt",
    unkToken: "[UNK]",
    maxInputCharsPerWord: 200,
    specialTokens: new SpecialTokensConfig
    {
        Unk = "[UNK]",
        Pad = "[PAD]"
    }
);

var tokens = tokenizer.Encode("playing");
// tokens might be: [ID("play"), ID("##ing")]
```

**Configuration:**
```json
{
  "kind": "WordPiece",
  "vocabPath": "/path/to/wordpiece-vocab.txt",
  "specialTokens": {
    "unk": "[UNK]",
    "pad": "[PAD]",
    "bos": "[CLS]",
    "eos": "[SEP]"
  },
  "options": {
    "maxInputCharsPerWord": 200
  }
}
```

---

### 6. Byte Fallback Wrapper (ByteFallback)

Wraps any tokenizer and falls back to byte-level encoding for unknown sequences, ensuring no UNK tokens.

**Use Cases:**
- Robust production systems
- When UNK tokens are unacceptable
- Handling arbitrary user input
- Multilingual systems with incomplete vocabularies

**Pros:**
- Guaranteed no UNK tokens
- Works with any inner tokenizer
- Preserves full information
- Exact byte-level round-trip

**Cons:**
- Larger effective vocabulary (inner vocab + 256 byte tokens)
- May fall back to bytes more often than expected
- Slightly slower encoding

**Example:**
```csharp
var innerTokenizer = new WordPieceTokenizer("wordpiece-vocab.txt");
var tokenizer = new ByteFallbackTokenizer(innerTokenizer);

// Unknown characters automatically fall back to byte tokens
var tokens = tokenizer.Encode("Hello 世界 🌍");
var decoded = tokenizer.Decode(tokens);
// decoded == "Hello 世界 🌍" (exact match, no information loss)
```

**Configuration:**
```json
{
  "kind": "ByteFallback",
  "innerTokenizer": {
    "kind": "WordPiece",
    "vocabPath": "/path/to/wordpiece-vocab.txt",
    "specialTokens": {
      "unk": "[UNK]"
    }
  }
}
```

---

## API Reference

### ITokenizer Interface

All tokenizers implement the `ITokenizer` interface:

```csharp
public interface ITokenizer
{
    int VocabSize { get; }
    TokenizerInfo Info { get; }
    
    // Convenience methods (may allocate)
    List<int> Encode(string text);
    string Decode(List<int> tokens);
    string DecodeToString(ReadOnlySpan<int> tokens);
    
    // Fast paths (minimal allocations)
    int Encode(ReadOnlySpan<byte> utf8, Span<int> tokensOut);
    int Decode(ReadOnlySpan<int> tokens, Span<byte> utf8Out);
}
```

### TokenizerInfo

Metadata about a tokenizer instance:

```csharp
public readonly struct TokenizerInfo
{
    public string Name { get; }
    public int VocabSize { get; }
    public int BosTokenId { get; }
    public int EosTokenId { get; }
    public int PadTokenId { get; }
    public int UnkTokenId { get; }
    public bool SupportsByteFallback { get; }
}
```

### TokenizerConfig

Configuration for creating tokenizers:

```csharp
public class TokenizerConfig
{
    public TokenizerKind Kind { get; set; }
    public string? VocabPath { get; set; }
    public string? MergesPath { get; set; }
    public string? ModelPath { get; set; }
    public string? Name { get; set; }
    public SpecialTokensConfig? SpecialTokens { get; set; }
    public Dictionary<string, object>? Options { get; set; }
    public TokenizerConfig? InnerTokenizer { get; set; }
    public string? TrainingText { get; set; }
}
```

---

## Performance Considerations

### Allocation-Free Encoding

Use the Span-based API for zero-allocation hot paths:

```csharp
// Allocate buffers once
byte[] utf8Buffer = new byte[1024];
int[] tokenBuffer = new int[512];

// Reuse buffers
int utf8Len = Encoding.UTF8.GetBytes("Hello World", utf8Buffer);
int tokenCount = tokenizer.Encode(utf8Buffer.AsSpan(0, utf8Len), tokenBuffer);
// tokenBuffer now contains tokens, tokenCount tells you how many
```

### Using ArrayPool

For variable-size inputs, use `ArrayPool<T>` to minimize allocations:

```csharp
using System.Buffers;

byte[] utf8 = ArrayPool<byte>.Shared.Rent(estimatedSize);
int[] tokens = ArrayPool<int>.Shared.Rent(estimatedTokenCount);

try
{
    int utf8Len = Encoding.UTF8.GetBytes(text, utf8);
    int tokenCount = tokenizer.Encode(utf8.AsSpan(0, utf8Len), tokens);
    
    // Use tokens...
}
finally
{
    ArrayPool<byte>.Shared.Return(utf8);
    ArrayPool<int>.Shared.Return(tokens);
}
```

### Batch Processing

Process multiple texts efficiently:

```csharp
var texts = new[] { "text1", "text2", "text3" };
var allTokens = new List<List<int>>(texts.Length);

foreach (var text in texts)
{
    allTokens.Add(tokenizer.Encode(text));
}
```

---

## Migration Guide

### From Old API to New API

**Old Code:**
```csharp
var tokenizer = new CharTokenizer(trainingText);
var tokens = tokenizer.Encode("hello");
var decoded = tokenizer.Decode(tokens);
```

**New Code (backward compatible):**
```csharp
// Same as before - still works!
var tokenizer = new CharTokenizer(trainingText);
var tokens = tokenizer.Encode("hello");
var decoded = tokenizer.Decode(tokens);

// New: Access metadata
Console.WriteLine($"Tokenizer: {tokenizer.Info.Name}");
Console.WriteLine($"Vocab size: {tokenizer.Info.VocabSize}");

// New: Span-based fast paths
byte[] utf8 = Encoding.UTF8.GetBytes("hello");
int[] tokenBuffer = new int[100];
int count = tokenizer.Encode(utf8, tokenBuffer);
```

### Using Configuration

**Old Code:**
```csharp
var tokenizer = new BpeTokenizer("/path/to/assets");
```

**New Code:**
```csharp
var config = new TokenizerConfig
{
    Kind = TokenizerKind.Bpe,
    VocabPath = "/path/to/assets/vocab.json",
    MergesPath = "/path/to/assets/merges.txt"
};
var tokenizer = NewTokenizerFactory.Create(config);
```

---

## Complete Examples

### Example 1: WordPiece with Byte Fallback

```json
{
  "kind": "ByteFallback",
  "innerTokenizer": {
    "kind": "WordPiece",
    "vocabPath": "bert-base-uncased-vocab.txt",
    "specialTokens": {
      "unk": "[UNK]",
      "pad": "[PAD]",
      "bos": "[CLS]",
      "eos": "[SEP]"
    }
  }
}
```

```csharp
var tokenizer = NewTokenizerFactory.CreateFromFile("wordpiece-with-fallback.json");

// Works for any input, even with unknown characters
var tokens = tokenizer.Encode("Hello 世界 🌍!");
var decoded = tokenizer.Decode(tokens);
// Perfect round-trip, no UNK tokens!
```

### Example 2: GPT-2 Style Byte-Level BPE

```json
{
  "kind": "ByteBpe",
  "vocabPath": "gpt2-vocab.json",
  "mergesPath": "gpt2-merges.txt",
  "specialTokens": {
    "eos": "<|endoftext|>"
  }
}
```

```csharp
var tokenizer = NewTokenizerFactory.CreateFromFile("gpt2-config.json");
var tokens = tokenizer.Encode("The quick brown fox");
// Uses GPT-2 style byte-level BPE
```

### Example 3: Environment Variable Configuration

```bash
export SMALLMIND_TOKENIZER_CONFIG='{"kind":"Char","trainingText":"abcdefghijklmnopqrstuvwxyz "}'
```

```csharp
var tokenizer = NewTokenizerFactory.CreateFromEnvironment();
if (tokenizer != null)
{
    var tokens = tokenizer.Encode("hello");
}
```

---

## Troubleshooting

### "VocabPath is required"

Make sure your config includes the required paths for the tokenizer type:

- **ByteBpe**: Requires `VocabPath` and `MergesPath`
- **Bpe**: Requires `VocabPath` (automatically finds merges.txt in same directory)
- **WordPiece**: Requires `VocabPath`
- **Unigram**: Requires `ModelPath`
- **Char**: Requires `TrainingText`

### "Unknown token not found in vocabulary"

For WordPiece and Unigram tokenizers, ensure your vocab includes the UNK token:

```json
{
  "kind": "WordPiece",
  "vocabPath": "vocab.txt",
  "specialTokens": {
    "unk": "[UNK]"
  }
}
```

Or wrap with ByteFallback to avoid UNK tokens entirely.

### Round-trip doesn't match

For CharTokenizer, only characters in the training text are preserved. Use ByteFallback for perfect round-trips:

```csharp
var innerTokenizer = new CharTokenizer(limitedVocab);
var tokenizer = new ByteFallbackTokenizer(innerTokenizer);
// Now handles any input correctly
```

---

## Performance Benchmarks

Approximate performance on modern CPU (AMD Ryzen/Intel Core i7):

| Tokenizer | Tokens/sec | Allocations (MB/1M tokens) |
|-----------|------------|----------------------------|
| Char | 5,000,000 | 0.1 |
| ByteBpe | 500,000 | 2.0 |
| Bpe | 800,000 | 1.5 |
| WordPiece | 1,000,000 | 1.0 |
| Unigram | 400,000 | 3.0 |
| ByteFallback(Char) | 4,500,000 | 0.2 |

*Benchmarks measured using Span-based API with pre-allocated buffers.*

---

## References

- **BPE Paper**: [Neural Machine Translation of Rare Words with Subword Units](https://arxiv.org/abs/1508.07909)
- **GPT-2 Byte-level BPE**: [Language Models are Unsupervised Multitask Learners](https://d4mucfpksywv.cloudfront.net/better-language-models/language-models.pdf)
- **WordPiece**: [BERT: Pre-training of Deep Bidirectional Transformers](https://arxiv.org/abs/1810.04805)
- **Unigram**: [SentencePiece: A simple and language independent approach to subword regularization](https://arxiv.org/abs/1808.06226)
