using System;
using System.IO;
using System.Threading.Tasks;
using SmallMind.Transformers;
using SmallMind.Quantization.IO.Smq;
using SmallMind.Quantization.Abstractions;
using SmallMind.Core;

namespace SmallMind.Runtime.Quantization
{
    /// <summary>
    /// Loads quantized models from SMQ format and integrates with inference runtime.
    /// Handles both FP32 checkpoints and quantized SMQ models.
    /// </summary>
    public sealed class QuantizedModelLoader
    {
        /// <summary>
        /// Load a model from either FP32 checkpoint or SMQ format.
        /// Automatically detects format based on file extension.
        /// </summary>
        /// <param name="path">Path to model file (.json or .smq).</param>
        /// <param name="dropout">Dropout rate (only for FP32 models).</param>
        /// <param name="seed">Random seed.</param>
        /// <returns>Loaded TransformerModel.</returns>
        public static TransformerModel LoadModel(string path, double dropout = 0.1, int seed = 42)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException(nameof(path));
            if (!File.Exists(path))
                throw new FileNotFoundException($"Model file not found: {path}");

            var ext = Path.GetExtension(path).ToLowerInvariant();

            return ext switch
            {
                ".smq" => throw new NotSupportedException(
                    "SMQ quantized model loading requires QuantizedInferenceEngine. " +
                    "Use LoadQuantizedModelMetadata() to inspect SMQ files."),
                ".json" => LoadFP32Checkpoint(path, dropout, seed),
                _ => throw new NotSupportedException($"Unsupported model format: {ext}")
            };
        }

        /// <summary>
        /// Load metadata from an SMQ quantized model without loading weights.
        /// Useful for inspecting model structure.
        /// </summary>
        /// <param name="smqPath">Path to SMQ model file.</param>
        /// <returns>Model metadata dictionary.</returns>
        public static SmqModelInfo LoadQuantizedModelMetadata(string smqPath)
        {
            if (string.IsNullOrEmpty(smqPath))
                throw new ArgumentNullException(nameof(smqPath));
            if (!File.Exists(smqPath))
                throw new FileNotFoundException($"SMQ file not found: {smqPath}");

            using var stream = File.OpenRead(smqPath);
            using var reader = new SmqReader(stream);
            
            reader.ReadHeader();
            var metadata = reader.GetMetadata();
            var tensorNames = reader.GetTensorNames();

            return new SmqModelInfo
            {
                TensorCount = tensorNames.Count(),
                Metadata = metadata,
                TensorNames = tensorNames.ToArray()
            };
        }

        /// <summary>
        /// Validate that an SMQ file is compatible with the expected model architecture.
        /// </summary>
        /// <param name="smqPath">Path to SMQ file.</param>
        /// <param name="expectedVocabSize">Expected vocabulary size.</param>
        /// <param name="expectedBlockSize">Expected context window size.</param>
        /// <param name="expectedEmbedDim">Expected embedding dimension.</param>
        /// <param name="expectedNumLayers">Expected number of layers.</param>
        /// <returns>True if compatible, false otherwise.</returns>
        public static bool ValidateSmqCompatibility(
            string smqPath,
            int expectedVocabSize,
            int expectedBlockSize,
            int expectedEmbedDim,
            int expectedNumLayers)
        {
            try
            {
                var info = LoadQuantizedModelMetadata(smqPath);

                // Check if metadata contains model dimensions
                if (info.Metadata == null)
                    return false;

                // Try to extract model dimensions from metadata
                if (!info.Metadata.TryGetValue("vocab_size", out var vocabObj) ||
                    !info.Metadata.TryGetValue("block_size", out var blockObj) ||
                    !info.Metadata.TryGetValue("embed_dim", out var embedObj) ||
                    !info.Metadata.TryGetValue("num_layers", out var layersObj))
                {
                    return false;
                }

                int vocabSize = Convert.ToInt32(vocabObj);
                int blockSize = Convert.ToInt32(blockObj);
                int embedDim = Convert.ToInt32(embedObj);
                int numLayers = Convert.ToInt32(layersObj);

                return vocabSize == expectedVocabSize &&
                       blockSize == expectedBlockSize &&
                       embedDim == expectedEmbedDim &&
                       numLayers == expectedNumLayers;
            }
            catch
            {
                return false;
            }
        }

        private static TransformerModel LoadFP32Checkpoint(string path, double dropout, int seed)
        {
            var store = new BinaryCheckpointStore();
            var checkpoint = store.LoadAsync(path).GetAwaiter().GetResult();
            return CheckpointExtensions.FromCheckpoint(checkpoint, dropout, seed);
        }
    }

    /// <summary>
    /// Metadata information about an SMQ quantized model.
    /// </summary>
    public sealed class SmqModelInfo
    {
        /// <summary>
        /// Number of tensors in the model.
        /// </summary>
        public int TensorCount { get; set; }

        /// <summary>
        /// Model metadata dictionary.
        /// </summary>
        public System.Collections.Generic.Dictionary<string, object>? Metadata { get; set; }

        /// <summary>
        /// Names of all tensors in the model.
        /// </summary>
        public string[] TensorNames { get; set; } = Array.Empty<string>();
    }
}
