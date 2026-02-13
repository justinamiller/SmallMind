using SmallMind.Core;

namespace SmallMind.Transformers
{
    /// <summary>
    /// Extension methods for converting between TransformerModel and ModelCheckpoint.
    /// </summary>
    internal static class CheckpointExtensions
    {
        /// <summary>
        /// Convert a TransformerModel to a ModelCheckpoint for saving.
        /// </summary>
        public static ModelCheckpoint ToCheckpoint(this TransformerModel model)
        {
            var checkpoint = new ModelCheckpoint
            {
                FormatVersion = 1,
                Metadata = new ModelMetadata
                {
                    ModelType = "TransformerModel",
                    VocabSize = model.VocabSize,
                    BlockSize = model.BlockSize,
                    EmbedDim = model.EmbedDim,
                    NumHeads = model.NumHeads,
                    NumLayers = model.NumLayers,
                    FfnHiddenDim = model.EmbedDim * 4 // Standard 4x expansion
                }
            };

            foreach (var param in model.Parameters)
            {
                var shape = new int[param.Shape.Length];
                Array.Copy(param.Shape, shape, param.Shape.Length);

                var data = new float[param.Data.Length];
                Array.Copy(param.Data, data, param.Data.Length);

                checkpoint.Parameters.Add(new TensorData
                {
                    Shape = shape,
                    Data = data
                });
            }

            return checkpoint;
        }

        /// <summary>
        /// Load parameters from a ModelCheckpoint into a TransformerModel.
        /// </summary>
        public static void LoadFromCheckpoint(this TransformerModel model, ModelCheckpoint checkpoint)
        {
            if (checkpoint.Parameters.Count != model.Parameters.Count)
            {
                throw new InvalidOperationException(
                    $"Checkpoint has {checkpoint.Parameters.Count} parameters, " +
                    $"but model expects {model.Parameters.Count}");
            }

            for (int i = 0; i < checkpoint.Parameters.Count; i++)
            {
                var checkpointParam = checkpoint.Parameters[i];
                var modelParam = model.Parameters[i];

                // Validate shape matches
                if (checkpointParam.Data.Length != modelParam.Size)
                {
                    throw new InvalidOperationException(
                        $"Parameter {i}: checkpoint has {checkpointParam.Data.Length} elements, " +
                        $"but model expects {modelParam.Size}");
                }

                // Copy data
                Array.Copy(checkpointParam.Data, modelParam.Data, checkpointParam.Data.Length);
            }
        }

        /// <summary>
        /// Create a new TransformerModel from a ModelCheckpoint.
        /// </summary>
        public static TransformerModel FromCheckpoint(ModelCheckpoint checkpoint, double dropout = 0.1, int seed = 42)
        {
            var metadata = checkpoint.Metadata;

            if (string.IsNullOrEmpty(metadata.ModelType) || metadata.ModelType != "TransformerModel")
            {
                throw new InvalidOperationException(
                    $"Checkpoint is not for a TransformerModel (got '{metadata.ModelType}')");
            }

            // Create model with metadata parameters
            var model = new TransformerModel(
                vocabSize: metadata.VocabSize,
                blockSize: metadata.BlockSize,
                nEmbd: metadata.EmbedDim,
                nLayer: metadata.NumLayers,
                nHead: metadata.NumHeads,
                dropout: dropout,
                seed: seed
            );

            // Load parameters
            model.LoadFromCheckpoint(checkpoint);

            return model;
        }
    }
}
