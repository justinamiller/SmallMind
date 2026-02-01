# SmallMind.Rag Dense and Hybrid Retrieval

This directory contains the dense retrieval and hybrid search implementation for SmallMind.Rag.

## Components

### Interfaces

- **IVectorStore** - Interface for vector storage and similarity search
- **IEmbedder** - Interface for text-to-vector embedding generation

### Implementations

#### Vector Storage
- **VectorStoreFlat** - Flat vector store with SIMD-optimized brute-force search
  - Uses `System.Numerics.Vector<float>` for fast dot product and L2 norm
  - Binary serialization for persistence
  - Cosine similarity scoring

#### Embeddings
- **FeatureHashingEmbedder** - Deterministic baseline embedder
  - Zero external dependencies
  - Uses hashing trick for feature extraction
  - Configurable dimension and seed for reproducibility
  - L2-normalized output vectors

#### Retrievers
- **DenseRetriever** - Semantic search using embeddings
  - Embeds query and chunks
  - Returns top-K by cosine similarity
  
- **HybridRetriever** - Combines BM25 + dense retrieval
  - Configurable sparse/dense weights
  - Score normalization and fusion
  - Union of candidate sets

## Usage Example

```csharp
using SmallMind.Rag;
using SmallMind.Rag.Retrieval;
using SmallMind.Rag.Indexing.Sparse;

// 1. Setup embedder and vector store
var embedder = new FeatureHashingEmbedder(dimension: 256, seed: 42);
var vectorStore = new VectorStoreFlat(dimension: 256);

// 2. Create dense retriever
var denseRetriever = new DenseRetriever(vectorStore, embedder);

// 3. Index chunks
var chunks = new Dictionary<string, Chunk>();
// ... populate chunks ...

denseRetriever.BuildIndex(chunks);

// 4. Search
var results = denseRetriever.Retrieve("query text", topK: 5, chunks);

// 5. Hybrid retrieval (optional - requires BM25)
var bm25Retriever = new Bm25Retriever(invertedIndex);
var hybridRetriever = new HybridRetriever(
    bm25: bm25Retriever,
    dense: denseRetriever,
    sparseWeight: 0.5f,  // BM25 weight
    denseWeight: 0.5f    // Dense weight
);

var hybridResults = hybridRetriever.Retrieve("query text", topK: 5, chunks);
```

## Performance Optimizations

### SIMD Vectorization
- Dot product and L2 norm use `System.Numerics.Vector<float>`
- Automatic SIMD width detection
- Scalar fallback for remainder elements

### Hot Path Optimizations
- `[MethodImpl(MethodImplOptions.AggressiveInlining)]` on critical methods
- Pre-allocated buffers where possible
- Minimal allocations in search path

### Memory Efficiency
- Binary serialization for compact storage
- No LINQ in hot paths
- Sparse data structures where appropriate

## Baseline Performance Notes

**FeatureHashingEmbedder** is a simple deterministic baseline:
- Suitable for development and testing
- Not state-of-the-art semantic quality
- Can be replaced with real embedding models (e.g., sentence transformers)
- Advantage: Zero dependencies, reproducible, fast

For production, consider implementing a custom `IEmbedder` backed by:
- Pre-trained sentence transformers (via ONNX)
- SmallMind language model embeddings
- External embedding APIs

## File Format

### Vector Store Binary Format
```
- Version (int32)
- Dimension (int32)
- Count (int32)
- For each vector:
  - ChunkId (string, length-prefixed)
  - Vector (dimension × float32)
```

## Future Enhancements

Potential improvements (not yet implemented):
- Approximate nearest neighbor (ANN) indices (HNSW, IVF)
- Quantization (PQ, scalar quantization)
- GPU acceleration
- Batch processing optimizations
- Cross-encoder re-ranking

---

**Note:** This is Phase 3 of the SmallMind.Rag MVP. Dense and hybrid retrieval enable semantic search beyond exact keyword matching.
