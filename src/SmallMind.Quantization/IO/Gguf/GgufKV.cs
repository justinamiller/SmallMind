namespace SmallMind.Quantization.IO.Gguf
{
    /// <summary>
    /// GGUF key-value metadata entry.
    /// </summary>
    internal class GgufKV
    {
        /// <summary>
        /// Metadata key.
        /// </summary>
        public string Key { get; set; } = "";

        /// <summary>
        /// Value type.
        /// </summary>
        public GgufValueType Type { get; set; }

        /// <summary>
        /// Value (can be various types: primitive, string, or array).
        /// </summary>
        public object? Value { get; set; }
    }
}
