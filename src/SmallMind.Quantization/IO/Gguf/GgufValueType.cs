namespace SmallMind.Quantization.IO.Gguf
{
    /// <summary>
    /// GGUF metadata value types.
    /// </summary>
    internal enum GgufValueType : uint
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
}
