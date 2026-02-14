namespace SmallMind.Quantization.IO.Gguf
{
    /// <summary>
    /// GGUF tensor metadata.
    /// </summary>
    internal class GgufTensorInfo
    {
        /// <summary>
        /// Tensor name.
        /// </summary>
        public string Name { get; set; } = "";

        /// <summary>
        /// Tensor data type.
        /// </summary>
        public GgufTensorType Type { get; set; }

        /// <summary>
        /// Tensor dimensions (e.g., [rows, cols] for 2D).
        /// </summary>
        public ulong[] Dimensions { get; set; } = Array.Empty<ulong>();

        /// <summary>
        /// Offset of tensor data from start of file.
        /// </summary>
        public ulong Offset { get; set; }

        /// <summary>
        /// Size of tensor data in bytes.
        /// </summary>
        public ulong Size { get; set; }
    }
}
