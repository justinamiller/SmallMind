namespace SmallMind.Transformers
{
    /// <summary>
    /// Rotary Position Embeddings (RoPE) for transformer attention.
    /// Precomputes sin/cos tables once and applies rotation in-place to Q and K.
    /// Used in modern architectures like Llama, Mistral, Qwen, Gemma.
    /// </summary>
    internal sealed class RotaryEmbedding
    {
        private readonly int _maxSeqLen;
        private readonly int _headDim;
        private readonly float _theta;
        private readonly float[] _sinTable;
        private readonly float[] _cosTable;
        private readonly int _halfDim;

        /// <summary>
        /// Creates a RotaryEmbedding instance.
        /// </summary>
        /// <param name="maxSeqLen">Maximum sequence length</param>
        /// <param name="headDim">Dimension per attention head (must be even)</param>
        /// <param name="theta">Base frequency (default 10000.0 for Llama)</param>
        public RotaryEmbedding(int maxSeqLen, int headDim, float theta = 10000.0f)
        {
            if (headDim % 2 != 0)
                throw new ArgumentException($"Head dimension {headDim} must be even for RoPE", nameof(headDim));

            _maxSeqLen = maxSeqLen;
            _headDim = headDim;
            _theta = theta;
            _halfDim = headDim / 2;

            // Precompute sin/cos tables for all positions and dimensions
            // Layout: [pos * halfDim + i] for cache-friendly access
            _sinTable = new float[maxSeqLen * _halfDim];
            _cosTable = new float[maxSeqLen * _halfDim];

            PrecomputeTables();
        }

        /// <summary>
        /// Precompute sin/cos tables for all positions.
        /// Formula: freq_i = 1 / (theta^(2i / headDim))
        /// </summary>
        private void PrecomputeTables()
        {
            for (int pos = 0; pos < _maxSeqLen; pos++)
            {
                for (int i = 0; i < _halfDim; i++)
                {
                    // Compute frequency for this dimension pair
                    float freq = 1.0f / MathF.Pow(_theta, (2.0f * i) / _headDim);
                    float angle = pos * freq;

                    int index = pos * _halfDim + i;
                    _sinTable[index] = MathF.Sin(angle);
                    _cosTable[index] = MathF.Cos(angle);
                }
            }
        }

        /// <summary>
        /// Apply RoPE rotation in-place to Q and K tensors.
        /// Q and K shapes: (batch, nHeads, seqLen, headDim) or (batch, nKvHeads, seqLen, headDim)
        /// </summary>
        /// <param name="q">Query tensor</param>
        /// <param name="k">Key tensor</param>
        /// <param name="position">Starting position in sequence (for incremental decoding)</param>
        public void ApplyInPlace(Span<float> q, Span<float> k, int position, int batchSize, int nHeads, int nKvHeads, int seqLen)
        {
            // Apply rotation to Q (uses nHeads)
            ApplyRotationInPlace(q, position, batchSize, nHeads, seqLen);

            // Apply rotation to K (uses nKvHeads)
            ApplyRotationInPlace(k, position, batchSize, nKvHeads, seqLen);
        }

        /// <summary>
        /// Apply RoPE rotation in-place to a single tensor (Q or K).
        /// Tensor shape: (batch, nHeads, seqLen, headDim)
        /// For each pair (x0, x1) at dimension i:
        ///   x0' = x0 * cos(theta) - x1 * sin(theta)
        ///   x1' = x0 * sin(theta) + x1 * cos(theta)
        /// </summary>
        private void ApplyRotationInPlace(Span<float> data, int positionOffset, int batchSize, int nHeads, int seqLen)
        {
            for (int b = 0; b < batchSize; b++)
            {
                for (int h = 0; h < nHeads; h++)
                {
                    for (int s = 0; s < seqLen; s++)
                    {
                        int pos = positionOffset + s;
                        if (pos >= _maxSeqLen)
                            throw new ArgumentException($"Position {pos} exceeds max sequence length {_maxSeqLen}");

                        // Base offset for this (batch, head, seq) position
                        int baseOffset = ((b * nHeads + h) * seqLen + s) * _headDim;

                        // Apply rotation to each pair of dimensions
                        for (int i = 0; i < _halfDim; i++)
                        {
                            int i0 = baseOffset + 2 * i;
                            int i1 = baseOffset + 2 * i + 1;

                            float x0 = data[i0];
                            float x1 = data[i1];

                            // Get sin/cos from precomputed tables
                            int tableIndex = pos * _halfDim + i;
                            float cos = _cosTable[tableIndex];
                            float sin = _sinTable[tableIndex];

                            // Apply rotation
                            data[i0] = x0 * cos - x1 * sin;
                            data[i1] = x0 * sin + x1 * cos;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Get maximum sequence length supported by this RoPE instance.
        /// </summary>
        public int MaxSeqLen => _maxSeqLen;

        /// <summary>
        /// Get head dimension.
        /// </summary>
        public int HeadDim => _headDim;
    }
}
