using System.Text;
using SmallMind.Quantization.Tensors;

namespace SmallMind.Quantization.IO.Smq
{
    /// <summary>
    /// Validates SMQ file integrity without loading full tensor data.
    /// </summary>
    internal static class SmqValidator
    {
        /// <summary>
        /// Validation error information.
        /// </summary>
        internal class ValidationError
        {
            /// <summary>
            /// Error or warning message.
            /// </summary>
            public string Message { get; set; } = "";

            /// <summary>
            /// Associated tensor name (null if not tensor-specific).
            /// </summary>
            public string? TensorName { get; set; }

            /// <summary>
            /// Severity level of the validation issue.
            /// </summary>
            public ValidationSeverity Severity { get; set; }

            /// <summary>
            /// Returns formatted error string.
            /// </summary>
            public override string ToString()
            {
                string prefix = Severity == ValidationSeverity.Error ? "ERROR" : "WARNING";
                string tensor = TensorName != null ? $" [Tensor: {TensorName}]" : "";
                return $"{prefix}{tensor}: {Message}";
            }
        }

        /// <summary>
        /// Severity level for validation issues.
        /// </summary>
        internal enum ValidationSeverity
        {
            /// <summary>
            /// Warning: non-critical issue.
            /// </summary>
            Warning,

            /// <summary>
            /// Error: critical issue that prevents file from being read.
            /// </summary>
            Error
        }

        /// <summary>
        /// Validate an SMQ file stream.
        /// </summary>
        /// <param name="stream">Stream to validate (must be readable and seekable).</param>
        /// <returns>List of validation errors (empty if valid).</returns>
        public static List<ValidationError> Validate(Stream stream)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (!stream.CanRead) throw new ArgumentException("Stream must be readable", nameof(stream));
            if (!stream.CanSeek) throw new ArgumentException("Stream must be seekable", nameof(stream));

            var errors = new List<ValidationError>();
            long fileSize = stream.Length;

            stream.Position = 0;

            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

            // Validate header
            if (!ValidateHeader(reader, fileSize, errors, out uint tensorCount, out uint metadataJsonLength))
            {
                return errors; // Fatal error, can't continue
            }

            // Skip metadata JSON
            long expectedDirStart = SmqFormat.HeaderSize + metadataJsonLength;
            if (expectedDirStart > fileSize)
            {
                errors.Add(new ValidationError
                {
                    Message = $"Metadata extends beyond file: metadata end at {expectedDirStart}, file size {fileSize}",
                    Severity = ValidationSeverity.Error
                });
                return errors;
            }

            stream.Position = expectedDirStart;

            // Read and validate tensor directory
            var entries = new List<SmqFormat.TensorEntry>();
            for (int i = 0; i < tensorCount; i++)
            {
                if (stream.Position + 156 > fileSize)
                {
                    errors.Add(new ValidationError
                    {
                        Message = $"Tensor directory entry {i} truncated",
                        Severity = ValidationSeverity.Error
                    });
                    return errors;
                }

                var entry = ReadTensorEntry(reader);
                entries.Add(entry);
                ValidateTensorEntry(entry, i, fileSize, errors);
            }

            // Validate no overlapping data regions
            ValidateNoOverlaps(entries, errors);

            return errors;
        }

        private static bool ValidateHeader(BinaryReader reader, long fileSize, List<ValidationError> errors, out uint tensorCount, out uint metadataJsonLength)
        {
            tensorCount = 0;
            metadataJsonLength = 0;

            if (fileSize < SmqFormat.HeaderSize)
            {
                errors.Add(new ValidationError
                {
                    Message = $"File too small: {fileSize} bytes < header size {SmqFormat.HeaderSize}",
                    Severity = ValidationSeverity.Error
                });
                return false;
            }

            // Read magic header
            byte[] magic = reader.ReadBytes(8);
            string magicStr = Encoding.ASCII.GetString(magic);
            if (magicStr != SmqFormat.MagicHeader)
            {
                errors.Add(new ValidationError
                {
                    Message = $"Invalid magic header: expected '{SmqFormat.MagicHeader}', got '{magicStr}'",
                    Severity = ValidationSeverity.Error
                });
                return false;
            }

            // Read version
            uint version = reader.ReadUInt32();
            if (version != SmqFormat.FormatVersion)
            {
                errors.Add(new ValidationError
                {
                    Message = $"Unsupported format version: {version} (expected {SmqFormat.FormatVersion})",
                    Severity = ValidationSeverity.Error
                });
                return false;
            }

            // Read header size
            uint headerSize = reader.ReadUInt32();
            if (headerSize != SmqFormat.HeaderSize)
            {
                errors.Add(new ValidationError
                {
                    Message = $"Invalid header size: {headerSize} (expected {SmqFormat.HeaderSize})",
                    Severity = ValidationSeverity.Error
                });
            }

            // Read tensor count
            tensorCount = reader.ReadUInt32();
            if (tensorCount > 100000)
            {
                errors.Add(new ValidationError
                {
                    Message = $"Suspicious tensor count: {tensorCount}",
                    Severity = ValidationSeverity.Warning
                });
            }

            // Read metadata JSON length
            metadataJsonLength = reader.ReadUInt32();
            if (metadataJsonLength > 10 * 1024 * 1024)
            {
                errors.Add(new ValidationError
                {
                    Message = $"Suspicious metadata size: {metadataJsonLength} bytes",
                    Severity = ValidationSeverity.Warning
                });
            }

            // Skip reserved
            reader.ReadUInt64();

            return true;
        }

        private static SmqFormat.TensorEntry ReadTensorEntry(BinaryReader reader)
        {
            var entry = new SmqFormat.TensorEntry();

            // Name (64 bytes)
            byte[] nameBytes = reader.ReadBytes(64);
            int nullIndex = Array.IndexOf(nameBytes, (byte)0);
            int nameLen = (nullIndex >= 0) ? nullIndex : 64;
            entry.Name = Encoding.UTF8.GetString(nameBytes, 0, nameLen);

            // Data type (4 bytes)
            entry.DataType = (QuantScheme)reader.ReadUInt32();

            // Rank (4 bytes)
            entry.Rank = reader.ReadInt32();

            // Dimensions (8 x 4 bytes)
            var dims = new List<int>();
            for (int i = 0; i < 8; i++)
            {
                int dim = reader.ReadInt32();
                if (i < entry.Rank)
                    dims.Add(dim);
            }
            entry.Dimensions = dims.ToArray();

            // Block size (4 bytes)
            entry.BlockSize = reader.ReadUInt32();

            // Data offset (8 bytes)
            entry.DataOffset = reader.ReadUInt64();

            // Data length (8 bytes)
            entry.DataLength = reader.ReadUInt64();

            // Aux offset (8 bytes)
            entry.AuxOffset = reader.ReadUInt64();

            // Aux length (8 bytes)
            entry.AuxLength = reader.ReadUInt64();

            // Reserved (16 bytes)
            reader.ReadUInt64();
            reader.ReadUInt64();

            return entry;
        }

        private static void ValidateTensorEntry(SmqFormat.TensorEntry entry, int index, long fileSize, List<ValidationError> errors)
        {
            string tensorName = string.IsNullOrEmpty(entry.Name) ? $"<unnamed:{index}>" : entry.Name;

            // Validate data type
            if (!IsValidQuantScheme(entry.DataType))
            {
                errors.Add(new ValidationError
                {
                    TensorName = tensorName,
                    Message = $"Invalid data type: {entry.DataType}",
                    Severity = ValidationSeverity.Error
                });
            }

            // Validate rank
            if (entry.Rank <= 0 || entry.Rank > 8)
            {
                errors.Add(new ValidationError
                {
                    TensorName = tensorName,
                    Message = $"Invalid rank: {entry.Rank}",
                    Severity = ValidationSeverity.Error
                });
                return; // Can't continue validating this tensor
            }

            // Validate dimensions
            for (int i = 0; i < entry.Rank; i++)
            {
                if (entry.Dimensions[i] <= 0)
                {
                    errors.Add(new ValidationError
                    {
                        TensorName = tensorName,
                        Message = $"Invalid dimension[{i}]: {entry.Dimensions[i]}",
                        Severity = ValidationSeverity.Error
                    });
                }
            }

            int totalElements = SmqFormat.GetTotalElements(entry.Dimensions);
            if (totalElements <= 0)
            {
                errors.Add(new ValidationError
                {
                    TensorName = tensorName,
                    Message = $"Invalid total elements: {totalElements}",
                    Severity = ValidationSeverity.Error
                });
                return;
            }

            // Validate block size
            if (entry.DataType == QuantScheme.Q8_0 || entry.DataType == QuantScheme.Q4_0)
            {
                if (entry.BlockSize == 0)
                {
                    errors.Add(new ValidationError
                    {
                        TensorName = tensorName,
                        Message = "Block size must be > 0 for quantized types",
                        Severity = ValidationSeverity.Error
                    });
                }
            }

            // Validate data offset and length
            if (entry.DataOffset == 0 && entry.DataLength > 0)
            {
                errors.Add(new ValidationError
                {
                    TensorName = tensorName,
                    Message = "Data offset is 0 but data length > 0",
                    Severity = ValidationSeverity.Error
                });
            }

            if (entry.DataOffset > 0)
            {
                if (entry.DataOffset >= (ulong)fileSize)
                {
                    errors.Add(new ValidationError
                    {
                        TensorName = tensorName,
                        Message = $"Data offset {entry.DataOffset} beyond file size {fileSize}",
                        Severity = ValidationSeverity.Error
                    });
                }

                if (entry.DataOffset + entry.DataLength > (ulong)fileSize)
                {
                    errors.Add(new ValidationError
                    {
                        TensorName = tensorName,
                        Message = $"Data region extends beyond file: {entry.DataOffset + entry.DataLength} > {fileSize}",
                        Severity = ValidationSeverity.Error
                    });
                }
            }

            // Validate aux offset and length
            if (entry.AuxOffset > 0)
            {
                if (entry.AuxOffset >= (ulong)fileSize)
                {
                    errors.Add(new ValidationError
                    {
                        TensorName = tensorName,
                        Message = $"Aux offset {entry.AuxOffset} beyond file size {fileSize}",
                        Severity = ValidationSeverity.Error
                    });
                }

                if (entry.AuxOffset + entry.AuxLength > (ulong)fileSize)
                {
                    errors.Add(new ValidationError
                    {
                        TensorName = tensorName,
                        Message = $"Aux region extends beyond file: {entry.AuxOffset + entry.AuxLength} > {fileSize}",
                        Severity = ValidationSeverity.Error
                    });
                }
            }

            // Validate expected sizes
            try
            {
                ulong expectedDataSize = SmqFormat.GetExpectedDataSize(entry.DataType, totalElements);
                if (entry.DataLength != expectedDataSize)
                {
                    errors.Add(new ValidationError
                    {
                        TensorName = tensorName,
                        Message = $"Data length mismatch: expected {expectedDataSize}, got {entry.DataLength}",
                        Severity = ValidationSeverity.Error
                    });
                }

                ulong expectedAuxSize = SmqFormat.GetExpectedAuxSize(entry.DataType, totalElements, entry.BlockSize);
                if (entry.AuxLength != expectedAuxSize)
                {
                    errors.Add(new ValidationError
                    {
                        TensorName = tensorName,
                        Message = $"Aux length mismatch: expected {expectedAuxSize}, got {entry.AuxLength}",
                        Severity = ValidationSeverity.Error
                    });
                }
            }
            catch (Exception ex)
            {
                errors.Add(new ValidationError
                {
                    TensorName = tensorName,
                    Message = $"Size calculation error: {ex.Message}",
                    Severity = ValidationSeverity.Error
                });
            }
        }

        private static void ValidateNoOverlaps(List<SmqFormat.TensorEntry> entries, List<ValidationError> errors)
        {
            // Collect all data regions
            var regions = new List<(string name, ulong start, ulong end)>();

            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                string name = string.IsNullOrEmpty(entry.Name) ? $"<unnamed:{i}>" : entry.Name;

                if (entry.DataLength > 0)
                {
                    regions.Add((name + ":data", entry.DataOffset, entry.DataOffset + entry.DataLength));
                }

                if (entry.AuxLength > 0)
                {
                    regions.Add((name + ":aux", entry.AuxOffset, entry.AuxOffset + entry.AuxLength));
                }
            }

            // Sort by start offset
            regions.Sort((a, b) => a.start.CompareTo(b.start));

            // Check for overlaps
            for (int i = 0; i < regions.Count - 1; i++)
            {
                var current = regions[i];
                var next = regions[i + 1];

                if (current.end > next.start)
                {
                    errors.Add(new ValidationError
                    {
                        Message = $"Overlapping regions: {current.name} [{current.start}, {current.end}) and {next.name} [{next.start}, {next.end})",
                        Severity = ValidationSeverity.Error
                    });
                }
            }
        }

        private static bool IsValidQuantScheme(QuantScheme scheme)
        {
            return scheme == QuantScheme.F32
                || scheme == QuantScheme.F16
                || scheme == QuantScheme.Q8_0
                || scheme == QuantScheme.Q4_0;
        }
    }
}
