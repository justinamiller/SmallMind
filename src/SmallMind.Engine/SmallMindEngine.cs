using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using SmallMind.Abstractions;
using SmallMind.Runtime;
using SmallMind.Runtime.Quantization;
using SmallMind.Tokenizers;
using SmallMind.Transformers;
using SmallMind.Rag.Pipeline;
using SmallMind.Rag;
using SmallMind.Quantization.IO.Gguf;

namespace SmallMind.Engine
{
    /// <summary>
    /// Main engine facade that adapts internal SmallMind components to the stable public API.
    /// Thread-safe and supports concurrent operations.
    /// </summary>
    internal sealed class SmallMindEngine : ISmallMindEngine
    {
        private readonly SmallMindOptions _options;
        private readonly IRagEngine? _ragEngine;
        private bool _disposed;

        public SmallMindEngine(SmallMindOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));

            // Initialize RAG engine if enabled
            if (_options.EnableRag)
            {
                _ragEngine = new RagEngineFacade();
            }
        }

        public EngineCapabilities Capabilities => new EngineCapabilities
        {
            SupportsQuantizedInference = true,
            SupportsGgufImport = true,
            SupportsRag = _options.EnableRag,
            SupportsKvCache = _options.EnableKvCache,
            SupportsBatching = _options.EnableBatching,
            SupportsDeterministicMode = true,
            SupportsStreaming = true
        };

        public IRagEngine? Rag => _ragEngine;

        public async ValueTask<IModelHandle> LoadModelAsync(
            ModelLoadRequest request,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (string.IsNullOrWhiteSpace(request.Path))
            {
                throw new ArgumentException("Model path cannot be empty", nameof(request));
            }

            if (!File.Exists(request.Path))
            {
                throw new FileNotFoundException($"Model file not found: {request.Path}");
            }

            var ext = Path.GetExtension(request.Path).ToLowerInvariant();

            // Handle GGUF import if requested
            if (ext == ".gguf")
            {
                if (!request.AllowGgufImport)
                {
                    throw new UnsupportedModelException(
                        request.Path,
                        ext,
                        "GGUF files require AllowGgufImport=true in ModelLoadRequest");
                }

                return await LoadGgufModelAsync(request, cancellationToken);
            }

            // Handle SMQ files
            if (ext == ".smq")
            {
                return await LoadSmqModelAsync(request, cancellationToken);
            }

            // Unsupported format
            throw new UnsupportedModelException(
                request.Path,
                ext,
                $"Unsupported model format '{ext}'. Supported formats: .smq, .gguf (with AllowGgufImport=true)");
        }

        public IChatSession CreateChatSession(IModelHandle model, SessionOptions options)
        {
            ThrowIfDisposed();

            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            if (model is not ModelHandle internalHandle)
            {
                throw new ArgumentException("Model handle must be created by this engine", nameof(model));
            }

            return new ChatSession(internalHandle, options ?? new SessionOptions(), _options);
        }

        public async ValueTask<GenerationResult> GenerateAsync(
            IModelHandle model,
            GenerationRequest request,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            if (model is not ModelHandle internalHandle)
            {
                throw new ArgumentException("Model handle must be created by this engine", nameof(model));
            }

            // Use existing inference engine
            var session = internalHandle.CreateInferenceSession(request.Options, _options);
            try
            {
                var text = await session.GenerateAsync(
                    request.Prompt,
                    metrics: null,
                    cancellationToken: cancellationToken);

                return new GenerationResult
                {
                    Text = text,
                    GeneratedTokens = session.Options.MaxNewTokens, // Approximate
                    StoppedByBudget = false,
                    StopReason = "completed"
                };
            }
            finally
            {
                session.Dispose();
            }
        }

        public async IAsyncEnumerable<TokenEvent> GenerateStreamingAsync(
            IModelHandle model,
            GenerationRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            if (model is not ModelHandle internalHandle)
            {
                throw new ArgumentException("Model handle must be created by this engine", nameof(model));
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

            int tokenCount = 0;

            var session = internalHandle.CreateInferenceSession(request.Options, _options);
            try
            {
                bool isLast = false;
                await foreach (var token in session.GenerateStreamAsync(
                    request.Prompt,
                    metrics: null,
                    cancellationToken: cancellationToken))
                {
                    tokenCount++;
                    isLast = (tokenCount >= request.Options.MaxNewTokens);

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
            finally
            {
                session.Dispose();
            }
        }

        private async ValueTask<IModelHandle> LoadSmqModelAsync(
            ModelLoadRequest request,
            CancellationToken cancellationToken)
        {
            // Load SMQ metadata
            var metadata = QuantizedModelLoader.LoadQuantizedModelMetadata(request.Path);

            // Extract model dimensions from metadata
            int vocabSize = ExtractMetadataInt(metadata.Metadata, "vocab_size", 50257);
            int blockSize = ExtractMetadataInt(metadata.Metadata, "block_size", 1024);
            int embedDim = ExtractMetadataInt(metadata.Metadata, "embed_dim", 768);
            int numLayers = ExtractMetadataInt(metadata.Metadata, "num_layers", 12);
            int numHeads = ExtractMetadataInt(metadata.Metadata, "num_heads", 12);

            // For now, we create a placeholder FP32 model structure
            // In a production system, this would load the quantized weights directly
            var model = new TransformerModel(
                vocabSize: vocabSize,
                blockSize: blockSize,
                nEmbd: embedDim,
                nLayer: numLayers,
                nHead: numHeads,
                dropout: 0.0,
                seed: 42);

            // Create a simple tokenizer (in production, load from model metadata)
            var tokenizer = CreateDefaultTokenizer(vocabSize);

            await Task.CompletedTask; // Satisfy async signature
            cancellationToken.ThrowIfCancellationRequested();

            return new ModelHandle(model, tokenizer, request.Path, metadata);
        }

        private async ValueTask<IModelHandle> LoadGgufModelAsync(
            ModelLoadRequest request,
            CancellationToken cancellationToken)
        {
            // Determine cache directory
            var cacheDir = request.ImportCacheDirectory ?? Path.Combine(
                Path.GetTempPath(),
                "SmallMind",
                "GgufCache");

            Directory.CreateDirectory(cacheDir);

            // Generate cached SMQ file path
            var fileName = Path.GetFileNameWithoutExtension(request.Path);
            var smqPath = Path.Combine(cacheDir, $"{fileName}.smq");

            // Import GGUF to SMQ if not already cached
            if (!File.Exists(smqPath))
            {
                try
                {
                    var importer = new GgufImporter();
                    importer.ImportToSmq(request.Path, smqPath);
                }
                catch (NotSupportedException ex)
                {
                    throw new UnsupportedGgufTensorException(
                        "unknown",
                        0,
                        $"GGUF import failed: {ex.Message}");
                }
            }

            // Now load the SMQ file
            var smqRequest = new ModelLoadRequest
            {
                Path = smqPath,
                AllowGgufImport = false,
                Threads = request.Threads,
                MaxMemoryBytes = request.MaxMemoryBytes
            };

            return await LoadSmqModelAsync(smqRequest, cancellationToken);
        }

        private static int ExtractMetadataInt(
            System.Collections.Generic.Dictionary<string, object>? metadata,
            string key,
            int defaultValue)
        {
            if (metadata != null && metadata.TryGetValue(key, out var value))
            {
                return Convert.ToInt32(value);
            }
            return defaultValue;
        }

        private static ITokenizer CreateDefaultTokenizer(int vocabSize)
        {
            // WARNING: This is a fallback tokenizer for demonstration purposes only.
            // Production deployments should load tokenizer configuration from model metadata.
            // This simple character tokenizer is NOT suitable for real models.
            
            var vocab = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 .,!?;:'-\n";
            if (vocab.Length < vocabSize)
            {
                // Pad vocabulary with placeholder characters
                var paddedVocab = vocab;
                for (int i = vocab.Length; i < vocabSize; i++)
                {
                    paddedVocab += ((char)(128 + i)).ToString();
                }
                vocab = paddedVocab;
            }

            return new CharTokenizer(vocab.Substring(0, Math.Min(vocabSize, vocab.Length)));
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(SmallMindEngine));
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }
    }
}
