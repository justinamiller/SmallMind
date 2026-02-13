namespace SmallMind.Core
{
    /// <summary>
    /// Metadata describing the model architecture and configuration.
    /// </summary>
    internal sealed class ModelMetadata
    {
        /// <summary>
        /// Type of model (e.g., "TransformerModel").
        /// </summary>
        public string ModelType { get; set; } = string.Empty;

        /// <summary>
        /// Size of the vocabulary (number of unique tokens).
        /// </summary>
        public int VocabSize { get; set; }

        /// <summary>
        /// Maximum sequence length (context window).
        /// </summary>
        public int BlockSize { get; set; }

        /// <summary>
        /// Embedding dimension for tokens and positions.
        /// </summary>
        public int EmbedDim { get; set; }

        /// <summary>
        /// Number of attention heads in each layer.
        /// </summary>
        public int NumHeads { get; set; }

        /// <summary>
        /// Number of transformer layers.
        /// </summary>
        public int NumLayers { get; set; }

        /// <summary>
        /// Hidden dimension in feed-forward network.
        /// </summary>
        public int FfnHiddenDim { get; set; }

        /// <summary>
        /// Additional metadata as key-value pairs.
        /// </summary>
        public Dictionary<string, object> Extra { get; set; } = new();
    }

    /// <summary>
    /// Data for a single tensor parameter.
    /// </summary>
    internal sealed class TensorData
    {
        /// <summary>
        /// Shape of the tensor (dimensions).
        /// </summary>
        public int[] Shape { get; set; } = Array.Empty<int>();

        /// <summary>
        /// Flat array of tensor data.
        /// </summary>
        public float[] Data { get; set; } = Array.Empty<float>();
    }

    /// <summary>
    /// Model checkpoint containing metadata and all parameters.
    /// </summary>
    internal sealed class ModelCheckpoint
    {
        /// <summary>
        /// Checkpoint format version (for backward compatibility).
        /// </summary>
        public int FormatVersion { get; set; }

        /// <summary>
        /// Model architecture metadata.
        /// </summary>
        public ModelMetadata Metadata { get; set; } = new();

        /// <summary>
        /// List of all model parameters (tensors).
        /// </summary>
        public List<TensorData> Parameters { get; set; } = new();
    }

    /// <summary>
    /// Interface for saving and loading model checkpoints.
    /// </summary>
    internal interface ICheckpointStore
    {
        /// <summary>
        /// Save a model checkpoint to a file.
        /// </summary>
        /// <param name="checkpoint">Checkpoint data to save</param>
        /// <param name="path">File path to save to</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task SaveAsync(
            ModelCheckpoint checkpoint,
            string path,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Load a model checkpoint from a file.
        /// </summary>
        /// <param name="path">File path to load from</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Loaded checkpoint data</returns>
        Task<ModelCheckpoint> LoadAsync(
            string path,
            CancellationToken cancellationToken = default);
    }
}
