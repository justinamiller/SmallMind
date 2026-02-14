using System.Buffers;
using SmallMind.Core.Validation;

namespace SmallMind.Core.Core
{
    /// <summary>
    /// Sliding window processor for handling large sequences with overlapping chunks.
    /// Enables processing of 32k+ token contexts by breaking them into manageable windows.
    /// </summary>
    internal sealed class SlidingWindowProcessor
    {
        private readonly int _windowSize;
        private readonly int _stride;

        /// <summary>
        /// Creates a new sliding window processor.
        /// </summary>
        /// <param name="windowSize">Size of each processing window (e.g., 4096 tokens).</param>
        /// <param name="stride">Number of tokens to advance between windows. Use windowSize - overlap for overlapping windows.</param>
        public SlidingWindowProcessor(int windowSize, int stride)
        {
            Guard.GreaterThan(windowSize, 0, nameof(windowSize));
            Guard.GreaterThan(stride, 0, nameof(stride));

            if (stride > windowSize)
            {
                throw new Exceptions.ValidationException(
                    "Stride cannot be greater than window size",
                    nameof(stride));
            }

            _windowSize = windowSize;
            _stride = stride;
        }

        /// <summary>
        /// Gets the window size.
        /// </summary>
        public int WindowSize => _windowSize;

        /// <summary>
        /// Gets the stride.
        /// </summary>
        public int Stride => _stride;

        /// <summary>
        /// Gets the overlap size between consecutive windows.
        /// </summary>
        public int OverlapSize => _windowSize - _stride;

        /// <summary>
        /// Generate sliding windows from a token sequence.
        /// </summary>
        /// <param name="tokens">Input token sequence.</param>
        /// <returns>Enumerable of token windows.</returns>
        public IEnumerable<int[]> GetWindows(int[] tokens)
        {
            Guard.NotNull(tokens, nameof(tokens));

            if (tokens.Length == 0)
            {
                yield break;
            }

            // If sequence fits in one window, return it as-is
            if (tokens.Length <= _windowSize)
            {
                yield return tokens;
                yield break;
            }

            // Generate overlapping windows
            for (int i = 0; i < tokens.Length; i += _stride)
            {
                int remaining = tokens.Length - i;
                int windowLen = Math.Min(_windowSize, remaining);

                var window = new int[windowLen];
                Array.Copy(tokens, i, window, 0, windowLen);

                yield return window;

                // Stop if we've covered the entire sequence
                if (i + windowLen >= tokens.Length)
                {
                    break;
                }
            }
        }

        /// <summary>
        /// Generate sliding windows from a tensor sequence.
        /// </summary>
        /// <param name="tokens">Input tensor with shape [batch, sequence_length].</param>
        /// <returns>Enumerable of tensor windows.</returns>
        public IEnumerable<Tensor> GetWindowTensors(Tensor tokens)
        {
            Guard.NotNull(tokens, nameof(tokens));

            if (tokens.Shape.Length != 2)
            {
                throw new Exceptions.ValidationException(
                    "Input tensor must have shape [batch, sequence_length]",
                    nameof(tokens));
            }

            int batchSize = tokens.Shape[0];
            int seqLen = tokens.Shape[1];

            // If sequence fits in one window, return it as-is
            if (seqLen <= _windowSize)
            {
                yield return tokens;
                yield break;
            }

            // Generate overlapping windows
            for (int i = 0; i < seqLen; i += _stride)
            {
                int remaining = seqLen - i;
                int windowLen = Math.Min(_windowSize, remaining);

                var window = new Tensor(new int[] { batchSize, windowLen }, requiresGrad: tokens.RequiresGrad);

                // Copy window data
                for (int b = 0; b < batchSize; b++)
                {
                    for (int t = 0; t < windowLen; t++)
                    {
                        int srcIdx = b * seqLen + i + t;
                        int dstIdx = b * windowLen + t;
                        window.Data[dstIdx] = tokens.Data[srcIdx];
                    }
                }

                yield return window;

                // Stop if we've covered the entire sequence
                if (i + windowLen >= seqLen)
                {
                    break;
                }
            }
        }

        /// <summary>
        /// Combine outputs from multiple windows by averaging overlapping regions.
        /// </summary>
        /// <param name="windowOutputs">List of output tensors from each window.</param>
        /// <param name="originalSeqLength">Original sequence length before windowing.</param>
        /// <returns>Combined output tensor.</returns>
        public Tensor CombineWindowOutputs(List<Tensor> windowOutputs, int originalSeqLength)
        {
            Guard.NotNull(windowOutputs, nameof(windowOutputs));
            Guard.GreaterThan(windowOutputs.Count, 0, nameof(windowOutputs));
            Guard.GreaterThan(originalSeqLength, 0, nameof(originalSeqLength));

            if (windowOutputs.Count == 1)
            {
                return windowOutputs[0];
            }

            // Get dimensions from first window
            int batchSize = windowOutputs[0].Shape[0];
            int outputDim = windowOutputs[0].Shape.Length == 3 ? windowOutputs[0].Shape[2] : 1;

            // Create output tensor and rent count buffer from ArrayPool for averaging
            var combined = new Tensor(
                new int[] { batchSize, originalSeqLength, outputDim },
                requiresGrad: windowOutputs[0].RequiresGrad);

            int countsSize = batchSize * originalSeqLength * outputDim;
            float[] counts = ArrayPool<float>.Shared.Rent(countsSize);
            if (counts.Length < countsSize)
            {
                // Defensive check: ensure rented buffer is large enough for subsequent indexing
                ArrayPool<float>.Shared.Return(counts);
                throw new InvalidOperationException("ArrayPool provided a buffer smaller than the requested size.");
            }
            try
            {
                // Clear the rented array (ArrayPool may return larger array with stale data)
                counts.AsSpan(0, countsSize).Clear();

                // Accumulate all window outputs using indexed for-loop instead of foreach
                int position = 0;
                int windowCount = windowOutputs.Count;

                // Use unsafe pointers for better performance in tight nested loop
                unsafe
                {
                    fixed (float* pCombined = combined.Data)
                    fixed (float* pCounts = counts)
                    {
                        for (int w = 0; w < windowCount; w++)
                        {
                            var window = windowOutputs[w];
                            int windowLen = window.Shape[1];

                            fixed (float* pWindow = window.Data)
                            {
                                for (int b = 0; b < batchSize; b++)
                                {
                                    int bOffset = b * originalSeqLength * outputDim;
                                    int bWindowOffset = b * windowLen * outputDim;

                                    for (int t = 0; t < windowLen; t++)
                                    {
                                        int globalPos = position + t;
                                        if (globalPos >= originalSeqLength) break;

                                        int combRowStart = bOffset + globalPos * outputDim;
                                        int windowRowStart = bWindowOffset + t * outputDim;

                                        // Use pointers for innermost loop to eliminate bounds checking
                                        for (int d = 0; d < outputDim; d++)
                                        {
                                            int combIdx = combRowStart + d;
                                            int windowIdx = windowRowStart + d;

                                            if ((uint)combIdx >= (uint)countsSize)
                                            {
                                                throw new InvalidOperationException("Computed index for combined output is outside of the allocated counts buffer.");
                                            }

                                            pCombined[combIdx] += pWindow[windowIdx];
                                            pCounts[combIdx] += 1.0f;
                                        }
                                    }
                                }
                            }

                            position += _stride;
                        }
                    }
                }

                // Average overlapping regions with unsafe pointers
                unsafe
                {
                    fixed (float* pCombined = combined.Data)
                    fixed (float* pCounts = counts)
                    {
                        int maxIndex = countsSize;
                        if (counts.Length < maxIndex)
                        {
                            maxIndex = counts.Length;
                        }

                        int limit = combined.Size < maxIndex ? combined.Size : maxIndex;

                        for (int i = 0; i < limit; i++)
                        {
                            if (pCounts[i] > 0)
                            {
                                pCombined[i] /= pCounts[i];
                            }
                        }
                    }
                }
            }
            finally
            {
                // Return counts buffer to pool
                ArrayPool<float>.Shared.Return(counts, clearArray: false);
            }

            return combined;
        }

        /// <summary>
        /// Combine outputs by taking the maximum value in overlapping regions.
        /// Useful for classification tasks.
        /// </summary>
        /// <param name="windowOutputs">List of output tensors from each window.</param>
        /// <param name="originalSeqLength">Original sequence length before windowing.</param>
        /// <returns>Combined output tensor.</returns>
        public Tensor CombineWindowOutputsMax(List<Tensor> windowOutputs, int originalSeqLength)
        {
            Guard.NotNull(windowOutputs, nameof(windowOutputs));
            Guard.GreaterThan(windowOutputs.Count, 0, nameof(windowOutputs));

            if (windowOutputs.Count == 1)
            {
                return windowOutputs[0];
            }

            // Get dimensions from first window
            int batchSize = windowOutputs[0].Shape[0];
            int outputDim = windowOutputs[0].Shape.Length == 3 ? windowOutputs[0].Shape[2] : 1;

            // Create output tensor initialized to negative infinity
            var combined = new Tensor(
                new int[] { batchSize, originalSeqLength, outputDim },
                requiresGrad: windowOutputs[0].RequiresGrad);

            for (int i = 0; i < combined.Size; i++)
            {
                combined.Data[i] = float.NegativeInfinity;
            }

            // Take maximum across all windows using indexed for-loop and unsafe pointers
            int position = 0;
            int windowCount = windowOutputs.Count;

            unsafe
            {
                fixed (float* pCombined = combined.Data)
                {
                    for (int w = 0; w < windowCount; w++)
                    {
                        var window = windowOutputs[w];
                        int windowLen = window.Shape[1];

                        fixed (float* pWindow = window.Data)
                        {
                            for (int b = 0; b < batchSize; b++)
                            {
                                int bOffset = b * originalSeqLength * outputDim;
                                int bWindowOffset = b * windowLen * outputDim;

                                for (int t = 0; t < windowLen; t++)
                                {
                                    int globalPos = position + t;
                                    if (globalPos >= originalSeqLength) break;

                                    int combRowStart = bOffset + globalPos * outputDim;
                                    int windowRowStart = bWindowOffset + t * outputDim;

                                    for (int d = 0; d < outputDim; d++)
                                    {
                                        int combIdx = combRowStart + d;
                                        int windowIdx = windowRowStart + d;

                                        float windowVal = pWindow[windowIdx];
                                        if (windowVal > pCombined[combIdx])
                                        {
                                            pCombined[combIdx] = windowVal;
                                        }
                                    }
                                }
                            }
                        }

                        position += _stride;
                    }
                }
            }

            return combined;
        }

        /// <summary>
        /// Estimate the number of windows needed for a given sequence length.
        /// </summary>
        /// <param name="sequenceLength">Length of the sequence.</param>
        /// <returns>Number of windows required.</returns>
        public int EstimateWindowCount(int sequenceLength)
        {
            if (sequenceLength <= _windowSize)
                return 1;

            return (int)Math.Ceiling((double)(sequenceLength - _windowSize) / _stride) + 1;
        }
    }
}
