using System.Text;
using System.Text.Json;

namespace SmallMind.Core
{
    /// <summary>
    /// High-performance binary checkpoint store with version control.
    /// Format: Magic header + version + JSON metadata + binary tensors.
    /// </summary>
    internal class BinaryCheckpointStore : ICheckpointStore
    {
        private const string MagicHeader = "SMND"; // SmallMiND
        private const int CurrentFormatVersion = 1;
        private const int HeaderSize = 16; // Magic(4) + Version(4) + Reserved(8)

        /// <summary>
        /// Save a model checkpoint in binary format.
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

            await using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true);
            await using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);

            // Write header
            WriteHeader(writer);

            // Serialize metadata to JSON
            var metadataJson = JsonSerializer.Serialize(checkpoint.Metadata, new JsonSerializerOptions
            {
                WriteIndented = false
            });
            var metadataBytes = Encoding.UTF8.GetBytes(metadataJson);

            // Write metadata length and data
            writer.Write(metadataBytes.Length);
            writer.Write(metadataBytes);

            // Write number of parameters
            writer.Write(checkpoint.Parameters.Count);

            // Write each parameter
            foreach (var param in checkpoint.Parameters)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Write shape
                writer.Write(param.Shape.Length);
                foreach (var dim in param.Shape)
                {
                    writer.Write(dim);
                }

                // Write data
                writer.Write(param.Data.Length);

                // Write float data efficiently
                var byteCount = param.Data.Length * sizeof(float);
                var bytes = new byte[byteCount];
                Buffer.BlockCopy(param.Data, 0, bytes, 0, byteCount);

                // Ensure little-endian
                if (!BitConverter.IsLittleEndian)
                {
                    for (int i = 0; i < param.Data.Length; i++)
                    {
                        var offset = i * sizeof(float);
                        Array.Reverse(bytes, offset, sizeof(float));
                    }
                }

                await stream.WriteAsync(bytes, 0, byteCount, cancellationToken);
            }

            await stream.FlushAsync(cancellationToken);
        }

        /// <summary>
        /// Load a model checkpoint from binary format.
        /// </summary>
        public async Task<ModelCheckpoint> LoadAsync(
            string path,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("Path cannot be null or empty", nameof(path));
            if (!File.Exists(path))
                throw new FileNotFoundException($"Checkpoint file not found: {path}", path);

            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

            // Read and validate header
            var version = ReadAndValidateHeader(reader);

            // Read metadata
            var metadataLength = reader.ReadInt32();
            if (metadataLength < 0 || metadataLength > 1024 * 1024) // Max 1MB metadata
                throw new InvalidDataException($"Invalid metadata length: {metadataLength}");

            var metadataBytes = reader.ReadBytes(metadataLength);
            var metadataJson = Encoding.UTF8.GetString(metadataBytes);
            var metadata = JsonSerializer.Deserialize<ModelMetadata>(metadataJson)
                ?? throw new InvalidDataException("Failed to deserialize metadata");

            // Read parameters
            var numParameters = reader.ReadInt32();
            if (numParameters < 0 || numParameters > 10000) // Sanity check
                throw new InvalidDataException($"Invalid number of parameters: {numParameters}");

            var checkpoint = new ModelCheckpoint
            {
                FormatVersion = version,
                Metadata = metadata
            };

            for (int i = 0; i < numParameters; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Read shape
                var shapeRank = reader.ReadInt32();
                if (shapeRank < 0 || shapeRank > 8) // Max 8 dimensions
                    throw new InvalidDataException($"Invalid shape rank: {shapeRank}");

                var shape = new int[shapeRank];
                for (int j = 0; j < shapeRank; j++)
                {
                    shape[j] = reader.ReadInt32();
                    if (shape[j] <= 0)
                        throw new InvalidDataException($"Invalid shape dimension: {shape[j]}");
                }

                // Read data
                var dataLength = reader.ReadInt32();
                if (dataLength <= 0 || dataLength > 100_000_000) // Max ~400MB per tensor
                    throw new InvalidDataException($"Invalid data length: {dataLength}");

                var data = new float[dataLength];
                var byteCount = dataLength * sizeof(float);
                var bytes = new byte[byteCount];

                await stream.ReadExactlyAsync(bytes, 0, byteCount, cancellationToken);

                // Convert bytes to floats
                Buffer.BlockCopy(bytes, 0, data, 0, byteCount);

                // Handle endianness
                if (!BitConverter.IsLittleEndian)
                {
                    for (int j = 0; j < dataLength; j++)
                    {
                        var floatBytes = BitConverter.GetBytes(data[j]);
                        Array.Reverse(floatBytes);
                        data[j] = BitConverter.ToSingle(floatBytes, 0);
                    }
                }

                checkpoint.Parameters.Add(new TensorData
                {
                    Shape = shape,
                    Data = data
                });
            }

            return checkpoint;
        }

        private void WriteHeader(BinaryWriter writer)
        {
            // Magic header
            writer.Write(Encoding.ASCII.GetBytes(MagicHeader));

            // Format version
            writer.Write(CurrentFormatVersion);

            // Reserved bytes for future use
            writer.Write(0L); // 8 bytes
        }

        private int ReadAndValidateHeader(BinaryReader reader)
        {
            // Read magic
            var magic = new string(reader.ReadChars(4));
            if (magic != MagicHeader)
            {
                throw new InvalidDataException(
                    $"Invalid checkpoint file: bad magic header '{magic}'. Expected '{MagicHeader}'. " +
                    "This may not be a SmallMind binary checkpoint.");
            }

            // Read version
            var version = reader.ReadInt32();
            if (version > CurrentFormatVersion)
            {
                throw new InvalidDataException(
                    $"Checkpoint format version {version} is newer than supported version {CurrentFormatVersion}. " +
                    "Please upgrade the SmallMind library.");
            }
            if (version < 1)
            {
                throw new InvalidDataException($"Invalid checkpoint format version: {version}");
            }

            // Skip reserved bytes
            reader.ReadInt64();

            return version;
        }
    }
}
