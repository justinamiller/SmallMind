using System;
using SmallMind.Abstractions;
using SmallMind.Runtime;
using SmallMind.Runtime.Quantization;
using SmallMind.Tokenizers;
using SmallMind.Transformers;
using PublicGenerationOptions = SmallMind.Abstractions.GenerationOptions;

namespace SmallMind.Engine
{
    /// <summary>
    /// Internal implementation of IModelHandle.
    /// Wraps a TransformerModel and provides access to inference capabilities.
    /// </summary>
    internal sealed class ModelHandle : IModelHandle
    {
        private readonly TransformerModel _model;
        private readonly ITokenizer _tokenizer;
        private readonly string _path;
        private readonly SmqModelInfo? _metadata;
        private bool _disposed;

        public ModelHandle(
            TransformerModel model,
            ITokenizer tokenizer,
            string path,
            SmqModelInfo? metadata)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _tokenizer = tokenizer ?? throw new ArgumentNullException(nameof(tokenizer));
            _path = path ?? throw new ArgumentNullException(nameof(path));
            _metadata = metadata;
        }

        public ModelInfo Info => new ModelInfo
        {
            Name = System.IO.Path.GetFileNameWithoutExtension(_path),
            VocabSize = _model.VocabSize,
            MaxContextLength = _model.BlockSize,
            QuantizationSchemes = ExtractQuantizationSchemes(),
            EngineVersion = GetEngineVersion(),
            BuildHash = GetBuildHash()
        };

        internal TransformerModel Model => _model;
        internal ITokenizer Tokenizer => _tokenizer;

        /// <summary>
        /// Creates an inference session for this model.
        /// </summary>
        internal InferenceSession CreateInferenceSession(
            PublicGenerationOptions options,
            SmallMindOptions engineOptions)
        {
            ThrowIfDisposed();

            // Map public GenerationOptions to internal ProductionInferenceOptions
            var internalOptions = new ProductionInferenceOptions
            {
                Temperature = options.Temperature,
                TopK = options.TopK,
                TopP = options.TopP,
                MaxNewTokens = options.MaxNewTokens,
                MaxContextTokens = options.MaxContextTokens,
                MaxTimeMs = options.TimeoutMs,
                Seed = options.Mode == GenerationMode.Deterministic ? (int?)options.Seed : null
            };

            return new InferenceSession(
                _model,
                _tokenizer,
                internalOptions,
                _model.BlockSize);
        }

        private string[] ExtractQuantizationSchemes()
        {
            if (_metadata?.Metadata == null)
            {
                return new[] { "FP32" };
            }

            if (_metadata.Metadata.TryGetValue("quantization_version", out var version))
            {
                return new[] { $"SMQ-{version}" };
            }

            return new[] { "SMQ" };
        }

        private static string GetEngineVersion()
        {
            var version = typeof(SmallMindEngine).Assembly.GetName().Version;
            return version?.ToString() ?? "1.0.0";
        }

        private static string GetBuildHash()
        {
            // In production, this would come from build metadata or Git commit hash
            // For now, return a placeholder that indicates this needs proper implementation
            var assembly = typeof(SmallMindEngine).Assembly;
            var version = assembly.GetName().Version;
            return $"dev-{version?.ToString() ?? "1.0.0"}";
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(ModelHandle));
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            // TransformerModel doesn't implement IDisposable currently
            // In production, release any native resources here
            _disposed = true;
        }
    }
}
