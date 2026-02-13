using SmallMind.Quantization.IO.Gguf;

namespace SmallMind.Runtime.Gguf.TensorDecoders
{
    /// <summary>
    /// Interface for decoding GGUF tensor data into SmallMind's internal representation.
    /// Each decoder handles one or more GGUF tensor types (e.g., Q4_0, Q8_K, F32).
    /// </summary>
    internal interface ITensorDecoder
    {
        /// <summary>
        /// Determines whether this decoder can handle the specified tensor type.
        /// </summary>
        /// <param name="type">GGUF tensor type to check.</param>
        /// <returns>True if this decoder supports the tensor type, false otherwise.</returns>
        bool CanDecode(GgufTensorType type);

        /// <summary>
        /// Decodes GGUF tensor data into a dequantized float array.
        /// </summary>
        /// <param name="tensorInfo">Metadata about the tensor (type, dimensions, etc.).</param>
        /// <param name="rawData">Raw binary data from the GGUF file.</param>
        /// <returns>Dequantized float array containing the tensor values.</returns>
        /// <remarks>
        /// This method performs full dequantization to float32 for use in the inference runtime.
        /// The output array is row-major and can be reshaped according to tensorInfo.Dimensions.
        /// </remarks>
        float[] Decode(GgufTensorInfo tensorInfo, byte[] rawData);
    }
}
