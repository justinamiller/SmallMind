namespace SmallMind.Core.Core
{
    /// <summary>
    /// Abstraction for tensor backing storage.
    /// Allows for different storage strategies (dense array vs chunked).
    /// </summary>
    internal interface ITensorStorage
    {
        /// <summary>
        /// Total number of elements in the storage.
        /// </summary>
        long Length { get; }

        /// <summary>
        /// Whether this storage is chunked (vs. dense single array).
        /// </summary>
        bool IsChunked { get; }

        /// <summary>
        /// Gets the element at the specified index.
        /// WARNING: Use only for non-hot paths.
        /// </summary>
        float Get(long index);

        /// <summary>
        /// Sets the element at the specified index.
        /// WARNING: Use only for non-hot paths.
        /// </summary>
        void Set(long index, float value);

        /// <summary>
        /// Copies data from storage to a destination span.
        /// </summary>
        void CopyTo(long sourceIndex, Span<float> destination, int length);

        /// <summary>
        /// Copies data from a source span to storage.
        /// </summary>
        void CopyFrom(ReadOnlySpan<float> source, long destinationIndex);

        /// <summary>
        /// Fills the entire storage with the specified value.
        /// </summary>
        void Fill(float value);

        /// <summary>
        /// Clears the entire storage (sets to zero).
        /// </summary>
        void Clear();

        /// <summary>
        /// For dense storage, gets the underlying array.
        /// For chunked storage, throws NotSupportedException.
        /// </summary>
        float[] GetDenseArray();

        /// <summary>
        /// For chunked storage, gets the underlying chunked buffer.
        /// For dense storage, throws NotSupportedException.
        /// </summary>
        ChunkedBuffer GetChunkedBuffer();
    }

    /// <summary>
    /// Dense storage using a single float[] array (traditional approach).
    /// Limited to int.MaxValue elements.
    /// </summary>
    internal sealed class DenseStorage : ITensorStorage
    {
        private readonly float[] _data;

        public long Length => _data.Length;
        public bool IsChunked => false;

        public DenseStorage(int length)
        {
            _data = new float[length];
        }

        public DenseStorage(float[] data)
        {
            _data = data;
        }

        public float Get(long index) => _data[index];
        public void Set(long index, float value) => _data[index] = value;

        public void CopyTo(long sourceIndex, Span<float> destination, int length)
        {
            _data.AsSpan((int)sourceIndex, length).CopyTo(destination);
        }

        public void CopyFrom(ReadOnlySpan<float> source, long destinationIndex)
        {
            source.CopyTo(_data.AsSpan((int)destinationIndex));
        }

        public void Fill(float value)
        {
            _data.AsSpan().Fill(value);
        }

        public void Clear()
        {
            Array.Clear(_data, 0, _data.Length);
        }

        public float[] GetDenseArray() => _data;

        public ChunkedBuffer GetChunkedBuffer()
        {
            throw new NotSupportedException("Dense storage does not have a chunked buffer.");
        }
    }

    /// <summary>
    /// Chunked storage using ChunkedBuffer for large tensors exceeding int.MaxValue.
    /// </summary>
    internal sealed class ChunkedStorage : ITensorStorage
    {
        private readonly ChunkedBuffer _buffer;

        public long Length => _buffer.Length;
        public bool IsChunked => true;

        public ChunkedStorage(long length, int chunkSize = ChunkedBuffer.DEFAULT_CHUNK_SIZE)
        {
            _buffer = new ChunkedBuffer(length, chunkSize);
        }

        public ChunkedStorage(ChunkedBuffer buffer)
        {
            _buffer = buffer;
        }

        public float Get(long index) => _buffer.Get(index);
        public void Set(long index, float value) => _buffer.Set(index, value);

        public void CopyTo(long sourceIndex, Span<float> destination, int length)
        {
            _buffer.CopyTo(sourceIndex, destination, length);
        }

        public void CopyFrom(ReadOnlySpan<float> source, long destinationIndex)
        {
            _buffer.CopyFrom(source, destinationIndex);
        }

        public void Fill(float value)
        {
            _buffer.Fill(value);
        }

        public void Clear()
        {
            _buffer.Clear();
        }

        public float[] GetDenseArray()
        {
            throw new NotSupportedException(
                "Chunked storage cannot be returned as a single dense array. " +
                "Use chunked access methods or copy to a destination array.");
        }

        public ChunkedBuffer GetChunkedBuffer() => _buffer;
    }
}
