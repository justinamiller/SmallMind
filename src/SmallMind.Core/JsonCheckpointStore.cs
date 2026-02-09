using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SmallMind.Core
{
    /// <summary>
    /// Legacy JSON checkpoint store for backward compatibility.
    /// Loads old JSON checkpoints but saves in binary format by default.
    /// </summary>
    internal class JsonCheckpointStore : ICheckpointStore
    {
        /// <summary>
        /// Save checkpoint in JSON format (legacy).
        /// For production use, prefer BinaryCheckpointStore.
        /// </summary>
        public async Task SaveAsync(
            ModelCheckpoint checkpoint,
            string path,
            CancellationToken cancellationToken = default)
        {
            if (checkpoint == null)
                throw new ArgumentNullException(nameof(checkpoint));
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("Path cannot be null or empty", nameof(path));

            // Ensure directory exists
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Convert to legacy format
            var legacyCheckpoint = new Dictionary<string, object>
            {
                ["formatVersion"] = checkpoint.FormatVersion,
                ["metadata"] = checkpoint.Metadata,
                ["parameters"] = new List<object>()
            };

            foreach (var param in checkpoint.Parameters)
            {
                var paramData = new Dictionary<string, object>
                {
                    ["shape"] = param.Shape,
                    ["data"] = param.Data
                };
                ((List<object>)legacyCheckpoint["parameters"]).Add(paramData);
            }

            var json = JsonSerializer.Serialize(legacyCheckpoint, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
            
            await File.WriteAllTextAsync(path, json, cancellationToken);
        }

        /// <summary>
        /// Load checkpoint from JSON format (legacy).
        /// </summary>
        public async Task<ModelCheckpoint> LoadAsync(
            string path,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("Path cannot be null or empty", nameof(path));
            if (!File.Exists(path))
                throw new FileNotFoundException($"Checkpoint file not found: {path}", path);

            var json = await File.ReadAllTextAsync(path, cancellationToken);
            var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);

            if (data == null)
                throw new InvalidDataException("Failed to parse checkpoint JSON");

            var checkpoint = new ModelCheckpoint();

            // Try to read format version (may not exist in old checkpoints)
            if (data.ContainsKey("formatVersion"))
            {
                checkpoint.FormatVersion = data["formatVersion"].GetInt32();
            }
            else
            {
                checkpoint.FormatVersion = 0; // Legacy format
            }

            // Try to read metadata (may not exist in old checkpoints)
            if (data.ContainsKey("metadata"))
            {
                checkpoint.Metadata = JsonSerializer.Deserialize<ModelMetadata>(
                    data["metadata"].GetRawText()) ?? new ModelMetadata();
            }

            // Read parameters
            if (!data.ContainsKey("parameters"))
                throw new InvalidDataException("Checkpoint missing 'parameters' field");

            var parametersJson = data["parameters"].GetRawText();
            var parameters = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(parametersJson);

            if (parameters == null)
                throw new InvalidDataException("Failed to parse parameters");

            foreach (var param in parameters)
            {
                if (!param.ContainsKey("shape") || !param.ContainsKey("data"))
                    throw new InvalidDataException("Parameter missing 'shape' or 'data' field");

                var shape = JsonSerializer.Deserialize<int[]>(param["shape"].GetRawText()) 
                    ?? Array.Empty<int>();
                var floatData = JsonSerializer.Deserialize<float[]>(param["data"].GetRawText()) 
                    ?? Array.Empty<float>();

                checkpoint.Parameters.Add(new TensorData
                {
                    Shape = shape,
                    Data = floatData
                });
            }

            return checkpoint;
        }
    }
}
