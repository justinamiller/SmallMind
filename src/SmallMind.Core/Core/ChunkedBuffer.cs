using System;
using SmallMind.Core.Validation;

namespace SmallMind.Core.Core
{
    /// <summary>
    /// A chunked buffer for storing large arrays that exceed int.MaxValue elements.
    /// Stores data in multiple fixed-size chunks to bypass CLR array indexing limitations.
    /// 
    /// Key design decisions:
    /// - Chunk size is fixed to balance GC overhead vs. memory locality
    /// - Each chunk is &lt;= int.MaxValue elements (safe for CLR)
    /// - Total length can exceed int.MaxValue using long indexing
    /// - Hot paths use Span&lt;T&gt; for zero-copy access
    /// - Avoid per-element indexing in hot loops
    /// </summary>
    public sealed class ChunkedBuffer
    {
        /// <summary>
        /// Default chunk size: 64M elements (256MB for float[]).
        /// Balances between:
        /// - Large enough to minimize chunk overhead
        /// - Small enough to avoid LOH fragmentation
        /// - Good cache behavior for typical operations
        /// </summary>
        public const int DEFAULT_CHUNK_SIZE = 64 * 1024 * 1024; // 64M elements = 256MB

        private readonly float[][] _chunks;
        private readonly int _chunkSize;
        private readonly long _totalLength;
        private readonly int _lastChunkSize;

        /// <summary>
        /// Gets the total number of elements across all chunks.
        /// </summary>
        public long Length => _totalLength;

        /// <summary>
        /// Gets the size of each chunk (in elements), except possibly the last chunk.
        /// </summary>
        public int ChunkSize => _chunkSize;

        /// <summary>
        /// Gets the number of chunks.
        /// </summary>
        public int ChunkCount => _chunks.Length;

        /// <summary>
        /// Creates a new chunked buffer with the specified total length.
        /// </summary>
        /// <param name="totalLength">Total number of elements across all chunks.</param>
        /// <param name="chunkSize">Size of each chunk in elements (default: 64M).</param>
        /// <param name="initializeToZero">Whether to initialize all elements to zero (default: true).</param>
        public ChunkedBuffer(long totalLength, int chunkSize = DEFAULT_CHUNK_SIZE, bool initializeToZero = true)
        {
            Guard.GreaterThan(totalLength, 0L, nameof(totalLength));
            Guard.GreaterThan(chunkSize, 0, nameof(chunkSize));
            
            if (chunkSize > int.MaxValue)
            {
                throw new Exceptions.ValidationException(
                    $"Chunk size {chunkSize} exceeds int.MaxValue",
                    nameof(chunkSize));
            }

            _totalLength = totalLength;
            _chunkSize = chunkSize;

            // Calculate number of chunks needed
            long numChunks = (totalLength + chunkSize - 1) / chunkSize;
            
            if (numChunks > int.MaxValue)
            {
                throw new Exceptions.ValidationException(
                    $"Total length {totalLength} with chunk size {chunkSize} requires {numChunks} chunks, " +
                    $"which exceeds int.MaxValue. Increase chunk size.",
                    nameof(chunkSize));
            }

            _chunks = new float[(int)numChunks][];

            // Allocate all chunks
            for (int i = 0; i < _chunks.Length - 1; i++)
            {
                _chunks[i] = new float[chunkSize];
            }

            // Last chunk may be smaller
            long remainingElements = totalLength - (long)(_chunks.Length - 1) * chunkSize;
            _lastChunkSize = (int)remainingElements;
            _chunks[_chunks.Length - 1] = new float[_lastChunkSize];

            // Initialize to zero if requested (arrays are already zero-initialized by CLR, but explicit for clarity)
            if (!initializeToZero)
            {
                // If not initializing to zero, we could fill with NaN or leave as-is
                // For now, CLR default zero-init is fine
            }
        }

        /// <summary>
        /// Gets a span representing the specified chunk.
        /// Prefer this method for batch operations over Get/Set.
        /// </summary>
        /// <param name="chunkIndex">Index of the chunk (0-based).</param>
        /// <returns>Span over the chunk's data.</returns>
        public Span<float> GetChunkSpan(int chunkIndex)
        {
            Guard.InRange(chunkIndex, 0, _chunks.Length - 1, nameof(chunkIndex));
            return _chunks[chunkIndex].AsSpan();
        }

        /// <summary>
        /// Gets a read-only span representing the specified chunk.
        /// Prefer this method for batch operations over Get/Set.
        /// </summary>
        /// <param name="chunkIndex">Index of the chunk (0-based).</param>
        /// <returns>Read-only span over the chunk's data.</returns>
        public ReadOnlySpan<float> GetChunkReadOnlySpan(int chunkIndex)
        {
            Guard.InRange(chunkIndex, 0, _chunks.Length - 1, nameof(chunkIndex));
            return _chunks[chunkIndex].AsSpan();
        }

        /// <summary>
        /// Converts a global index to chunk index and offset within that chunk.
        /// </summary>
        /// <param name="index">Global index (0-based).</param>
        /// <returns>Tuple of (chunkIndex, offset within chunk).</returns>
        public (int chunkIndex, int offset) GetChunkOffset(long index)
        {
            Guard.InRange(index, 0L, _totalLength - 1, nameof(index));
            
            int chunkIndex = (int)(index / _chunkSize);
            int offset = (int)(index % _chunkSize);
            
            return (chunkIndex, offset);
        }

        /// <summary>
        /// Gets the element at the specified global index.
        /// WARNING: Use only for non-hot paths. For hot paths, use GetChunkSpan.
        /// </summary>
        /// <param name="index">Global index (0-based).</param>
        /// <returns>The element value.</returns>
        public float Get(long index)
        {
            var (chunkIndex, offset) = GetChunkOffset(index);
            return _chunks[chunkIndex][offset];
        }

        /// <summary>
        /// Sets the element at the specified global index.
        /// WARNING: Use only for non-hot paths. For hot paths, use GetChunkSpan.
        /// </summary>
        /// <param name="index">Global index (0-based).</param>
        /// <param name="value">The value to set.</param>
        public void Set(long index, float value)
        {
            var (chunkIndex, offset) = GetChunkOffset(index);
            _chunks[chunkIndex][offset] = value;
        }

        /// <summary>
        /// Copies data from this chunked buffer to a destination span.
        /// Handles chunk boundaries automatically.
        /// </summary>
        /// <param name="sourceIndex">Starting index in this buffer.</param>
        /// <param name="destination">Destination span.</param>
        /// <param name="length">Number of elements to copy.</param>
        public void CopyTo(long sourceIndex, Span<float> destination, int length)
        {
            Guard.InRange(sourceIndex, 0L, _totalLength - 1, nameof(sourceIndex));
            Guard.GreaterThan(length, 0, nameof(length));
            
            if (sourceIndex + length > _totalLength)
            {
                throw new Exceptions.ValidationException(
                    $"Copy range [{sourceIndex}, {sourceIndex + length}) exceeds buffer length {_totalLength}",
                    nameof(length));
            }
            
            if (destination.Length < length)
            {
                throw new Exceptions.ValidationException(
                    $"Destination length {destination.Length} is less than copy length {length}",
                    nameof(destination));
            }

            var (startChunkIndex, startOffset) = GetChunkOffset(sourceIndex);
            int destOffset = 0;
            int remaining = length;
            int currentChunkIndex = startChunkIndex;
            int currentOffset = startOffset;

            while (remaining > 0)
            {
                int chunkRemaining = _chunks[currentChunkIndex].Length - currentOffset;
                int toCopy = Math.Min(remaining, chunkRemaining);

                _chunks[currentChunkIndex].AsSpan(currentOffset, toCopy)
                    .CopyTo(destination.Slice(destOffset, toCopy));

                destOffset += toCopy;
                remaining -= toCopy;
                currentChunkIndex++;
                currentOffset = 0; // After first chunk, always start from beginning
            }
        }

        /// <summary>
        /// Copies data from a source span to this chunked buffer.
        /// Handles chunk boundaries automatically.
        /// </summary>
        /// <param name="source">Source span.</param>
        /// <param name="destinationIndex">Starting index in this buffer.</param>
        public void CopyFrom(ReadOnlySpan<float> source, long destinationIndex)
        {
            Guard.InRange(destinationIndex, 0L, _totalLength - 1, nameof(destinationIndex));
            
            if (destinationIndex + source.Length > _totalLength)
            {
                throw new Exceptions.ValidationException(
                    $"Copy range [{destinationIndex}, {destinationIndex + source.Length}) exceeds buffer length {_totalLength}",
                    nameof(source));
            }

            var (startChunkIndex, startOffset) = GetChunkOffset(destinationIndex);
            int srcOffset = 0;
            int remaining = source.Length;
            int currentChunkIndex = startChunkIndex;
            int currentOffset = startOffset;

            while (remaining > 0)
            {
                int chunkRemaining = _chunks[currentChunkIndex].Length - currentOffset;
                int toCopy = Math.Min(remaining, chunkRemaining);

                source.Slice(srcOffset, toCopy)
                    .CopyTo(_chunks[currentChunkIndex].AsSpan(currentOffset, toCopy));

                srcOffset += toCopy;
                remaining -= toCopy;
                currentChunkIndex++;
                currentOffset = 0;
            }
        }

        /// <summary>
        /// Fills the entire buffer with the specified value.
        /// </summary>
        /// <param name="value">Value to fill with.</param>
        public void Fill(float value)
        {
            for (int i = 0; i < _chunks.Length; i++)
            {
                _chunks[i].AsSpan().Fill(value);
            }
        }

        /// <summary>
        /// Clears the entire buffer (sets all elements to zero).
        /// </summary>
        public void Clear()
        {
            for (int i = 0; i < _chunks.Length; i++)
            {
                Array.Clear(_chunks[i], 0, _chunks[i].Length);
            }
        }

        /// <summary>
        /// Gets memory usage in bytes for this chunked buffer.
        /// </summary>
        /// <returns>Approximate memory usage in bytes.</returns>
        public long GetMemoryUsageBytes()
        {
            // Each float is 4 bytes, plus array overhead
            long dataBytes = _totalLength * 4L;
            
            // Array overhead: approximately 24 bytes per array object on 64-bit
            long arrayOverhead = _chunks.Length * 24L;
            
            // Pointer array for chunks
            long chunkPointerArray = _chunks.Length * 8L + 24L;
            
            return dataBytes + arrayOverhead + chunkPointerArray;
        }
    }
}
