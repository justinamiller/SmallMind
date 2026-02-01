using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using SmallMind.Rag.Indexing;
using SmallMind.Rag.Indexing.Sparse;
using SmallMind.Rag.Ingestion;
using SmallMind.Rag.Retrieval;
using SmallMind.Rag.Security;
using SmallMind.Rag.Telemetry;
using SmallMind.Rag.Prompting;

namespace SmallMind.Rag.Pipeline
{
    /// <summary>
    /// Main RAG pipeline that orchestrates ingestion, retrieval, prompting, security, and telemetry.
    /// Thread-safe and supports both sparse (BM25) and dense (vector) retrieval.
    /// </summary>
    public sealed class RagPipeline
    {
        private IncrementalIndexer? _indexer;
        private InvertedIndex _invertedIndex;
        private Dictionary<string, Chunk> _chunkStore;
        private Bm25Retriever _bm25Retriever;
        private DenseRetriever? _denseRetriever;
        private HybridRetriever? _hybridRetriever;
        private readonly IAuthorizer _authorizer;
        private readonly IRagLogger _logger;
        private readonly IRagMetrics _metrics;
        private readonly RagOptions _options;
        private readonly string _indexDirectory;
        private bool _isInitialized;
        private readonly object _initLock = new object();

        /// <summary>
        /// Creates a new RAG pipeline with the specified configuration and optional components.
        /// </summary>
        /// <param name="options">Configuration options for the RAG pipeline.</param>
        /// <param name="authorizer">Optional custom authorizer. Uses DefaultAuthorizer if null.</param>
        /// <param name="logger">Optional custom logger. Uses ConsoleRagLogger if null.</param>
        /// <param name="metrics">Optional custom metrics collector. Uses InMemoryRagMetrics if null.</param>
        public RagPipeline(
            RagOptions options,
            IAuthorizer? authorizer = null,
            IRagLogger? logger = null,
            IRagMetrics? metrics = null)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _indexDirectory = options.IndexDirectory ?? throw new ArgumentException("IndexDirectory must be set", nameof(options));
            
            _authorizer = authorizer ?? new DefaultAuthorizer();
            _logger = logger ?? new ConsoleRagLogger();
            _metrics = metrics ?? new InMemoryRagMetrics();

            _chunkStore = new Dictionary<string, Chunk>();
            _invertedIndex = new InvertedIndex();
            _bm25Retriever = new Bm25Retriever(_invertedIndex);
        }

        /// <summary>
        /// Initializes the pipeline by loading an existing index if present, or creating a new empty index.
        /// This method is thread-safe and idempotent.
        /// </summary>
        public void Initialize()
        {
            lock (_initLock)
            {
                if (_isInitialized)
                    return;

                RagContext.TraceId ??= RagContext.GenerateTraceId();
                string traceId = RagContext.TraceId;

                try
                {
                    _logger.LogInfo(traceId, $"Initializing RAG pipeline with index directory: {_indexDirectory}");

                    if (!Directory.Exists(_indexDirectory))
                    {
                        Directory.CreateDirectory(_indexDirectory);
                        _logger.LogInfo(traceId, $"Created index directory: {_indexDirectory}");
                    }

                    string indexPath = Path.Combine(_indexDirectory, "index.json");
                    if (File.Exists(indexPath))
                    {
                        _logger.LogInfo(traceId, $"Loading existing index from: {indexPath}");
                        var (index, chunks, _) = IndexSerializer.LoadIndex(_indexDirectory);
                        _invertedIndex = index;
                        _chunkStore = chunks;
                        _bm25Retriever = new Bm25Retriever(_invertedIndex);
                        _logger.LogInfo(traceId, $"Loaded {_chunkStore.Count} chunks from index");
                    }
                    else
                    {
                        _logger.LogInfo(traceId, $"No existing index found, starting with empty index");
                    }

                    // Create indexer for ingestion operations
                    var ingestor = new DocumentIngestor();
                    var chunker = new Chunker();
                    _indexer = new IncrementalIndexer(_indexDirectory, ingestor, chunker, _options);

                    _isInitialized = true;
                    _logger.LogInfo(traceId, $"Pipeline initialized successfully");
                }
                catch (Exception ex)
                {
                    _logger.LogError(traceId, "Initialize", ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// Ingests documents from the specified directory path into the index.
        /// </summary>
        /// <param name="path">Directory path containing documents to ingest.</param>
        /// <param name="rebuild">If true, rebuilds the entire index. If false, incrementally updates the index.</param>
        /// <param name="includePatterns">Semicolon-separated file patterns to include (e.g., "*.txt;*.md").</param>
        public void IngestDocuments(string path, bool rebuild = false, string includePatterns = "*.txt;*.md;*.json;*.log")
        {
            if (!_isInitialized)
                throw new InvalidOperationException("Pipeline must be initialized before ingesting documents. Call Initialize() first.");

            if (_indexer == null)
                throw new InvalidOperationException("Indexer not initialized. Call Initialize() first.");

            RagContext.TraceId ??= RagContext.GenerateTraceId();
            string traceId = RagContext.TraceId;

            Stopwatch sw = Stopwatch.StartNew();

            try
            {
                _logger.LogInfo(traceId, $"Starting document ingestion from: {path} (rebuild={rebuild})");

                if (!Directory.Exists(path))
                {
                    string error = $"Path does not exist: {path}";
                    _logger.LogError(traceId, "IngestDocuments", new DirectoryNotFoundException(error));
                    throw new DirectoryNotFoundException(error);
                }

                // Create document ingestor
                var ingestor = new DocumentIngestor();

                // Scan directory for documents
                _logger.LogInfo(traceId, $"Scanning directory with patterns: {includePatterns}");
                List<DocumentRecord> docRecords = ingestor.IngestDirectory(path, includePatterns);
                _logger.LogInfo(traceId, $"Found {docRecords.Count} documents");

                // Read file contents into dictionary
                var docContents = new Dictionary<string, string>();
                int successCount = 0;
                int errorCount = 0;

                foreach (DocumentRecord doc in docRecords)
                {
                    try
                    {
                        string content = File.ReadAllText(doc.SourceUri, System.Text.Encoding.UTF8);
                        docContents[doc.DocId] = content;
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(traceId, "IngestDocuments", ex);
                        errorCount++;
                    }
                }

                _logger.LogInfo(traceId, $"Successfully read {successCount} documents ({errorCount} errors)");

                // Update or rebuild index
                if (rebuild)
                {
                    _logger.LogInfo(traceId, $"Rebuilding index from scratch");
                    _indexer.RebuildIndex(docRecords, docContents);
                }
                else
                {
                    _logger.LogInfo(traceId, $"Incrementally updating index");
                    _indexer.UpdateIndex(docRecords, docContents);
                }

                // Reload index into memory after indexer updates it
                var (index, chunks, _) = IndexSerializer.LoadIndex(_indexDirectory);
                _invertedIndex = index;
                _chunkStore = chunks;
                _bm25Retriever = new Bm25Retriever(_invertedIndex);

                _logger.LogInfo(traceId, $"Index now contains {_chunkStore.Count} chunks");

                sw.Stop();

                // Log metrics
                _metrics.RecordIngestionDuration(sw.Elapsed);
                _metrics.RecordDocumentCount(docRecords.Count);
                _metrics.RecordChunkCount(_chunkStore.Count);

                _logger.LogInfo(traceId, $"Ingestion completed in {sw.ElapsedMilliseconds}ms");
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError(traceId, "IngestDocuments", ex);
                throw;
            }
        }

        /// <summary>
        /// Retrieves relevant chunks for the given query using the configured retrieval method.
        /// </summary>
        /// <param name="query">The search query.</param>
        /// <param name="userContext">Optional user context for authorization filtering.</param>
        /// <param name="topK">Optional override for number of results to return. Uses RagOptions.TopK if null.</param>
        /// <returns>List of retrieved chunks with scores and ranks.</returns>
        public List<RetrievedChunk> Retrieve(string query, UserContext? userContext = null, int? topK = null)
        {
            if (!_isInitialized)
                throw new InvalidOperationException("Pipeline must be initialized before retrieving. Call Initialize() first.");

            RagContext.TraceId ??= RagContext.GenerateTraceId();
            string traceId = RagContext.TraceId;

            Stopwatch sw = Stopwatch.StartNew();

            try
            {
                int k = topK ?? _options.Retrieval.TopK;
                _logger.LogInfo(traceId, $"Retrieving top {k} chunks for query: {query}");

                List<RetrievedChunk> results;

                // Use configured retrieval method
                if (_hybridRetriever != null)
                {
                    _logger.LogInfo(traceId, $"Using hybrid retrieval");
                    results = _hybridRetriever.Retrieve(query, k, _chunkStore);
                }
                else if (_denseRetriever != null)
                {
                    _logger.LogInfo(traceId, $"Using dense retrieval");
                    results = _denseRetriever.Retrieve(query, k, _chunkStore);
                }
                else
                {
                    _logger.LogInfo(traceId, $"Using sparse (BM25) retrieval");
                    results = _bm25Retriever.Retrieve(query, k, _chunkStore);
                }

                // Filter by authorization if user context provided
                if (userContext != null)
                {
                    _logger.LogInfo(traceId, $"Applying authorization filter for user: {userContext.UserId}");
                    int beforeCount = results.Count;
                    
                    var filteredResults = new List<RetrievedChunk>();
                    foreach (var r in results)
                    {
                        if (_chunkStore.TryGetValue(r.ChunkId, out var chunk))
                        {
                            if (_authorizer.IsAuthorized(userContext, chunk))
                            {
                                filteredResults.Add(r);
                            }
                        }
                    }
                    results = filteredResults;
                    
                    int afterCount = results.Count;
                    if (beforeCount != afterCount)
                    {
                        _logger.LogInfo(traceId, $"Authorization filtered {beforeCount - afterCount} chunks");
                    }
                }

                // Filter by minimum score
                if (_options.Retrieval.MinScore > 0)
                {
                    int beforeCount = results.Count;
                    var filteredResults = new List<RetrievedChunk>();
                    foreach (var r in results)
                    {
                        if (r.Score >= _options.Retrieval.MinScore)
                        {
                            filteredResults.Add(r);
                        }
                    }
                    results = filteredResults;
                    int afterCount = results.Count;
                    
                    if (beforeCount != afterCount)
                    {
                        _logger.LogInfo(traceId, $"Score threshold filtered {beforeCount - afterCount} chunks");
                    }
                }

                sw.Stop();

                // Log metrics
                _metrics.RecordRetrievalDuration(sw.Elapsed);

                _logger.LogInfo(traceId, $"Retrieved {results.Count} chunks in {sw.ElapsedMilliseconds}ms");

                return results;
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError(traceId, "Retrieve", ex);
                throw;
            }
        }

        /// <summary>
        /// Answers a question by retrieving relevant context and composing a prompt.
        /// </summary>
        /// <param name="question">The question to answer.</param>
        /// <param name="userContext">Optional user context for authorization filtering.</param>
        /// <param name="topK">Optional override for number of chunks to retrieve.</param>
        /// <param name="maxContextTokens">Optional override for maximum context tokens. Uses RagOptions.MaxContextTokens if null.</param>
        /// <returns>A prompt string with context (or insufficient evidence message). Actual LLM generation would happen here in the future.</returns>
        public string AskQuestion(string question, UserContext? userContext = null, int? topK = null, int? maxContextTokens = null)
        {
            if (!_isInitialized)
                throw new InvalidOperationException("Pipeline must be initialized before asking questions. Call Initialize() first.");

            RagContext.TraceId ??= RagContext.GenerateTraceId();
            string traceId = RagContext.TraceId;

            try
            {
                _logger.LogInfo(traceId, $"Processing question: {question}");

                // Retrieve relevant chunks
                List<RetrievedChunk> chunks = Retrieve(question, userContext, topK);

                // Check if we have sufficient evidence
                if (!GroundingRules.HasSufficientEvidence(question, chunks, _options.Retrieval.MinScore))
                {
                    _logger.LogWarning(traceId, $"Insufficient evidence to answer question");
                    return GroundingRules.GenerateInsufficientEvidenceResponse(question);
                }

                // Compose prompt
                _logger.LogInfo(traceId, $"Composing prompt");
                var composer = new PromptComposer(_options.Retrieval);
                string prompt = composer.ComposePrompt(question, chunks, _chunkStore);

                _logger.LogInfo(traceId, $"Prompt composed successfully");

                // Note: Actual LLM generation would be called here
                // For now, we just return the prompt
                return prompt;
            }
            catch (Exception ex)
            {
                _logger.LogError(traceId, "AskQuestion", ex);
                throw;
            }
        }

        /// <summary>
        /// Enables dense (vector-based) retrieval using feature hashing embeddings.
        /// </summary>
        /// <param name="embeddingDim">Dimension of the embedding vectors.</param>
        /// <param name="seed">Random seed for deterministic embeddings.</param>
        public void EnableDenseRetrieval(int embeddingDim = 256, int seed = 42)
        {
            if (!_isInitialized)
                throw new InvalidOperationException("Pipeline must be initialized before enabling dense retrieval. Call Initialize() first.");

            RagContext.TraceId ??= RagContext.GenerateTraceId();
            string traceId = RagContext.TraceId;

            try
            {
                _logger.LogInfo(traceId, $"Enabling dense retrieval with embedding dimension {embeddingDim}");

                // Create embedder and vector store
                var embedder = new FeatureHashingEmbedder(embeddingDim, seed);
                var vectorStore = new VectorStoreFlat(embeddingDim);

                // Build vector index from existing chunks
                _logger.LogInfo(traceId, $"Building vector index from {_chunkStore.Count} chunks");
                
                foreach (var kvp in _chunkStore)
                {
                    string chunkId = kvp.Key;
                    Chunk chunk = kvp.Value;
                    float[] embedding = embedder.Embed(chunk.Text);
                    vectorStore.AddVector(chunkId, embedding);
                }

                // Create dense retriever
                _denseRetriever = new DenseRetriever(vectorStore, embedder);

                _logger.LogInfo(traceId, $"Dense retrieval enabled successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(traceId, "EnableDenseRetrieval", ex);
                throw;
            }
        }

        /// <summary>
        /// Enables hybrid retrieval that combines sparse (BM25) and dense (vector) retrieval.
        /// Requires dense retrieval to be enabled first.
        /// </summary>
        /// <param name="sparseWeight">Weight for sparse retrieval scores (0.0 to 1.0).</param>
        /// <param name="denseWeight">Weight for dense retrieval scores (0.0 to 1.0).</param>
        public void EnableHybridRetrieval(float sparseWeight = 0.5f, float denseWeight = 0.5f)
        {
            if (!_isInitialized)
                throw new InvalidOperationException("Pipeline must be initialized before enabling hybrid retrieval. Call Initialize() first.");

            if (_denseRetriever == null)
                throw new InvalidOperationException("Dense retrieval must be enabled before enabling hybrid retrieval. Call EnableDenseRetrieval() first.");

            RagContext.TraceId ??= RagContext.GenerateTraceId();
            string traceId = RagContext.TraceId;

            try
            {
                _logger.LogInfo(traceId, $"Enabling hybrid retrieval with weights (sparse={sparseWeight}, dense={denseWeight})");

                _hybridRetriever = new HybridRetriever(_bm25Retriever, _denseRetriever, sparseWeight, denseWeight);

                _logger.LogInfo(traceId, $"Hybrid retrieval enabled successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(traceId, "EnableHybridRetrieval", ex);
                throw;
            }
        }

        /// <summary>
        /// Gets the current number of chunks in the index.
        /// </summary>
        public int ChunkCount => _chunkStore.Count;

        /// <summary>
        /// Gets whether the pipeline is initialized.
        /// </summary>
        public bool IsInitialized => _isInitialized;

        /// <summary>
        /// Gets whether dense retrieval is enabled.
        /// </summary>
        public bool IsDenseRetrievalEnabled => _denseRetriever != null;

        /// <summary>
        /// Gets whether hybrid retrieval is enabled.
        /// </summary>
        public bool IsHybridRetrievalEnabled => _hybridRetriever != null;
    }
}
