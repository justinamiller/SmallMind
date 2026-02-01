namespace SmallMind.Quantization.Tensors
{
    /// <summary>
    /// Supported quantization schemes for weight compression.
    /// </summary>
    public enum QuantScheme : uint
    {
        /// <summary>
        /// Standard 32-bit floating point (no quantization).
        /// </summary>
        F32 = 1,

        /// <summary>
        /// 16-bit floating point.
        /// </summary>
        F16 = 2,

        /// <summary>
        /// 8-bit symmetric quantization with per-block scaling.
        /// Each block has a single scale factor, values in [-127, 127].
        /// </summary>
        Q8_0 = 10,

        /// <summary>
        /// 4-bit symmetric quantization with per-block scaling.
        /// Each block has a single scale factor, values packed as 4-bit signed in [-8, 7].
        /// Two values per byte.
        /// </summary>
        Q4_0 = 11
    }
}
