using System.IO.MemoryMappedFiles;
using SmallMind.Core.Validation;

namespace SmallMind.Core.Core
{
    /// <summary>
    /// Memory-mapped storage for large tensors that can exceed available RAM.
    /// Streams tensor data from disk using memory-mapped files for on-demand access.
    /// 
    /// Use cases:
    /// - Very large models that don't fit in RAM
    /// - Inference-only scenarios where weights are read-only
    /// - Sharing large model weights across multiple processes
    /// 
    /// Trade-offs:
    /// - Much slower than in-memory (disk I/O bottleneck)
    /// - Read-only by default (writable mode requires careful management)
    /// - OS manages paging automatically
    /// - Excellent for inference, not suitable for training
    /// </summary>
    internal sealed class MemoryMappedTensorStorage : ITensorStorage, IDisposable
    {
        private readonly MemoryMappedFile _mmf;
        private readonly MemoryMappedViewAccessor _accessor;
        private readonly long _length;
        private readonly string _filePath;
        private bool _disposed;

        /// <summary>
        /// Gets the total number of elements in the storage.
        /// </summary>
        public long Length => _length;

        /// <summary>
        /// Memory-mapped storage is not chunked (it's streamed from disk).
        /// </summary>
        public bool IsChunked => false;

        /// <summary>
        /// Creates memory-mapped storage from an existing file.
        /// </summary>
        /// <param name="filePath">Path to the file containing tensor data.</param>
        /// <param name="length">Number of float elements in the file.</param>
        /// <param name="writable">Whether to allow write access (default: false for read-only inference).</param>
        public MemoryMappedTensorStorage(string filePath, long length, bool writable = false)
        {
            Guard.NotNullOrWhiteSpace(filePath);
            Guard.GreaterThan(length, 0L, nameof(length));

            if (!File.Exists(filePath))
            {
                throw new Exceptions.ValidationException(
                    $"Tensor file not found: {filePath}",
                    nameof(filePath));
            }

            _filePath = filePath;
            _length = length;

            long fileSize = length * sizeof(float);
            var fileInfo = new FileInfo(filePath);

            if (fileInfo.Length < fileSize)
            {
                throw new Exceptions.ValidationException(
                    $"File size ({fileInfo.Length} bytes) is smaller than expected tensor size ({fileSize} bytes)",
                    nameof(filePath));
            }

            try
            {
                _mmf = MemoryMappedFile.CreateFromFile(
                    filePath,
                    FileMode.Open,
                    mapName: null,
                    capacity: 0,
                    writable ? MemoryMappedFileAccess.ReadWrite : MemoryMappedFileAccess.Read);

                _accessor = _mmf.CreateViewAccessor(
                    offset: 0,
                    size: 0,
                    writable ? MemoryMappedFileAccess.ReadWrite : MemoryMappedFileAccess.Read);
            }
            catch (Exception ex)
            {
                throw new Exceptions.ValidationException(
                    $"Failed to create memory-mapped file for {filePath}: {ex.Message}",
                    ex,
                    nameof(filePath));
            }
        }

        /// <summary>
        /// Creates a new memory-mapped file and initializes it with zeros.
        /// </summary>
        /// <param name="filePath">Path where the file will be created.</param>
        /// <param name="length">Number of float elements to allocate.</param>
        public static MemoryMappedTensorStorage Create(string filePath, long length)
        {
            Guard.NotNullOrWhiteSpace(filePath);
            Guard.GreaterThan(length, 0L, nameof(length));

            long fileSize = length * sizeof(float);

            // Create the file and initialize with zeros
            using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                fs.SetLength(fileSize);
            }

            return new MemoryMappedTensorStorage(filePath, length, writable: true);
        }

        /// <summary>
        /// Gets the element at the specified index.
        /// WARNING: Disk I/O overhead - use batch operations when possible.
        /// </summary>
        public float Get(long index)
        {
            Guard.InRange(index, 0L, _length - 1, nameof(index));
            ThrowIfDisposed();

            long byteOffset = index * sizeof(float);
            return _accessor.ReadSingle(byteOffset);
        }

        /// <summary>
        /// Sets the element at the specified index.
        /// WARNING: Disk I/O overhead - use batch operations when possible.
        /// </summary>
        public void Set(long index, float value)
        {
            Guard.InRange(index, 0L, _length - 1, nameof(index));
            ThrowIfDisposed();

            long byteOffset = index * sizeof(float);
            _accessor.Write(byteOffset, value);
        }

        /// <summary>
        /// Copies data from storage to a destination span.
        /// Optimized for bulk reads from disk.
        /// </summary>
        public void CopyTo(long sourceIndex, Span<float> destination, int length)
        {
            Guard.InRange(sourceIndex, 0L, _length - 1, nameof(sourceIndex));
            Guard.GreaterThan(length, 0, nameof(length));

            if (sourceIndex + length > _length)
            {
                throw new Exceptions.ValidationException(
                    $"Copy range exceeds storage length",
                    nameof(length));
            }

            if (destination.Length < length)
            {
                throw new Exceptions.ValidationException(
                    $"Destination too small",
                    nameof(destination));
            }

            ThrowIfDisposed();

            long byteOffset = sourceIndex * sizeof(float);

            // Read floats one by one from memory-mapped file
            for (int i = 0; i < length; i++)
            {
                destination[i] = _accessor.ReadSingle(byteOffset + i * sizeof(float));
            }
        }

        /// <summary>
        /// Copies data from a source span to storage.
        /// Optimized for bulk writes to disk.
        /// </summary>
        public void CopyFrom(ReadOnlySpan<float> source, long destinationIndex)
        {
            Guard.InRange(destinationIndex, 0L, _length - 1, nameof(destinationIndex));

            if (destinationIndex + source.Length > _length)
            {
                throw new Exceptions.ValidationException(
                    $"Copy range exceeds storage length",
                    nameof(source));
            }

            ThrowIfDisposed();

            long byteOffset = destinationIndex * sizeof(float);

            // Write floats one by one to memory-mapped file
            for (int i = 0; i < source.Length; i++)
            {
                _accessor.Write(byteOffset + i * sizeof(float), source[i]);
            }
        }

        /// <summary>
        /// Fills the entire storage with the specified value.
        /// WARNING: Very slow for large files - consider pre-initialized files.
        /// </summary>
        public void Fill(float value)
        {
            ThrowIfDisposed();

            // Write in chunks to avoid excessive memory usage
            const int CHUNK_SIZE = 1024 * 1024; // 1M floats = 4MB
            var chunk = new float[CHUNK_SIZE];
            Array.Fill(chunk, value);

            long remaining = _length;
            long offset = 0;

            while (remaining > 0)
            {
                int toWrite = (int)Math.Min(remaining, CHUNK_SIZE);
                CopyFrom(chunk.AsSpan(0, toWrite), offset);
                offset += toWrite;
                remaining -= toWrite;
            }
        }

        /// <summary>
        /// Clears the entire storage (sets to zero).
        /// WARNING: Very slow for large files.
        /// </summary>
        public void Clear()
        {
            Fill(0f);
        }

        /// <summary>
        /// Memory-mapped storage cannot return a dense array.
        /// Use CopyTo to read data into memory as needed.
        /// </summary>
        public float[] GetDenseArray()
        {
            throw new NotSupportedException(
                "Memory-mapped storage cannot be returned as a dense array. " +
                "Use CopyTo to read portions of the data into memory.");
        }

        /// <summary>
        /// Memory-mapped storage does not use chunked buffers.
        /// </summary>
        public ChunkedBuffer GetChunkedBuffer()
        {
            throw new NotSupportedException(
                "Memory-mapped storage does not use chunked buffers.");
        }

        /// <summary>
        /// Gets the file path of the memory-mapped storage.
        /// </summary>
        public string FilePath => _filePath;

        /// <summary>
        /// Flushes any pending writes to disk.
        /// </summary>
        public void Flush()
        {
            ThrowIfDisposed();
            _accessor.Flush();
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(MemoryMappedTensorStorage));
        }

        public void Dispose()
        {
            if (_disposed) return;

            _accessor?.Dispose();
            _mmf?.Dispose();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
