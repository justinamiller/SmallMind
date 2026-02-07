using System;

namespace SmallMind.Quantization.Tensors
{
    /// <summary>
    /// Represents a full-precision (FP32) tensor.
    /// Used for storing unquantized model weights (e.g., layer norm parameters).
    /// </summary>
    public sealed class Fp32Tensor
    {
        /// <summary>
        /// Raw float data.
        /// </summary>
        public float[] Data { get; }

        /// <summary>
        /// Tensor dimensions.
        /// </summary>
        public int[] Dimensions { get; }

        /// <summary>
        /// Rank (number of dimensions).
        /// </summary>
        public int Rank => Dimensions.Length;

        /// <summary>
        /// Total number of elements.
        /// </summary>
        public int TotalElements
        {
            get
            {
                int total = 1;
                foreach (var dim in Dimensions)
                {
                    total *= dim;
                }
                return total;
            }
        }

        /// <summary>
        /// Creates a new FP32 tensor.
        /// </summary>
        /// <param name="data">Float data.</param>
        /// <param name="dimensions">Tensor dimensions.</param>
        public Fp32Tensor(float[] data, params int[] dimensions)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            if (dimensions == null || dimensions.Length == 0)
                throw new ArgumentException("Dimensions cannot be null or empty", nameof(dimensions));

            // Validate dimensions match data length
            int expectedElements = 1;
            foreach (var dim in dimensions)
            {
                if (dim <= 0)
                    throw new ArgumentException($"Invalid dimension: {dim}", nameof(dimensions));
                expectedElements *= dim;
            }

            if (data.Length != expectedElements)
                throw new ArgumentException(
                    $"Data length {data.Length} does not match dimensions {string.Join("x", dimensions)} = {expectedElements}",
                    nameof(data));

            Data = data;
            Dimensions = dimensions;
        }

        /// <summary>
        /// Creates an FP32 tensor from ulong dimensions (for GGUF compatibility).
        /// </summary>
        public static Fp32Tensor FromUlongDimensions(float[] data, ulong[] dimensions)
        {
            var intDims = new int[dimensions.Length];
            for (int i = 0; i < dimensions.Length; i++)
            {
                intDims[i] = (int)dimensions[i];
            }
            return new Fp32Tensor(data, intDims);
        }
    }
}
