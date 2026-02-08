using System;
using System.Runtime.CompilerServices;

namespace SmallMind.Runtime.Cache
{
    /// <summary>
    /// Utilities for quantizing and dequantizing KV cache data.
    /// </summary>
    public static class QuantizationHelpers
    {
        /// <summary>
        /// Quantizes FP32 to INT8 using linear quantization.
        /// Returns quantized data and scale/offset for dequantization.
        /// </summary>
        public static void QuantizeToInt8(
            ReadOnlySpan<float> input,
            Span<byte> output,
            out float scale,
            out float offset)
        {
            if (input.Length != output.Length)
                throw new ArgumentException("Input and output must have same length");
            
            // Find min/max for dynamic range
            float min = float.MaxValue;
            float max = float.MinValue;
            
            foreach (float val in input)
            {
                if (val < min) min = val;
                if (val > max) max = val;
            }
            
            // Compute scale and offset
            float range = max - min;
            scale = range / 255.0f;
            offset = min;
            
            // Quantize
            for (int i = 0; i < input.Length; i++)
            {
                float normalized = (input[i] - offset) / scale;
                output[i] = (byte)Math.Clamp(normalized, 0, 255);
            }
        }
        
        /// <summary>
        /// Dequantizes INT8 back to FP32.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DequantizeFromInt8(
            ReadOnlySpan<byte> input,
            Span<float> output,
            float scale,
            float offset)
        {
            for (int i = 0; i < input.Length; i++)
            {
                output[i] = input[i] * scale + offset;
            }
        }
        
        /// <summary>
        /// Converts FP32 to FP16 (Half).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Half FloatToHalf(float value) => (Half)value;
        
        /// <summary>
        /// Converts FP16 (Half) to FP32.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float HalfToFloat(Half value) => (float)value;
    }
}
