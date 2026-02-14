namespace SmallMind.Quantization.IO.Gguf
{
    /// <summary>
    /// GGUF tensor data types.
    /// </summary>
    internal enum GgufTensorType : uint
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
}
