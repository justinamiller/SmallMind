using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using SmallMind.Quantization.Tensors;

namespace SmallMind.Quantization.IO.Smq
{
    /// <summary>
    /// Reads quantized tensors from SMQ binary format.
    /// </summary>
    public sealed class SmqReader : IDisposable
    {
        private readonly Stream _stream;
        private readonly BinaryReader _reader;
        private readonly bool _leaveOpen;
        private readonly Dictionary<string, SmqFormat.TensorEntry> _tensorDirectory;
        private Dictionary<string, object>? _metadata;
        private bool _headerRead;

        /// <summary>
        /// Creates a new SMQ reader.
        /// </summary>
        /// <param name="stream">Input stream (must be readable and seekable).</param>
        /// <param name="leaveOpen">If true, keeps stream open after disposal.</param>
        public SmqReader(Stream stream, bool leaveOpen = false)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (!stream.CanRead) throw new ArgumentException("Stream must be readable", nameof(stream));
            if (!stream.CanSeek) throw new ArgumentException("Stream must be seekable", nameof(stream));

            _stream = stream;
            _reader = new BinaryReader(_stream, Encoding.UTF8, leaveOpen);
            _leaveOpen = leaveOpen;
            _tensorDirectory = new Dictionary<string, SmqFormat.TensorEntry>();
        }

        /// <summary>
        /// Read header and tensor directory. Call this before loading tensors.
        /// </summary>
        public void ReadHeader()
        {
            if (_headerRead)
                throw new InvalidOperationException("Header already read");

            _stream.Position = 0;

            // Read magic header
            byte[] magic = _reader.ReadBytes(8);
            string magicStr = Encoding.ASCII.GetString(magic);
            if (magicStr != SmqFormat.MagicHeader)
                throw new InvalidDataException($"Invalid magic header: expected '{SmqFormat.MagicHeader}', got '{magicStr}'");

            // Read version
            uint version = _reader.ReadUInt32();
            if (version != SmqFormat.FormatVersion)
                throw new InvalidDataException($"Unsupported format version: {version}");

            // Read header size
            uint headerSize = _reader.ReadUInt32();
            if (headerSize != SmqFormat.HeaderSize)
                throw new InvalidDataException($"Invalid header size: {headerSize}");

            // Read tensor count
            uint tensorCount = _reader.ReadUInt32();

            // Read metadata JSON length
            uint metadataJsonLength = _reader.ReadUInt32();

            // Read reserved (skip)
            _reader.ReadUInt64();

            // Read metadata JSON if present
            if (metadataJsonLength > 0)
            {
                byte[] metadataBytes = _reader.ReadBytes((int)metadataJsonLength);
                string metadataJson = Encoding.UTF8.GetString(metadataBytes);
                _metadata = JsonSerializer.Deserialize<Dictionary<string, object>>(metadataJson);
            }

            // Read tensor directory
            ReadTensorDirectory((int)tensorCount);

            _headerRead = true;
        }

        /// <summary>
        /// Get metadata dictionary (null if no metadata).
        /// </summary>
        public Dictionary<string, object>? GetMetadata()
        {
            EnsureHeaderRead();
            return _metadata;
        }

        /// <summary>
        /// Get list of tensor names in the file.
        /// </summary>
        public IEnumerable<string> GetTensorNames()
        {
            EnsureHeaderRead();
            return _tensorDirectory.Keys;
        }

        /// <summary>
        /// Load a tensor by name.
        /// </summary>
        public object LoadTensor(string name)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));
            EnsureHeaderRead();

            if (!_tensorDirectory.TryGetValue(name, out var entry))
                throw new ArgumentException($"Tensor not found: {name}");

            return LoadTensorFromEntry(entry);
        }

        /// <summary>
        /// Try to load a tensor by name. Returns false if not found.
        /// </summary>
        public bool TryLoadTensor(string name, out object? tensor)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));
            EnsureHeaderRead();

            if (!_tensorDirectory.TryGetValue(name, out var entry))
            {
                tensor = null;
                return false;
            }

            tensor = LoadTensorFromEntry(entry);
            return true;
        }

        /// <summary>
        /// Load all tensors into a dictionary.
        /// </summary>
        public Dictionary<string, object> LoadAllTensors()
        {
            EnsureHeaderRead();

            var tensors = new Dictionary<string, object>();
            foreach (var kvp in _tensorDirectory)
            {
                tensors[kvp.Key] = LoadTensorFromEntry(kvp.Value);
            }
            return tensors;
        }

        private void ReadTensorDirectory(int tensorCount)
        {
            _tensorDirectory.Clear();

            for (int i = 0; i < tensorCount; i++)
            {
                var entry = ReadTensorEntry();
                _tensorDirectory[entry.Name] = entry;
            }
        }

        private SmqFormat.TensorEntry ReadTensorEntry()
        {
            var entry = new SmqFormat.TensorEntry();

            // Name (64 bytes, null-padded)
            byte[] nameBytes = _reader.ReadBytes(64);
            int nullIndex = Array.IndexOf(nameBytes, (byte)0);
            int nameLen = (nullIndex >= 0) ? nullIndex : 64;
            entry.Name = Encoding.UTF8.GetString(nameBytes, 0, nameLen);

            // Data type (4 bytes)
            entry.DataType = (QuantScheme)_reader.ReadUInt32();

            // Rank (4 bytes)
            entry.Rank = _reader.ReadInt32();
            if (entry.Rank < 0 || entry.Rank > 8)
                throw new InvalidDataException($"Invalid rank: {entry.Rank}");

            // Dimensions (8 x 4 bytes = 32 bytes)
            entry.Dimensions = new int[entry.Rank];
            for (int i = 0; i < 8; i++)
            {
                int dim = _reader.ReadInt32();
                if (i < entry.Rank)
                {
                    if (dim <= 0)
                        throw new InvalidDataException($"Invalid dimension: {dim}");
                    entry.Dimensions[i] = dim;
                }
            }

            // Block size (4 bytes)
            entry.BlockSize = _reader.ReadUInt32();

            // Data offset (8 bytes)
            entry.DataOffset = _reader.ReadUInt64();

            // Data length (8 bytes)
            entry.DataLength = _reader.ReadUInt64();

            // Aux offset (8 bytes)
            entry.AuxOffset = _reader.ReadUInt64();

            // Aux length (8 bytes)
            entry.AuxLength = _reader.ReadUInt64();

            // Reserved (16 bytes) - skip
            _reader.ReadUInt64();
            _reader.ReadUInt64();

            return entry;
        }

        private object LoadTensorFromEntry(SmqFormat.TensorEntry entry)
        {
            if (entry.DataType == QuantScheme.Q8_0)
            {
                return LoadQ8Tensor(entry);
            }
            else if (entry.DataType == QuantScheme.Q4_0)
            {
                return LoadQ4Tensor(entry);
            }
            else
            {
                throw new NotSupportedException($"Unsupported data type: {entry.DataType}");
            }
        }

        private Q8Tensor LoadQ8Tensor(SmqFormat.TensorEntry entry)
        {
            if (entry.Rank != 2)
                throw new InvalidDataException($"Q8 tensor must be rank 2, got {entry.Rank}");

            int rows = entry.Dimensions[0];
            int cols = entry.Dimensions[1];
            int blockSize = (int)entry.BlockSize;
            int totalElements = rows * cols;

            // Validate data size
            ulong expectedDataSize = (ulong)totalElements;
            if (entry.DataLength != expectedDataSize)
                throw new InvalidDataException($"Data length mismatch: expected {expectedDataSize}, got {entry.DataLength}");

            // Validate aux size (scales)
            int expectedBlocks = (totalElements + blockSize - 1) / blockSize;
            ulong expectedAuxSize = (ulong)(expectedBlocks * sizeof(float));
            if (entry.AuxLength != expectedAuxSize)
                throw new InvalidDataException($"Aux length mismatch: expected {expectedAuxSize}, got {entry.AuxLength}");

            // Read quantized data
            _stream.Position = (long)entry.DataOffset;
            var data = new sbyte[totalElements];
            for (int i = 0; i < totalElements; i++)
            {
                data[i] = _reader.ReadSByte();
            }

            // Read scales
            _stream.Position = (long)entry.AuxOffset;
            var scales = new float[expectedBlocks];
            for (int i = 0; i < expectedBlocks; i++)
            {
                scales[i] = _reader.ReadSingle();
            }

            return new Q8Tensor(rows, cols, blockSize, data, scales);
        }

        private Q4Tensor LoadQ4Tensor(SmqFormat.TensorEntry entry)
        {
            if (entry.Rank != 2)
                throw new InvalidDataException($"Q4 tensor must be rank 2, got {entry.Rank}");

            int rows = entry.Dimensions[0];
            int cols = entry.Dimensions[1];
            int blockSize = (int)entry.BlockSize;
            int totalElements = rows * cols;

            // Validate data size (packed: 2 elements per byte)
            ulong expectedDataSize = (ulong)((totalElements + 1) / 2);
            if (entry.DataLength != expectedDataSize)
                throw new InvalidDataException($"Data length mismatch: expected {expectedDataSize}, got {entry.DataLength}");

            // Validate aux size (scales)
            int expectedBlocks = (totalElements + blockSize - 1) / blockSize;
            ulong expectedAuxSize = (ulong)(expectedBlocks * sizeof(float));
            if (entry.AuxLength != expectedAuxSize)
                throw new InvalidDataException($"Aux length mismatch: expected {expectedAuxSize}, got {entry.AuxLength}");

            // Read packed quantized data
            _stream.Position = (long)entry.DataOffset;
            var data = _reader.ReadBytes((int)expectedDataSize);

            // Read scales
            _stream.Position = (long)entry.AuxOffset;
            var scales = new float[expectedBlocks];
            for (int i = 0; i < expectedBlocks; i++)
            {
                scales[i] = _reader.ReadSingle();
            }

            return new Q4Tensor(rows, cols, blockSize, data, scales);
        }

        private void EnsureHeaderRead()
        {
            if (!_headerRead)
                throw new InvalidOperationException("Header not read. Call ReadHeader() first.");
        }

        /// <summary>
        /// Dispose resources.
        /// </summary>
        public void Dispose()
        {
            if (!_leaveOpen)
            {
                _reader?.Dispose();
            }
        }
    }
}
