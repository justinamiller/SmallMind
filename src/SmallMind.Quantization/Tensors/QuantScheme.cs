namespace SmallMind.Quantization.Tensors
{
    /// <summary>
    /// Supported quantization schemes for weight compression.
    /// </summary>
    internal enum QuantScheme : uint
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
        Q4_0 = 11,

        /// <summary>
        /// 4-bit asymmetric quantization with per-block scale and minimum.
        /// Each block (32 values) has scale and min (fp16), values in [0, 15].
        /// Formula: value = q * scale + min
        /// </summary>
        Q4_1 = 12,

        /// <summary>
        /// 5-bit symmetric quantization with per-block scaling.
        /// Each block (32 values) has scale (fp16), high bits (4 bytes), low nibbles (16 bytes).
        /// Effective range [-16, 15] with better precision than Q4_0.
        /// </summary>
        Q5_0 = 13,

        /// <summary>
        /// 4-bit K-quant with super-block structure.
        /// Block size: 256 values per super-block (8 sub-blocks of 32 values).
        /// Better compression ratio than Q4_0/Q4_1.
        /// </summary>
        Q4_K = 20,

        /// <summary>
        /// 6-bit K-quant with super-block structure.
        /// Block size: 256 values per super-block (16 sub-blocks of 16 values).
        /// Better precision than Q4_K with reasonable compression.
        /// </summary>
        Q6_K = 21
    }
}
