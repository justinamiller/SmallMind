using System.Runtime.CompilerServices;
using SmallMind.Abstractions;
using SmallMind.Rag.Common;
using SmallMind.Rag.Indexing;
using SmallMind.Rag.Pipeline;
using SmallMind.Runtime;
using PublicGenerationOptions = SmallMind.Abstractions.GenerationOptions;
using RagGenerationOptions = SmallMind.Rag.Generation.GenerationOptions;
using RagITextGenerator = SmallMind.Rag.Generation.ITextGenerator;
using RagNamespace = SmallMind.Rag;

namespace SmallMind.Engine
{
    /// <summary>
    /// Internal implementation of IRagEngine.
    /// Adapts the existing RAG pipeline to the stable public API.
    /// </summary>
    internal sealed class RagEngineFacade : IRagEngine
    {
        public async ValueTask<IRagIndex> BuildIndexAsync(
            RagBuildRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (string.IsNullOrWhiteSpace(request.IndexDirectory))
            {
                throw new ArgumentException("Index directory cannot be empty", nameof(request));
            }

            if (request.SourcePaths == null || request.SourcePaths.Length == 0)
            {
                throw new ArgumentException("At least one source path is required", nameof(request));
            }

            // Create RAG options
            var ragOptions = new RagNamespace.RagOptions
            {
                IndexDirectory = request.IndexDirectory,
                Chunking = new RagNamespace.RagOptions.ChunkingOptions
                {
                    MaxChunkSize = request.ChunkSize,
                    OverlapSize = request.ChunkOverlap
                },
                Retrieval = new RagNamespace.RagOptions.RetrievalOptions
                {
                    TopK = 5,
                    MinScore = 0.0f
                }
            };

            // Create pipeline
            var pipeline = new RagPipeline(ragOptions);
            pipeline.Initialize();

            // Ingest documents from all source paths
            foreach (var sourcePath in request.SourcePaths)
            {
                if (Directory.Exists(sourcePath))
                {
                    pipeline.IngestDocuments(sourcePath, rebuild: false);
                }
                else if (File.Exists(sourcePath))
                {
                    // For single files, ingest parent directory with filter
                    var directory = Path.GetDirectoryName(sourcePath);
                    var extension = Path.GetExtension(sourcePath);

                    if (!string.IsNullOrEmpty(directory))
                    {
                        pipeline.IngestDocuments(directory, rebuild: false, includePatterns: $"*{extension}");
                    }
                }
            }

            await Task.CompletedTask; // Satisfy async signature
            cancellationToken.ThrowIfCancellationRequested();

            return new RagIndexFacade(request.IndexDirectory, pipeline);
        }

        public async ValueTask<RagAnswer> AskAsync(
            IModelHandle model,
            RagAskRequest request,
            CancellationToken cancellationToken = default)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (request.Index is not RagIndexFacade indexFacade)
            {
                throw new ArgumentException("Index must be created by this RAG engine", nameof(request));
            }

            // Use existing pipeline
            var pipeline = indexFacade.Pipeline;

            // Ask question
            var answer = pipeline.AskQuestion(
                request.Query,
                userContext: null,
                topK: request.TopK,
                maxTokens: request.GenerationOptions.MaxNewTokens,
                temperature: request.GenerationOptions.Temperature);

            // Extract citations from chunks
            // Note: charRange uses placeholder (0,0) because chunk store doesn't expose
            // character positions. Future enhancement would require chunk store schema update.
            var chunks = pipeline.Retrieve(request.Query, userContext: null, topK: request.TopK);

            // Replace LINQ with manual loop - avoid Where().Select().ToArray() allocation chain
            int citationCount = 0;
            for (int i = 0; i < chunks.Count; i++)
            {
                if (chunks[i].Score >= request.MinConfidence)
                    citationCount++;
            }

            var citations = new RagCitation[citationCount];
            int citationIndex = 0;
            for (int i = 0; i < chunks.Count; i++)
            {
                var c = chunks[i];
                if (c.Score >= request.MinConfidence)
                {
                    citations[citationIndex++] = new RagCitation(
                        sourceUri: $"chunk://{c.ChunkId}",
                        charRange: (0, 0), // Placeholder - requires chunk store access
                        lineRange: null, // Optional in schema
                        snippet: TextHelper.TruncateWithEllipsis(c.Excerpt, RetrievalConstants.MaxExcerptLength),
                        confidence: c.Score);
                }
            }

            if (citations.Length == 0 && request.MinConfidence > 0)
            {
                throw new RagInsufficientEvidenceException(request.Query, request.MinConfidence);
            }

            await Task.CompletedTask; // Satisfy async signature
            cancellationToken.ThrowIfCancellationRequested();

            return new RagAnswer
            {
                Answer = answer,
                Citations = citations,
                GeneratedTokens = request.GenerationOptions.MaxNewTokens, // Approximate
                StoppedByBudget = false
            };
        }

        public async IAsyncEnumerable<TokenEvent> AskStreamingAsync(
            IModelHandle model,
            RagAskRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (request.Index is not RagIndexFacade indexFacade)
            {
                throw new ArgumentException("Index must be created by this RAG engine", nameof(request));
            }

            // Create text generator adapter
            var modelHandle = (ModelHandle)model;
            var textGenerator = new StreamingInferenceEngineAdapter(modelHandle, request.GenerationOptions);

            var pipeline = indexFacade.Pipeline;

            // Retrieve chunks first
            var chunks = pipeline.Retrieve(request.Query, userContext: null, topK: request.TopK);

            // Replace .All() LINQ with manual loop
            bool hasValidChunk = false;
            if (chunks.Count > 0 && request.MinConfidence > 0)
            {
                for (int i = 0; i < chunks.Count; i++)
                {
                    if (chunks[i].Score >= request.MinConfidence)
                    {
                        hasValidChunk = true;
                        break;
                    }
                }
            }
            else if (chunks.Count > 0)
            {
                hasValidChunk = true;
            }

            if (!hasValidChunk)
            {
                throw new RagInsufficientEvidenceException(request.Query, request.MinConfidence);
            }

            // Emit started event
            yield return new TokenEvent(
                kind: TokenEventKind.Started,
                text: ReadOnlyMemory<char>.Empty,
                tokenId: -1,
                generatedTokens: 0,
                isFinal: false);

            // Stream tokens from generator
            int tokenCount = 0;
            bool isLast = false;
            await foreach (var token in textGenerator.GenerateStreamingAsync(
                request.Query,
                cancellationToken: cancellationToken))
            {
                tokenCount++;
                isLast = (tokenCount >= request.GenerationOptions.MaxNewTokens);

                yield return new TokenEvent(
                    kind: TokenEventKind.Token,
                    text: token.Text.AsMemory(),
                    tokenId: token.TokenId,
                    generatedTokens: tokenCount,
                    isFinal: isLast);

                if (isLast)
                {
                    break;
                }
            }

            // Emit completed event
            yield return new TokenEvent(
                kind: TokenEventKind.Completed,
                text: ReadOnlyMemory<char>.Empty,
                tokenId: -1,
                generatedTokens: tokenCount,
                isFinal: true);
        }
    }

    /// <summary>
    /// Internal implementation of IRagIndex.
    /// </summary>
    internal sealed class RagIndexFacade : IRagIndex
    {
        private readonly string _indexDirectory;
        private readonly RagPipeline _pipeline;
        private bool _disposed;

        public RagIndexFacade(string indexDirectory, RagPipeline pipeline)
        {
            _indexDirectory = indexDirectory ?? throw new ArgumentNullException(nameof(indexDirectory));
            _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
        }

        internal RagPipeline Pipeline => _pipeline;

        public RagIndexInfo Info
        {
            get
            {
                // Load index metadata
                var (_, chunks, metadata) = IndexSerializer.LoadIndex(_indexDirectory);

                return new RagIndexInfo
                {
                    IndexDirectory = _indexDirectory,
                    ChunkCount = chunks.Count,
                    DocumentCount = chunks.Values.Select(c => c.SourceUri).Distinct().Count(),
                    UsesDenseRetrieval = false, // Currently only BM25 is supported
                    CreatedAt = Directory.GetCreationTimeUtc(_indexDirectory)
                };
            }
        }

        public async ValueTask SaveAsync(string directory, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new ArgumentException("Directory cannot be empty", nameof(directory));
            }

            // Index is automatically saved by the pipeline during ingestion
            // For explicit save, we can copy the index files
            if (directory != _indexDirectory)
            {
                Directory.CreateDirectory(directory);

                foreach (var file in Directory.GetFiles(_indexDirectory))
                {
                    var fileName = Path.GetFileName(file);
                    var destFile = Path.Combine(directory, fileName);
                    File.Copy(file, destFile, overwrite: true);
                }
            }

            await Task.CompletedTask; // Satisfy async signature
            cancellationToken.ThrowIfCancellationRequested();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            // RagPipeline doesn't implement IDisposable currently
            _disposed = true;
        }
    }

    /// <summary>
    /// Adapter to use ModelHandle as ITextGenerator for RAG pipeline.
    /// </summary>
    internal sealed class InferenceEngineAdapter : RagITextGenerator
    {
        private readonly ModelHandle _modelHandle;

        public InferenceEngineAdapter(ModelHandle modelHandle)
        {
            _modelHandle = modelHandle ?? throw new ArgumentNullException(nameof(modelHandle));
        }

        public string Generate(string prompt, int maxTokens, double temperature, int? seed)
        {
            var options = new PublicGenerationOptions
            {
                MaxNewTokens = maxTokens,
                Temperature = temperature,
                Mode = seed.HasValue ? GenerationMode.Deterministic : GenerationMode.Exploratory,
                Seed = seed.HasValue ? (uint)seed.Value : 42
            };

            var engineOptions = new SmallMindOptions();
            var session = _modelHandle.CreateInferenceSession(options, engineOptions);

            try
            {
                return session.GenerateAsync(prompt, metrics: null, cancellationToken: default)
                    .GetAwaiter()
                    .GetResult();
            }
            finally
            {
                session.Dispose();
            }
        }

        public async IAsyncEnumerable<int> GenerateStreamAsync(
            string prompt,
            RagGenerationOptions options,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var publicOptions = new PublicGenerationOptions
            {
                MaxNewTokens = options.MaxTokens,
                Temperature = options.Temperature,
                TopK = options.TopK,
                TopP = options.TopP,
                Mode = options.Seed.HasValue ? GenerationMode.Deterministic : GenerationMode.Exploratory,
                Seed = options.Seed.HasValue ? (uint)options.Seed.Value : 42
            };

            var engineOptions = new SmallMindOptions();
            var session = _modelHandle.CreateInferenceSession(publicOptions, engineOptions);

            try
            {
                await foreach (var token in session.GenerateStreamAsync(
                    prompt,
                    metrics: null,
                    cancellationToken: cancellationToken))
                {
                    yield return token.TokenId;
                }
            }
            finally
            {
                session.Dispose();
            }
        }
    }

    /// <summary>
    /// Streaming adapter for RAG generation.
    /// </summary>
    internal sealed class StreamingInferenceEngineAdapter
    {
        private readonly ModelHandle _modelHandle;
        private readonly PublicGenerationOptions _options;

        public StreamingInferenceEngineAdapter(ModelHandle modelHandle, PublicGenerationOptions options)
        {
            _modelHandle = modelHandle ?? throw new ArgumentNullException(nameof(modelHandle));
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public async IAsyncEnumerable<GeneratedToken> GenerateStreamingAsync(
            string prompt,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var engineOptions = new SmallMindOptions();
            var session = _modelHandle.CreateInferenceSession(_options, engineOptions);

            try
            {
                await foreach (var token in session.GenerateStreamAsync(
                    prompt,
                    metrics: null,
                    cancellationToken: cancellationToken))
                {
                    yield return token;
                }
            }
            finally
            {
                session.Dispose();
            }
        }
    }
}
