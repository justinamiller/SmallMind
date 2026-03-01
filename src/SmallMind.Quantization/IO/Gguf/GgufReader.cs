using System.Text;

namespace SmallMind.Quantization.IO.Gguf
{
    /// <summary>
    /// Reads GGUF (GPT-Generated Unified Format) model files.
    /// Supports GGUF versions 2 and 3.
    /// </summary>
    internal sealed class GgufReader : ITensorDataReader, IDisposable
    {
        private const string ExpectedMagic = "GGUF";
        private const uint SupportedVersionMin = 2;
        private const uint SupportedVersionMax = 3;
        private const uint DefaultAlignment = 32;

        private readonly Stream _stream;
        private readonly BinaryReader _reader;
        private readonly bool _leaveOpen;

        /// <summary>
        /// Creates a new GGUF reader.
        /// </summary>
        /// <param name="stream">Input stream (must be readable and seekable).</param>
        /// <param name="leaveOpen">If true, keeps stream open after disposal.</param>
        public GgufReader(Stream stream, bool leaveOpen = false)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (!stream.CanRead) throw new ArgumentException("Stream must be readable", nameof(stream));
            if (!stream.CanSeek) throw new ArgumentException("Stream must be seekable", nameof(stream));

            _stream = stream;
            _reader = new BinaryReader(_stream, Encoding.UTF8, leaveOpen);
            _leaveOpen = leaveOpen;
        }

        /// <summary>
        /// Read GGUF model information from the stream.
        /// </summary>
        public GgufModelInfo ReadModelInfo()
        {
            _stream.Seek(0, SeekOrigin.Begin);

            var info = new GgufModelInfo();

            // Read and validate magic header
            byte[] magicBytes = _reader.ReadBytes(4);
            string magic = Encoding.ASCII.GetString(magicBytes);
            if (magic != ExpectedMagic)
                throw new InvalidDataException($"Invalid GGUF magic header: expected '{ExpectedMagic}', got '{magic}'");

            // Read version
            info.Version = _reader.ReadUInt32();
            if (info.Version < SupportedVersionMin || info.Version > SupportedVersionMax)
                throw new NotSupportedException($"Unsupported GGUF version: {info.Version} (supported: {SupportedVersionMin}-{SupportedVersionMax})");

            // Read counts
            ulong tensorCount = _reader.ReadUInt64();
            ulong metadataKvCount = _reader.ReadUInt64();

            // Read metadata KV pairs
            for (ulong i = 0; i < metadataKvCount; i++)
            {
                var kv = ReadKV();
                if (kv.Value != null)
                {
                    info.Metadata[kv.Key] = kv.Value;
                }
            }

            // Extract alignment from metadata if present (version 3)
            if (info.Metadata.TryGetValue("general.alignment", out var alignmentObj))
            {
                info.Alignment = Convert.ToUInt32(alignmentObj);
            }
            else
            {
                info.Alignment = DefaultAlignment;
            }

            // Read tensor infos
            for (ulong i = 0; i < tensorCount; i++)
            {
                var tensorInfo = ReadTensorInfo();
                info.Tensors.Add(tensorInfo);
            }

            // Calculate data offset (aligned)
            ulong currentOffset = (ulong)_stream.Position;
            ulong alignment = info.Alignment;
            ulong alignedOffset = (currentOffset + alignment - 1) / alignment * alignment;
            info.DataOffset = alignedOffset;

            // Resolve tensor offsets to absolute file positions.
            // GGUF tensors are stored at offsets RELATIVE to the start of the data section.
            // The writer (llama.cpp) aligns each tensor independently within the data section,
            // so the gaps between tensors may not equal CalculateTensorSize(prev).
            // Using the stored relative offsets (+ DataOffset base) is the only correct approach.
            foreach (var tensor in info.Tensors)
            {
                // tensor.Offset currently holds the relative offset (read from file); convert to absolute.
                tensor.Offset = info.DataOffset + tensor.Offset;
                tensor.Size = CalculateTensorSize(tensor.Type, tensor.Dimensions);
            }

            return info;
        }

        /// <summary>
        /// Read a key-value metadata entry.
        /// </summary>
        private GgufKV ReadKV()
        {
            var kv = new GgufKV();

            // Read key (GGUF string format)
            kv.Key = ReadGgufString();

            // Read type
            kv.Type = (GgufValueType)_reader.ReadUInt32();

            // Read value based on type
            kv.Value = ReadValue(kv.Type);

            return kv;
        }

        /// <summary>
        /// Read a value based on its type.
        /// </summary>
        private object? ReadValue(GgufValueType type)
        {
            return type switch
            {
                GgufValueType.UInt8 => _reader.ReadByte(),
                GgufValueType.Int8 => _reader.ReadSByte(),
                GgufValueType.UInt16 => _reader.ReadUInt16(),
                GgufValueType.Int16 => _reader.ReadInt16(),
                GgufValueType.UInt32 => _reader.ReadUInt32(),
                GgufValueType.Int32 => _reader.ReadInt32(),
                GgufValueType.Float32 => _reader.ReadSingle(),
                GgufValueType.Bool => _reader.ReadByte() != 0,
                GgufValueType.String => ReadGgufString(),
                GgufValueType.Array => ReadArray(),
                GgufValueType.UInt64 => _reader.ReadUInt64(),
                GgufValueType.Int64 => _reader.ReadInt64(),
                GgufValueType.Float64 => _reader.ReadDouble(),
                _ => throw new NotSupportedException($"Unsupported GGUF value type: {type}")
            };
        }

        /// <summary>
        /// Read an array value (array type + count + elements).
        /// </summary>
        private object ReadArray()
        {
            var elementType = (GgufValueType)_reader.ReadUInt32();
            ulong count = _reader.ReadUInt64();

            // Read elements into appropriate array type
            switch (elementType)
            {
                case GgufValueType.UInt8:
                    {
                        var arr = new byte[count];
                        for (ulong i = 0; i < count; i++)
                            arr[i] = _reader.ReadByte();
                        return arr;
                    }
                case GgufValueType.Int8:
                    {
                        var arr = new sbyte[count];
                        for (ulong i = 0; i < count; i++)
                            arr[i] = _reader.ReadSByte();
                        return arr;
                    }
                case GgufValueType.UInt16:
                    {
                        var arr = new ushort[count];
                        for (ulong i = 0; i < count; i++)
                            arr[i] = _reader.ReadUInt16();
                        return arr;
                    }
                case GgufValueType.Int16:
                    {
                        var arr = new short[count];
                        for (ulong i = 0; i < count; i++)
                            arr[i] = _reader.ReadInt16();
                        return arr;
                    }
                case GgufValueType.UInt32:
                    {
                        var arr = new uint[count];
                        for (ulong i = 0; i < count; i++)
                            arr[i] = _reader.ReadUInt32();
                        return arr;
                    }
                case GgufValueType.Int32:
                    {
                        var arr = new int[count];
                        for (ulong i = 0; i < count; i++)
                            arr[i] = _reader.ReadInt32();
                        return arr;
                    }
                case GgufValueType.Float32:
                    {
                        var arr = new float[count];
                        for (ulong i = 0; i < count; i++)
                            arr[i] = _reader.ReadSingle();
                        return arr;
                    }
                case GgufValueType.Float64:
                    {
                        var arr = new double[count];
                        for (ulong i = 0; i < count; i++)
                            arr[i] = _reader.ReadDouble();
                        return arr;
                    }
                case GgufValueType.Bool:
                    {
                        var arr = new bool[count];
                        for (ulong i = 0; i < count; i++)
                            arr[i] = _reader.ReadByte() != 0;
                        return arr;
                    }
                case GgufValueType.String:
                    {
                        var arr = new string[count];
                        for (ulong i = 0; i < count; i++)
                            arr[i] = ReadGgufString();
                        return arr;
                    }
                case GgufValueType.UInt64:
                    {
                        var arr = new ulong[count];
                        for (ulong i = 0; i < count; i++)
                            arr[i] = _reader.ReadUInt64();
                        return arr;
                    }
                case GgufValueType.Int64:
                    {
                        var arr = new long[count];
                        for (ulong i = 0; i < count; i++)
                            arr[i] = _reader.ReadInt64();
                        return arr;
                    }
                default:
                    throw new NotSupportedException($"Unsupported array element type: {elementType}");
            }
        }

        /// <summary>
        /// Read a GGUF string (uint64 length prefix + UTF-8 bytes, NOT null-terminated).
        /// </summary>
        private string ReadGgufString()
        {
            ulong length = _reader.ReadUInt64();
            if (length == 0)
                return string.Empty;

            if (length > int.MaxValue)
                throw new InvalidDataException($"String length too large: {length}");

            byte[] bytes = _reader.ReadBytes((int)length);
            return Encoding.UTF8.GetString(bytes);
        }

        /// <summary>
        /// Read tensor metadata.
        /// </summary>
        private GgufTensorInfo ReadTensorInfo()
        {
            var info = new GgufTensorInfo();

            // Read name
            info.Name = ReadGgufString();

            // Read number of dimensions
            uint nDims = _reader.ReadUInt32();

            // Read dimensions
            info.Dimensions = new ulong[nDims];
            for (uint i = 0; i < nDims; i++)
            {
                info.Dimensions[i] = _reader.ReadUInt64();
            }

            // Read tensor type
            info.Type = (GgufTensorType)_reader.ReadUInt32();

            // Read offset (this is relative offset, will be adjusted later)
            ulong offsetInDataSection = _reader.ReadUInt64();
            // We'll calculate absolute offset later in ReadModelInfo
            info.Offset = offsetInDataSection;

            return info;
        }

        /// <summary>
        /// Calculate tensor data size in bytes based on type and dimensions.
        /// Block sizes and layouts match the GGUF / ggml specification used by llama.cpp.
        /// See: https://github.com/ggerganov/ggml/blob/master/docs/gguf.md
        /// </summary>
        private ulong CalculateTensorSize(GgufTensorType type, ulong[] dimensions)
        {
            // Calculate total elements
            ulong totalElements = 1;
            foreach (var dim in dimensions)
            {
                totalElements *= dim;
            }

            // Get bytes per element (or calculate based on block structure)
            return type switch
            {
                GgufTensorType.F32 => totalElements * 4,
                GgufTensorType.F16 => totalElements * 2,
                GgufTensorType.Q4_0 => CalculateQ4_0Size(totalElements),
                GgufTensorType.Q4_1 => CalculateQ4_1Size(totalElements),
                GgufTensorType.Q5_0 => CalculateQ5_0Size(totalElements),
                GgufTensorType.Q8_0 => CalculateQ8_0Size(totalElements),
                GgufTensorType.Q8_1 => CalculateQ8_1Size(totalElements),
                GgufTensorType.Q2_K => CalculateQ2KSize(totalElements),
                GgufTensorType.Q3_K => CalculateQ3KSize(totalElements),
                GgufTensorType.Q4_K => CalculateQ4KSize(totalElements),
                GgufTensorType.Q5_K => CalculateQ5KSize(totalElements),
                GgufTensorType.Q6_K => CalculateQ6KSize(totalElements),
                GgufTensorType.Q8_K => CalculateQ8KSize(totalElements),
                _ => throw new NotSupportedException($"Unsupported tensor type for size calculation: {type}")
            };
        }

        /// <summary>
        /// Calculate Q4_0 tensor size (block size = 32).
        /// Each block: 2 bytes (fp16 scale) + 16 bytes (32 x 4-bit values).
        /// </summary>
        private ulong CalculateQ4_0Size(ulong totalElements)
        {
            const int blockSize = 32;
            ulong numBlocks = (totalElements + blockSize - 1) / blockSize;
            return numBlocks * (2 + 16); // 2 bytes scale + 16 bytes data
        }

        /// <summary>
        /// Calculate Q4_1 tensor size (block size = 32).
        /// Each block: 2 bytes (fp16 scale) + 2 bytes (fp16 min) + 16 bytes (32 x 4-bit values).
        /// </summary>
        private ulong CalculateQ4_1Size(ulong totalElements)
        {
            const int blockSize = 32;
            ulong numBlocks = (totalElements + blockSize - 1) / blockSize;
            return numBlocks * (2 + 2 + 16); // 2 bytes scale + 2 bytes min + 16 bytes data
        }

        /// <summary>
        /// Calculate Q5_0 tensor size (block size = 32).
        /// Each block: 2 bytes (fp16 scale) + 4 bytes (32 x 1-bit high bits) + 16 bytes (32 x 4-bit low nibbles).
        /// Ref: block_q5_0 in ggml-quants.h
        /// </summary>
        private ulong CalculateQ5_0Size(ulong totalElements)
        {
            const int blockSize = 32;
            ulong numBlocks = (totalElements + blockSize - 1) / blockSize;
            return numBlocks * (2 + 4 + 16); // 2 bytes scale + 4 bytes high bits + 16 bytes low nibbles
        }

        /// <summary>
        /// Calculate Q8_0 tensor size (block size = 32).
        /// Each block: 2 bytes (fp16 scale) + 32 bytes (32 x int8 values).
        /// </summary>
        private ulong CalculateQ8_0Size(ulong totalElements)
        {
            const int blockSize = 32;
            ulong numBlocks = (totalElements + blockSize - 1) / blockSize;
            return numBlocks * (2 + 32); // 2 bytes scale + 32 bytes data
        }

        /// <summary>
        /// Calculate Q8_1 tensor size (block size = 32).
        /// Each block: 2 bytes (fp16 scale) + 2 bytes (fp16 min) + 32 bytes (32 x int8 values).
        /// </summary>
        private ulong CalculateQ8_1Size(ulong totalElements)
        {
            const int blockSize = 32;
            ulong numBlocks = (totalElements + blockSize - 1) / blockSize;
            return numBlocks * (2 + 2 + 32); // 2 bytes scale + 2 bytes min + 32 bytes data
        }

        /// <summary>
        /// Calculate Q2_K tensor size (super-block size = 256).
        /// Each super-block: 16 bytes (scales/mins, 4-bit each) + 64 bytes (256 x 2-bit quants)
        ///   + 2 bytes (fp16 super-block scale d) + 2 bytes (fp16 super-block min dmin) = 84 bytes.
        /// Ref: block_q2_K in ggml-quants.h
        /// </summary>
        private ulong CalculateQ2KSize(ulong totalElements)
        {
            const int blockSize = 256;
            ulong numBlocks = (totalElements + blockSize - 1) / blockSize;
            return numBlocks * 84; // 16 + 64 + 2 + 2 = 84 bytes per super-block
        }

        /// <summary>
        /// Calculate Q3_K tensor size (super-block size = 256).
        /// Each super-block: 32 bytes (high bits, 1 per value) + 64 bytes (low 2 bits, 4 per byte)
        ///   + 16 bytes (int8 scales) + 2 bytes (fp16 scale d) = 114 bytes.
        /// Ref: block_q3_K in ggml-quants.h
        /// </summary>
        private ulong CalculateQ3KSize(ulong totalElements)
        {
            const int blockSize = 256;
            ulong numBlocks = (totalElements + blockSize - 1) / blockSize;
            return numBlocks * 114; // 32 + 64 + 16 + 2 = 114 bytes per super-block
        }

        /// <summary>
        /// Calculate Q4_K tensor size (super-block size = 256).
        /// Each super-block: 2 bytes (fp16 scale d) + 2 bytes (fp16 min dmin)
        ///   + 12 bytes (6-bit scales packed) + 128 bytes (256 x 4-bit quants) = 144 bytes.
        /// Ref: block_q4_K in ggml-quants.h
        /// </summary>
        private ulong CalculateQ4KSize(ulong totalElements)
        {
            const int blockSize = 256;
            ulong numBlocks = (totalElements + blockSize - 1) / blockSize;
            return numBlocks * 144; // 2 + 2 + 12 + 128 = 144 bytes per super-block
        }

        /// <summary>
        /// Calculate Q5_K tensor size (super-block size = 256).
        /// Each super-block: 2 bytes (fp16 scale d) + 2 bytes (fp16 min dmin)
        ///   + 12 bytes (6-bit scales packed) + 32 bytes (256 x 1-bit high bits) + 128 bytes (256 x 4-bit low nibbles) = 176 bytes.
        /// Ref: block_q5_K in ggml-quants.h
        /// </summary>
        private ulong CalculateQ5KSize(ulong totalElements)
        {
            const int blockSize = 256;
            ulong numBlocks = (totalElements + blockSize - 1) / blockSize;
            return numBlocks * 176; // 2 + 2 + 12 + 32 + 128 = 176 bytes per super-block
        }

        /// <summary>
        /// Calculate Q6_K tensor size (super-block size = 256).
        /// Each super-block: 128 bytes (ql - low 4 bits of 256 values) + 64 bytes (qh - high 2 bits, 4 per byte)
        ///   + 16 bytes (int8 scales, one per 16-value sub-block) + 2 bytes (fp16 super-block scale d) = 210 bytes.
        /// Ref: block_q6_K in ggml-quants.h
        /// </summary>
        private ulong CalculateQ6KSize(ulong totalElements)
        {
            const int blockSize = 256;
            ulong numBlocks = (totalElements + blockSize - 1) / blockSize;
            return numBlocks * 210; // 128 + 64 + 16 + 2 = 210 bytes per super-block
        }

        /// <summary>
        /// Calculate Q8_K tensor size (super-block size = 256).
        /// Each super-block: 4 bytes (float32 scale d) + 256 bytes (256 x int8 quants)
        ///   + 32 bytes (int16 block sums, one per 16-value sub-block) = 292 bytes.
        /// Ref: block_q8_K in ggml-quants.h
        /// </summary>
        private ulong CalculateQ8KSize(ulong totalElements)
        {
            const int blockSize = 256;
            ulong numBlocks = (totalElements + blockSize - 1) / blockSize;
            return numBlocks * 292; // 4 + 256 + 32 = 292 bytes per super-block
        }

        /// <summary>
        /// Read tensor data at the specified offset.
        /// </summary>
        /// <param name="offset">Absolute offset in file.</param>
        /// <param name="size">Number of bytes to read.</param>
        /// <returns>Raw tensor data bytes.</returns>
        public byte[] ReadTensorData(ulong offset, ulong size)
        {
            if (size > int.MaxValue)
                throw new ArgumentException($"Tensor size too large: {size}");

            _stream.Seek((long)offset, SeekOrigin.Begin);
            return _reader.ReadBytes((int)size);
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
            GC.SuppressFinalize(this);
        }
    }
}
