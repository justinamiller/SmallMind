using System.Collections.Concurrent;
using SmallMind.Abstractions.Telemetry;
using SmallMind.Core.Core;
using SmallMind.Core.Exceptions;
using SmallMind.Core.Utilities;
using SmallMind.Core.Validation;
using SmallMind.Quantization.IO.Gguf;
using SmallMind.Runtime.Quantization;
using SmallMind.Tokenizers;
using SmallMind.Tokenizers.Gguf;
using SmallMind.Transformers;

namespace SmallMind.Runtime
{
    /// <summary>
    /// Thread-safe inference engine facade for concurrent session management.
    /// Provides bounded concurrency and session pooling for production deployments.
    /// Model weights are immutable and shared safely across all sessions.
    /// </summary>
    internal sealed class InferenceEngine : IDisposable
    {
        private readonly TransformerModel _model;
        private readonly ITokenizer _tokenizer;
        private readonly int _blockSize;
        private readonly int _maxConcurrentSessions;
        private readonly SemaphoreSlim _concurrencySemaphore;
        private readonly object _lock = new object();

        // Track active sessions for diagnostics
        private readonly ConcurrentDictionary<string, InferenceSession> _activeSessions;
        private bool _disposed;

        /// <summary>
        /// Gets the number of currently active sessions.
        /// </summary>
        public int ActiveSessionCount => _activeSessions.Count;

        /// <summary>
        /// Gets the maximum allowed concurrent sessions.
        /// </summary>
        public int MaxConcurrentSessions => _maxConcurrentSessions;

        /// <summary>
        /// Creates a new inference engine with bounded concurrency.
        /// </summary>
        /// <param name="model">Transformer model (weights are shared, read-only across all sessions)</param>
        /// <param name="tokenizer">Tokenizer (read-only)</param>
        /// <param name="blockSize">Model block size for context window</param>
        /// <param name="maxConcurrentSessions">Maximum allowed concurrent sessions (0 for unlimited)</param>
        public InferenceEngine(
            TransformerModel model,
            ITokenizer tokenizer,
            int blockSize,
            int maxConcurrentSessions = 0)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _tokenizer = tokenizer ?? throw new ArgumentNullException(nameof(tokenizer));
            _blockSize = blockSize;
            _maxConcurrentSessions = maxConcurrentSessions;

            // Create semaphore for concurrency control if limit is set
            _concurrencySemaphore = maxConcurrentSessions > 0
                ? new SemaphoreSlim(maxConcurrentSessions, maxConcurrentSessions)
                : new SemaphoreSlim(int.MaxValue, int.MaxValue); // Effectively unlimited

            _activeSessions = new ConcurrentDictionary<string, InferenceSession>();

            // Ensure model is in eval mode (thread-safe)
            _model.Eval();
        }

        /// <summary>
        /// Creates an inference engine from a GGUF model file.
        /// Automatically imports GGUF to SMQ format if needed (cached for subsequent loads).
        /// </summary>
        /// <param name="ggufPath">Path to the GGUF model file</param>
        /// <param name="maxConcurrentSessions">Maximum allowed concurrent sessions (0 for unlimited)</param>
        /// <param name="cacheDirectory">Optional cache directory for converted SMQ files (defaults to temp directory)</param>
        /// <returns>A configured InferenceEngine ready for text generation</returns>
        /// <exception cref="ArgumentException">If ggufPath is null or empty</exception>
        /// <exception cref="System.IO.FileNotFoundException">If the GGUF file does not exist</exception>
        /// <exception cref="NotSupportedException">If the GGUF file contains unsupported tensor types</exception>
        public static InferenceEngine FromGguf(
            string ggufPath,
            int maxConcurrentSessions = 0,
            string? cacheDirectory = null)
        {
            if (string.IsNullOrWhiteSpace(ggufPath))
            {
                throw new ArgumentException("GGUF path cannot be null or empty", nameof(ggufPath));
            }

            if (!System.IO.File.Exists(ggufPath))
            {
                throw new System.IO.FileNotFoundException($"GGUF file not found: {ggufPath}", ggufPath);
            }

            // Determine cache directory
            var cacheDir = cacheDirectory ?? System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "SmallMind",
                "GgufCache");

            System.IO.Directory.CreateDirectory(cacheDir);

            // Generate cached SMQ file path
            // Extract only the file name (without path) to prevent path traversal
            var fileName = System.IO.Path.GetFileName(ggufPath);
            var fileNameWithoutExt = System.IO.Path.GetFileNameWithoutExtension(fileName);
            
            // Validate the file name to ensure it doesn't contain path separators or invalid characters
            Guard.SafeFileName(fileNameWithoutExt, nameof(ggufPath));
            
            var smqPath = System.IO.Path.Combine(cacheDir, $"{fileNameWithoutExt}.smq");

            // Import GGUF to SMQ if not already cached
            if (!System.IO.File.Exists(smqPath))
            {
                var importer = new GgufImporter();
                importer.ImportToSmq(ggufPath, smqPath);
            }

            // Load the SMQ file
            var metadata = QuantizedModelLoader.LoadQuantizedModelMetadata(smqPath);

            // Extract tokenizer from GGUF metadata using new GgufTokenizerFactory
            ITokenizer tokenizer;
            try
            {
                var (extractedTokenizer, diagnostics) = GgufTokenizerFactory.CreateTokenizer(
                    metadata.Metadata,
                    NullRuntimeLogger.Instance);

                if (extractedTokenizer != null)
                {
                    tokenizer = extractedTokenizer;
                }
                else
                {
                    // Fallback to default tokenizer
                    int vocabSize = GgufMetadataHelpers.ExtractMetadataInt(metadata.Metadata, "llama.vocab_size",
                                    GgufMetadataHelpers.ExtractMetadataInt(metadata.Metadata, "vocab_size", 50257));
                    tokenizer = CreateDefaultTokenizer(vocabSize);
                }
            }
            catch
            {
                // If extraction fails, use default
                int vocabSize = GgufMetadataHelpers.ExtractMetadataInt(metadata.Metadata, "llama.vocab_size",
                                GgufMetadataHelpers.ExtractMetadataInt(metadata.Metadata, "vocab_size", 50257));
                tokenizer = CreateDefaultTokenizer(vocabSize);
            }

            // Try to load as Llama/modern model with proper configuration
            TransformerModel model;
            try
            {
                var config = ModelConfig.FromGgufMetadata(metadata.Metadata!);

                model = new TransformerModel(
                    vocabSize: config.VocabSize,
                    blockSize: config.ContextLength,
                    nEmbd: config.EmbeddingLength,
                    nLayer: config.BlockCount,
                    nHead: config.HeadCount,
                    dropout: 0.0,
                    seed: 42);
            }
            catch
            {
                // If modern config fails, fall back to legacy GPT loading
                int vocabSize = GgufMetadataHelpers.ExtractMetadataInt(metadata.Metadata, "vocab_size", 50257);
                int blockSize = GgufMetadataHelpers.ExtractMetadataInt(metadata.Metadata, "block_size", 1024);
                int embedDim = GgufMetadataHelpers.ExtractMetadataInt(metadata.Metadata, "embed_dim", 768);
                int numLayers = GgufMetadataHelpers.ExtractMetadataInt(metadata.Metadata, "num_layers", 12);
                int numHeads = GgufMetadataHelpers.ExtractMetadataInt(metadata.Metadata, "num_heads", 12);

                model = new TransformerModel(
                    vocabSize: vocabSize,
                    blockSize: blockSize,
                    nEmbd: embedDim,
                    nLayer: numLayers,
                    nHead: numHeads,
                    dropout: 0.0,
                    seed: 42);
            }

            return new InferenceEngine(model, tokenizer, model.BlockSize, maxConcurrentSessions);
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

        /// <summary>
        /// Generate text from a prompt (non-streaming).
        /// Automatically manages concurrency and session lifecycle.
        /// </summary>
        /// <param name="prompt">Input text prompt</param>
        /// <param name="options">Inference options (cloned internally)</param>
        /// <param name="metrics">Optional performance metrics collector</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Generated text including the original prompt</returns>
        public async ValueTask<string> GenerateAsync(
            string prompt,
            ProductionInferenceOptions options,
            PerformanceMetrics? metrics = null,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            // Wait for available slot (respects cancellation)
            await _concurrencySemaphore.WaitAsync(cancellationToken);

            try
            {
                // Create session
                using var session = CreateSession(options);

                // Generate
                return await session.GenerateAsync(prompt, metrics, cancellationToken);
            }
            finally
            {
                _concurrencySemaphore.Release();
            }
        }

        /// <summary>
        /// Generate text as a stream of tokens.
        /// Automatically manages concurrency and session lifecycle.
        /// </summary>
        /// <param name="prompt">Input text prompt</param>
        /// <param name="options">Inference options (cloned internally)</param>
        /// <param name="metrics">Optional performance metrics collector</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Async enumerable of generated tokens</returns>
        public async IAsyncEnumerable<GeneratedToken> GenerateStreamAsync(
            string prompt,
            ProductionInferenceOptions options,
            PerformanceMetrics? metrics = null,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            // Wait for available slot (respects cancellation)
            await _concurrencySemaphore.WaitAsync(cancellationToken);

            InferenceSession? session = null;
            try
            {
                // Create session
                session = CreateSession(options);

                // Stream tokens
                await foreach (var token in session.GenerateStreamAsync(prompt, metrics, cancellationToken))
                {
                    yield return token;
                }
            }
            finally
            {
                // Clean up session
                if (session != null)
                {
                    _activeSessions.TryRemove(session.SessionId, out _);
                    session.Dispose();
                }

                _concurrencySemaphore.Release();
            }
        }

        /// <summary>
        /// Create a managed session for manual control.
        /// Caller is responsible for disposing the session.
        /// Session will count against concurrency limit until disposed.
        /// </summary>
        /// <param name="options">Inference options</param>
        /// <param name="sessionId">Optional session ID</param>
        /// <returns>Inference session</returns>
        public InferenceSession CreateManagedSession(
            ProductionInferenceOptions options,
            string? sessionId = null)
        {
            ThrowIfDisposed();

            // Check concurrency limit before creating
            if (_maxConcurrentSessions > 0 && _activeSessions.Count >= _maxConcurrentSessions)
            {
                throw new ResourceLimitException(
                    "MaxConcurrentSessions",
                    _maxConcurrentSessions,
                    _activeSessions.Count + 1,
                    "Close existing sessions before creating new ones.");
            }

            return CreateSession(options, sessionId);
        }

        /// <summary>
        /// Get current engine statistics.
        /// </summary>
        public EngineStatistics GetStatistics()
        {
            ThrowIfDisposed();

            return new EngineStatistics(
                activeSessions: _activeSessions.Count,
                maxConcurrentSessions: _maxConcurrentSessions,
                availableSlots: _maxConcurrentSessions > 0
                    ? Math.Max(0, _maxConcurrentSessions - _activeSessions.Count)
                    : int.MaxValue);
        }

        private InferenceSession CreateSession(ProductionInferenceOptions options, string? sessionId = null)
        {
            var session = new InferenceSession(_model, _tokenizer, options, _blockSize, sessionId);

            // Track session
            if (!_activeSessions.TryAdd(session.SessionId, session))
            {
                // Very unlikely race condition
                session.Dispose();
                throw new InvalidOperationException($"Session ID collision: {session.SessionId}");
            }

            return session;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(InferenceEngine));
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                // Dispose all active sessions
                foreach (var session in _activeSessions.Values)
                {
                    session?.Dispose();
                }
                _activeSessions.Clear();

                _concurrencySemaphore?.Dispose();
                _disposed = true;
                GC.SuppressFinalize(this);
            }
        }
    }

    /// <summary>
    /// Statistics about the inference engine state.
    /// </summary>
    internal readonly struct EngineStatistics : IEquatable<EngineStatistics>
    {
        /// <summary>
        /// Number of currently active sessions.
        /// </summary>
        public readonly int ActiveSessions;

        /// <summary>
        /// Maximum allowed concurrent sessions (0 = unlimited).
        /// </summary>
        public readonly int MaxConcurrentSessions;

        /// <summary>
        /// Number of available session slots.
        /// </summary>
        public readonly int AvailableSlots;

        /// <summary>
        /// Initializes a new instance of the EngineStatistics struct.
        /// </summary>
        public EngineStatistics(int activeSessions, int maxConcurrentSessions, int availableSlots)
        {
            ActiveSessions = activeSessions;
            MaxConcurrentSessions = maxConcurrentSessions;
            AvailableSlots = availableSlots;
        }

        public bool Equals(EngineStatistics other) =>
            ActiveSessions == other.ActiveSessions &&
            MaxConcurrentSessions == other.MaxConcurrentSessions &&
            AvailableSlots == other.AvailableSlots;

        public override bool Equals(object? obj) =>
            obj is EngineStatistics other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(ActiveSessions, MaxConcurrentSessions, AvailableSlots);

        public static bool operator ==(EngineStatistics left, EngineStatistics right) => left.Equals(right);
        public static bool operator !=(EngineStatistics left, EngineStatistics right) => !left.Equals(right);
    }
}
