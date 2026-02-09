namespace SmallMind.Runtime.Cache
{
    /// <summary>
    /// Quantization precision for KV cache storage.
    /// </summary>
    internal enum QuantizationType
    {
        /// <summary>No quantization - full FP32 precision.</summary>
        None = 0,
        
        /// <summary>FP16 half precision - 2x memory reduction.</summary>
        FP16 = 1,
        
        /// <summary>INT8 quantization - 4x memory reduction.</summary>
        INT8 = 2
    }
}
