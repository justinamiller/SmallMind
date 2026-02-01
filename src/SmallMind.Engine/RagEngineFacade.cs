using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using SmallMind.Abstractions;
using RagNamespace = SmallMind.Rag;
using SmallMind.Rag.Pipeline;
using SmallMind.Rag.Indexing;
using SmallMind.Rag.Generation;
using SmallMind.Runtime;
using PublicGenerationOptions = SmallMind.Abstractions.GenerationOptions;
using RagGenerationOptions = SmallMind.Rag.Generation.GenerationOptions;
using RagITextGenerator = SmallMind.Rag.Generation.ITextGenerator;

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
                    var fileName = Path.GetFileName(sourcePath);
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

            // Create text generator adapter
            var modelHandle = (ModelHandle)model;
            var textGenerator = new InferenceEngineAdapter(modelHandle);

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
            // TODO: Access chunk store to get full chunk details (CharStart, CharEnd) for accurate location
            var chunks = pipeline.Retrieve(request.Query, userContext: null, topK: request.TopK);
            var citations = chunks
                .Where(c => c.Score >= request.MinConfidence)
                .Select(c => new RagCitation
                {
                    SourceUri = $"chunk://{c.ChunkId}",
                    // Note: CharRange and LineRange would need actual Chunk lookup from chunk store
                    // RetrievedChunk only has ChunkId, DocId, Score, Rank, Excerpt
                    CharRange = (0, 0), // Placeholder - requires chunk store access
                    LineRange = null, // Optional in schema
                    Snippet = c.Excerpt.Length > 200 ? c.Excerpt.Substring(0, 200) + "..." : c.Excerpt,
                    Confidence = c.Score
                })
                .ToArray();

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

            if (chunks.Count == 0 || (request.MinConfidence > 0 && chunks.All(c => c.Score < request.MinConfidence)))
            {
                throw new RagInsufficientEvidenceException(request.Query, request.MinConfidence);
            }

            // Emit started event
            yield return new TokenEvent
            {
                Kind = TokenEventKind.Started,
                Text = ReadOnlyMemory<char>.Empty,
                TokenId = -1,
                GeneratedTokens = 0,
                IsFinal = false
            };

            // Stream tokens from generator
            int tokenCount = 0;
            bool isLast = false;
            await foreach (var token in textGenerator.GenerateStreamingAsync(
                request.Query,
                cancellationToken: cancellationToken))
            {
                tokenCount++;
                isLast = (tokenCount >= request.GenerationOptions.MaxNewTokens);

                yield return new TokenEvent
                {
                    Kind = TokenEventKind.Token,
                    Text = token.Text.AsMemory(),
                    TokenId = token.TokenId,
                    GeneratedTokens = tokenCount,
                    IsFinal = isLast
                };

                if (isLast)
                {
                    break;
                }
            }

            // Emit completed event
            yield return new TokenEvent
            {
                Kind = TokenEventKind.Completed,
                Text = ReadOnlyMemory<char>.Empty,
                TokenId = -1,
                GeneratedTokens = tokenCount,
                IsFinal = true
            };
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
