using System;
using System.Collections.Generic;

namespace SmallMind.Quantization.IO.Gguf
{
    /// <summary>
    /// GGUF metadata value types.
    /// </summary>
    public enum GgufValueType : uint
    {
        /// <summary>8-bit unsigned integer.</summary>
        UInt8 = 0,
        /// <summary>8-bit signed integer.</summary>
        Int8 = 1,
        /// <summary>16-bit unsigned integer.</summary>
        UInt16 = 2,
        /// <summary>16-bit signed integer.</summary>
        Int16 = 3,
        /// <summary>32-bit unsigned integer.</summary>
        UInt32 = 4,
        /// <summary>32-bit signed integer.</summary>
        Int32 = 5,
        /// <summary>32-bit floating point.</summary>
        Float32 = 6,
        /// <summary>Boolean value.</summary>
        Bool = 7,
        /// <summary>UTF-8 string.</summary>
        String = 8,
        /// <summary>Array of values.</summary>
        Array = 9,
        /// <summary>64-bit unsigned integer.</summary>
        UInt64 = 10,
        /// <summary>64-bit signed integer.</summary>
        Int64 = 11,
        /// <summary>64-bit floating point.</summary>
        Float64 = 12
    }

    /// <summary>
    /// GGUF tensor data types.
    /// </summary>
    public enum GgufTensorType : uint
    {
        /// <summary>32-bit floating point.</summary>
        F32 = 0,
        /// <summary>16-bit floating point.</summary>
        F16 = 1,
        /// <summary>4-bit quantized (type 0).</summary>
        Q4_0 = 2,
        /// <summary>4-bit quantized (type 1).</summary>
        Q4_1 = 3,
        /// <summary>5-bit quantized (type 0).</summary>
        Q5_0 = 6,
        /// <summary>5-bit quantized (type 1).</summary>
        Q5_1 = 7,
        /// <summary>8-bit quantized (type 0).</summary>
        Q8_0 = 8,
        /// <summary>8-bit quantized (type 1).</summary>
        Q8_1 = 9,
        /// <summary>K-quant 2-bit.</summary>
        Q2_K = 10,
        /// <summary>K-quant 3-bit.</summary>
        Q3_K = 11,
        /// <summary>K-quant 4-bit.</summary>
        Q4_K = 12,
        /// <summary>K-quant 5-bit.</summary>
        Q5_K = 13,
        /// <summary>K-quant 6-bit.</summary>
        Q6_K = 14,
        /// <summary>K-quant 8-bit.</summary>
        Q8_K = 15,
        /// <summary>Importance-weighted 2-bit (XXS).</summary>
        IQ2_XXS = 16,
        /// <summary>Importance-weighted 2-bit (XS).</summary>
        IQ2_XS = 17,
        /// <summary>Importance-weighted 3-bit (XXS).</summary>
        IQ3_XXS = 18,
        /// <summary>Importance-weighted 1-bit (S).</summary>
        IQ1_S = 19,
        /// <summary>Importance-weighted 4-bit (NL).</summary>
        IQ4_NL = 20,
        /// <summary>Importance-weighted 3-bit (S).</summary>
        IQ3_S = 21,
        /// <summary>Importance-weighted 2-bit (S).</summary>
        IQ2_S = 22,
        /// <summary>Importance-weighted 4-bit (XS).</summary>
        IQ4_XS = 23
    }

    /// <summary>
    /// GGUF key-value metadata entry.
    /// </summary>
    public class GgufKV
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

    /// <summary>
    /// GGUF tensor metadata.
    /// </summary>
    public class GgufTensorInfo
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
