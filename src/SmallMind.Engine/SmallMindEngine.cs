using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using SmallMind.Abstractions;
using SmallMind.Core.Core;
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

            // Early validation with actionable error messages
            ModelValidator.ValidatePathAndExtension(request);

            var ext = Path.GetExtension(request.Path).ToLowerInvariant();

            // Handle GGUF import if requested
            if (ext == ".gguf")
            {
                return await LoadGgufModelAsync(request, cancellationToken);
            }

            // Handle SMQ files
            if (ext == ".smq")
            {
                return await LoadSmqModelAsync(request, cancellationToken);
            }

            // Should never reach here due to ValidatePathAndExtension, but defensive
            throw new UnsupportedModelException(
                request.Path,
                ext,
                $"Unsupported model format '{ext}'. This should have been caught by validation.");
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

            // Enforce budgets with BudgetEnforcer
            using var budgetEnforcer = new BudgetEnforcer(request.Options, cancellationToken);

            // Use existing inference engine with combined cancellation token
            var session = internalHandle.CreateInferenceSession(request.Options, _options);
            try
            {
                // Tokenize prompt to validate context length (use tokenizer from model handle)
                var promptTokens = internalHandle.Tokenizer.Encode(request.Prompt);
                budgetEnforcer.ValidateContextLength(promptTokens.Count);

                var text = await session.GenerateAsync(
                    request.Prompt,
                    metrics: null,
                    cancellationToken: budgetEnforcer.CombinedToken);

                // Return actual generated tokens (don't use fallback estimate)
                var generatedTokens = budgetEnforcer.GeneratedTokens;

                return new GenerationResult
                {
                    Text = text,
                    GeneratedTokens = generatedTokens,
                    StoppedByBudget = budgetEnforcer.BudgetExceeded,
                    StopReason = budgetEnforcer.BudgetExceeded 
                        ? $"budget_exceeded_{budgetEnforcer.ExceededReason}" 
                        : "completed"
                };
            }
            catch (OperationCanceledException) when (budgetEnforcer.BudgetExceeded)
            {
                // Budget exceeded, throw our exception instead
                budgetEnforcer.ThrowIfExceeded();
                // If ThrowIfExceeded didn't throw (shouldn't happen), rethrow original
                throw;
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

            // Use helper method to avoid yield in try-catch
            await foreach (var token in GenerateStreamingAsyncImpl(internalHandle, request, cancellationToken))
            {
                yield return token;
            }
        }

        private async IAsyncEnumerable<TokenEvent> GenerateStreamingAsyncImpl(
            ModelHandle internalHandle,
            GenerationRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            // Enforce budgets with BudgetEnforcer
            using var budgetEnforcer = new BudgetEnforcer(request.Options, cancellationToken);

            // Emit started event
            yield return new TokenEvent(
                kind: TokenEventKind.Started,
                text: ReadOnlyMemory<char>.Empty,
                tokenId: -1,
                generatedTokens: 0,
                isFinal: false);

            InferenceSession? session = null;

            try
            {
                session = internalHandle.CreateInferenceSession(request.Options, _options);
                
                // Tokenize prompt to validate context length (use tokenizer from model handle)
                var promptTokens = internalHandle.Tokenizer.Encode(request.Prompt);
                budgetEnforcer.ValidateContextLength(promptTokens.Count);

                // Use helper to avoid yield in try-catch
                await foreach (var token in GenerateTokensWithBudgetAsync(session, request, budgetEnforcer))
                {
                    yield return token;

                    // Stop if final token
                    if (token.IsFinal)
                    {
                        yield break;
                    }
                }

                // Emit completed event
                yield return new TokenEvent(
                    kind: TokenEventKind.Completed,
                    text: ReadOnlyMemory<char>.Empty,
                    tokenId: -1,
                    generatedTokens: budgetEnforcer.GeneratedTokens,
                    isFinal: true);
            }
            finally
            {
                session?.Dispose();
            }
        }

        private async IAsyncEnumerable<TokenEvent> GenerateTokensWithBudgetAsync(
            InferenceSession session,
            GenerationRequest request,
            BudgetEnforcer budgetEnforcer)
        {
            await foreach (var token in session.GenerateStreamAsync(
                request.Prompt,
                metrics: null,
                cancellationToken: budgetEnforcer.CombinedToken))
            {
                // Check budget before emitting token
                if (!budgetEnforcer.ShouldContinue())
                {
                    // Budget exceeded - emit error event and stop
                    yield return new TokenEvent(
                        kind: TokenEventKind.Error,
                        text: $"Budget exceeded: {budgetEnforcer.ExceededReason}".AsMemory(),
                        tokenId: -1,
                        generatedTokens: budgetEnforcer.GeneratedTokens,
                        isFinal: true,
                        error: $"Budget exceeded: {budgetEnforcer.ExceededReason}");
                    yield break;
                }

                budgetEnforcer.IncrementTokenCount();

                bool isLast = !budgetEnforcer.ShouldContinue();

                yield return new TokenEvent(
                    kind: TokenEventKind.Token,
                    text: token.Text.AsMemory(),
                    tokenId: token.TokenId,
                    generatedTokens: budgetEnforcer.GeneratedTokens,
                    isFinal: isLast);

                if (isLast)
                {
                    break;
                }
            }
        }

        private async ValueTask<IModelHandle> LoadSmqModelAsync(
            ModelLoadRequest request,
            CancellationToken cancellationToken)
        {
            // Load SMQ metadata
            var metadata = QuantizedModelLoader.LoadQuantizedModelMetadata(request.Path);

            // Validate metadata sanity
            ModelValidator.ValidateMetadata(metadata.Metadata, request.Path);

            // Extract model dimensions from metadata
            int vocabSize = ExtractMetadataInt(metadata.Metadata, "vocab_size", 50257);
            int blockSize = ExtractMetadataInt(metadata.Metadata, "block_size", 1024);
            int embedDim = ExtractMetadataInt(metadata.Metadata, "embed_dim", 768);
            int numLayers = ExtractMetadataInt(metadata.Metadata, "num_layers", 12);
            int numHeads = ExtractMetadataInt(metadata.Metadata, "num_heads", 12);

            // Check memory budget if specified
            if (request.MaxMemoryBytes.HasValue)
            {
                // Create memory configuration for validation
                var memoryConfig = new MemoryConfiguration(
                    enableGradientCheckpointing: false,
                    enableMixedPrecision: false,
                    enableMemoryMapping: false,
                    strictBudget: request.EnableStrictMemoryBudget 
                        ? new StrictMemoryBudget(
                            request.MaxMemoryBytes.Value,
                            maxBytesPerSession: request.MaxMemoryBytes.Value,
                            rejectOnExceed: true,
                            preAllocate: request.PreAllocateMemory,
                            safetyMargin: request.StrictBudgetSafetyMargin)
                        : null);

                // Perform pre-flight check (using reasonable defaults for batch size and sequence length)
                var checkResult = memoryConfig.CheckBeforeRun(
                    vocabSize: vocabSize,
                    embeddingDim: embedDim,
                    numLayers: numLayers,
                    numHeads: numHeads,
                    batchSize: 1,
                    seqLength: blockSize);

                if (!checkResult.CanProceed)
                {
                    throw new UnsupportedModelException(
                        request.Path,
                        ".smq",
                        $"Model loading rejected by memory budget check:\n{checkResult.GetSummary()}");
                }
            }
            else
            {
                // Legacy path: estimate memory without strict budgeting (advisory only)
                // Operation proceeds even if estimated memory exceeds available memory
                var estimatedBytes = ModelValidator.EstimateMemoryRequirementBytes(
                    metadata.Metadata,
                    quantizationBits: 8); // Assume Q8 for SMQ files

                // No rejection in legacy mode - just proceed (backward compatible behavior)
            }

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
                // Handle JsonElement from deserialized SMQ metadata
                if (value is System.Text.Json.JsonElement jsonElement)
                {
                    if (jsonElement.ValueKind == System.Text.Json.JsonValueKind.Number)
                    {
                        return jsonElement.GetInt32();
                    }
                    else if (jsonElement.ValueKind == System.Text.Json.JsonValueKind.String)
                    {
                        if (int.TryParse(jsonElement.GetString(), out int parsed))
                        {
                            return parsed;
                        }
                    }
                }
                
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
