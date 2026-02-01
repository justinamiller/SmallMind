using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using SmallMind.Quantization.Tensors;

namespace SmallMind.Quantization.IO.Smq
{
    /// <summary>
    /// Writes quantized tensors to SMQ binary format.
    /// </summary>
    public sealed class SmqWriter : IDisposable
    {
        private readonly Stream _stream;
        private readonly BinaryWriter _writer;
        private readonly bool _leaveOpen;

        public SmqWriter(Stream stream, bool leaveOpen = false)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (!stream.CanWrite) throw new ArgumentException("Stream must be writable", nameof(stream));

            _stream = stream;
            _writer = new BinaryWriter(_stream, Encoding.UTF8, leaveOpen);
            _leaveOpen = leaveOpen;
        }

        /// <summary>
        /// Write a complete SMQ model file.
        /// </summary>
        /// <param name="tensors">Dictionary of tensor name to tensor object (Q8Tensor or Q4Tensor).</param>
        /// <param name="metadata">Optional metadata dictionary.</param>
        /// <returns>Total bytes written.</returns>
        public long WriteModel(Dictionary<string, object> tensors, Dictionary<string, object>? metadata = null)
        {
            if (tensors == null) throw new ArgumentNullException(nameof(tensors));

            long startPosition = _stream.Position;

            // Prepare tensor entries
            var entries = new List<SmqFormat.TensorEntry>();
            foreach (var kvp in tensors)
            {
                var entry = CreateTensorEntry(kvp.Key, kvp.Value);
                entries.Add(entry);
            }

            // Serialize metadata to JSON
            byte[] metadataBytes;
            if (metadata != null && metadata.Count > 0)
            {
                var json = JsonSerializer.Serialize(metadata);
                metadataBytes = Encoding.UTF8.GetBytes(json);
            }
            else
            {
                metadataBytes = Array.Empty<byte>();
            }

            // Write header
            WriteHeader((uint)entries.Count, (uint)metadataBytes.Length);

            // Write metadata JSON
            if (metadataBytes.Length > 0)
            {
                _writer.Write(metadataBytes);
            }

            // Calculate offsets for tensor directory and data
            long currentOffset = _stream.Position;
            long tensorDirSize = CalculateTensorDirectorySize(entries.Count);
            long dataStartOffset = currentOffset + tensorDirSize;

            // Assign offsets to each tensor entry
            long currentDataOffset = dataStartOffset;
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                entry.DataOffset = (ulong)currentDataOffset;
                currentDataOffset += (long)entry.DataLength;

                if (entry.AuxLength > 0)
                {
                    entry.AuxOffset = (ulong)currentDataOffset;
                    currentDataOffset += (long)entry.AuxLength;
                }
            }

            // Write tensor directory
            WriteTensorDirectory(entries);

            // Write tensor data blobs
            foreach (var kvp in tensors)
            {
                WriteTensorData(kvp.Value);
            }

            long totalBytes = _stream.Position - startPosition;
            return totalBytes;
        }

        private void WriteHeader(uint tensorCount, uint metadataJsonLength)
        {
            // Magic header (8 bytes ASCII)
            byte[] magic = Encoding.ASCII.GetBytes(SmqFormat.MagicHeader);
            if (magic.Length != 8)
                throw new InvalidOperationException("Magic header must be 8 bytes");
            _writer.Write(magic);

            // Version (4 bytes)
            _writer.Write(SmqFormat.FormatVersion);

            // Header size (4 bytes) - constant 32
            _writer.Write((uint)SmqFormat.HeaderSize);

            // Tensor count (4 bytes)
            _writer.Write(tensorCount);

            // Metadata JSON length (4 bytes)
            _writer.Write(metadataJsonLength);

            // Reserved (8 bytes) - zeros
            _writer.Write(0UL);
        }

        private void WriteTensorDirectory(List<SmqFormat.TensorEntry> entries)
        {
            foreach (var entry in entries)
            {
                WriteTensorEntry(entry);
            }
        }

        private void WriteTensorEntry(SmqFormat.TensorEntry entry)
        {
            // Name (64 bytes, null-padded)
            byte[] nameBytes = new byte[64];
            byte[] nameUtf8 = Encoding.UTF8.GetBytes(entry.Name);
            if (nameUtf8.Length > 64)
                throw new ArgumentException($"Tensor name too long: {entry.Name}");
            Array.Copy(nameUtf8, nameBytes, nameUtf8.Length);
            _writer.Write(nameBytes);

            // Data type (4 bytes)
            _writer.Write((uint)entry.DataType);

            // Rank (4 bytes)
            _writer.Write(entry.Rank);

            // Dimensions (8 x 4 bytes = 32 bytes, zero-padded)
            for (int i = 0; i < 8; i++)
            {
                int dim = (i < entry.Dimensions.Length) ? entry.Dimensions[i] : 0;
                _writer.Write(dim);
            }

            // Block size (4 bytes)
            _writer.Write(entry.BlockSize);

            // Data offset (8 bytes)
            _writer.Write(entry.DataOffset);

            // Data length (8 bytes)
            _writer.Write(entry.DataLength);

            // Aux offset (8 bytes)
            _writer.Write(entry.AuxOffset);

            // Aux length (8 bytes)
            _writer.Write(entry.AuxLength);

            // Reserved (16 bytes)
            _writer.Write(0UL);
            _writer.Write(0UL);
        }

        private void WriteTensorData(object tensor)
        {
            if (tensor is Q8Tensor q8)
            {
                WriteQ8TensorData(q8);
            }
            else if (tensor is Q4Tensor q4)
            {
                WriteQ4TensorData(q4);
            }
            else
            {
                throw new ArgumentException($"Unsupported tensor type: {tensor.GetType()}");
            }
        }

        private void WriteQ8TensorData(Q8Tensor tensor)
        {
            // Write quantized data (sbyte array)
            for (int i = 0; i < tensor.Data.Length; i++)
            {
                _writer.Write(tensor.Data[i]);
            }

            // Write scales (float array)
            for (int i = 0; i < tensor.Scales.Length; i++)
            {
                _writer.Write(tensor.Scales[i]);
            }
        }

        private void WriteQ4TensorData(Q4Tensor tensor)
        {
            // Write packed quantized data (byte array)
            _writer.Write(tensor.Data);

            // Write scales (float array)
            for (int i = 0; i < tensor.Scales.Length; i++)
            {
                _writer.Write(tensor.Scales[i]);
            }
        }

        private SmqFormat.TensorEntry CreateTensorEntry(string name, object tensor)
        {
            if (tensor is Q8Tensor q8)
            {
                int totalElements = q8.Rows * q8.Cols;
                return new SmqFormat.TensorEntry
                {
                    Name = name,
                    DataType = QuantScheme.Q8_0,
                    Rank = 2,
                    Dimensions = new[] { q8.Rows, q8.Cols },
                    BlockSize = (uint)q8.BlockSize,
                    DataLength = (ulong)q8.Data.Length,
                    AuxLength = (ulong)(q8.Scales.Length * sizeof(float))
                };
            }
            else if (tensor is Q4Tensor q4)
            {
                int totalElements = q4.Rows * q4.Cols;
                return new SmqFormat.TensorEntry
                {
                    Name = name,
                    DataType = QuantScheme.Q4_0,
                    Rank = 2,
                    Dimensions = new[] { q4.Rows, q4.Cols },
                    BlockSize = (uint)q4.BlockSize,
                    DataLength = (ulong)q4.Data.Length,
                    AuxLength = (ulong)(q4.Scales.Length * sizeof(float))
                };
            }
            else
            {
                throw new ArgumentException($"Unsupported tensor type: {tensor.GetType()}");
            }
        }

        private long CalculateTensorDirectorySize(int tensorCount)
        {
            // Each entry: 64 (name) + 4 (dtype) + 4 (rank) + 32 (dims) + 4 (blocksize) + 8+8+8+8 (offsets/lengths) + 16 (reserved) = 156 bytes
            const int entrySize = 156;
            return tensorCount * entrySize;
        }

        public void Dispose()
        {
            if (!_leaveOpen)
            {
                _writer?.Dispose();
            }
        }
    }
}
