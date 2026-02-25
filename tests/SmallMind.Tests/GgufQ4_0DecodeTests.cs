using SmallMind.Quantization.IO.Gguf;
using SmallMind.Runtime.Gguf.TensorDecoders;

namespace SmallMind.Tests;

/// <summary>
/// Unit tests for GGUF Q4_0 dequantization correctness.
///
/// GGUF Q4_0 spec (matches ggml dequantize_row_q4_0):
///   Block of 32 elements:  fp16 scale (2 bytes) + 16 packed bytes.
///   For byte j (j=0..15):
///     element j      = ((byte_j &amp; 0x0F) - 8) * scale   ← low  nibble
///     element j + 16 = ((byte_j >> 4)   - 8) * scale   ← high nibble
///   Values are unsigned 4-bit offset by 8 (0→-8, 8→0, 15→7), NOT two's-complement.
/// </summary>
public class GgufQ4_0DecodeTests
{
    private readonly Q4_0Decoder _decoder = new Q4_0Decoder();

    // ------------------------------------------------------------------
    // Helper: build a minimal 1-block Q4_0 byte array
    //   scale_fp16 (2 bytes) + 16 data bytes
    // ------------------------------------------------------------------

    private static byte[] BuildOneBlock(ushort scaleFp16, byte[] dataBytes)
    {
        Assert.Equal(16, dataBytes.Length);
        var buf = new byte[18];
        buf[0] = (byte)(scaleFp16 & 0xFF);
        buf[1] = (byte)(scaleFp16 >> 8);
        Array.Copy(dataBytes, 0, buf, 2, 16);
        return buf;
    }

    private static float Fp16ToFloat(ushort half)
    {
        // Use .NET's built-in conversion via BitConverter (correct for all normal values)
        return (float)BitConverter.Int16BitsToHalf((short)half);
    }

    // fp16 representation of 1.0f  = 0x3C00
    private const ushort Scale1_0_Fp16 = 0x3C00;

    // ------------------------------------------------------------------
    // 1. Nibble value mapping: (nibble - 8) not two's complement
    // ------------------------------------------------------------------

    [Fact]
    public void Q4_0_Decode_NibbleValueMapping_UsesOffsetEight()
    {
        // Arrange: set byte 0 = 0x08 (low nibble = 8 → should give 0.0, high nibble = 0 → -8.0)
        // All other bytes = 0x88 (low=8 → 0.0, high=8 → 0.0)
        var data = new byte[16];
        data[0] = 0x08;  // low nibble=8→0.0, high nibble=0→-8.0
        for (int i = 1; i < 16; i++) data[i] = 0x88; // both nibbles=8→0.0

        var info = new GgufTensorInfo
        {
            Name = "test",
            Type = GgufTensorType.Q4_0,
            Dimensions = new ulong[] { 32 }
        };

        // Act
        float[] result = _decoder.Decode(info, BuildOneBlock(Scale1_0_Fp16, data));

        // Assert
        Assert.Equal(32, result.Length);

        // Element 0: low nibble of byte 0 = 8 → (8-8)*1.0 = 0.0
        Assert.Equal(0.0f, result[0]);

        // Element 16: high nibble of byte 0 = 0 → (0-8)*1.0 = -8.0
        Assert.Equal(-8.0f, result[16]);

        // Elements 1-15: low nibbles of bytes 1-15 = 8 → 0.0
        for (int j = 1; j < 16; j++)
            Assert.Equal(0.0f, result[j]);

        // Elements 17-31: high nibbles of bytes 1-15 = 8 → 0.0
        for (int j = 17; j < 32; j++)
            Assert.Equal(0.0f, result[j]);
    }

    [Fact]
    public void Q4_0_Decode_NibbleRange_AllValues()
    {
        // Pack nibble n (0-15) into low/high of each byte and verify:
        //   n=0  → (0-8)*1.0  = -8.0
        //   n=7  → (7-8)*1.0  = -1.0
        //   n=8  → (8-8)*1.0  =  0.0
        //   n=15 → (15-8)*1.0 =  7.0
        var data = new byte[16];
        for (int j = 0; j < 16; j++)
        {
            byte low = (byte)(j % 16);
            byte high = (byte)((j + 1) % 16);
            data[j] = (byte)((high << 4) | low);
        }

        var info = new GgufTensorInfo
        {
            Name = "test",
            Type = GgufTensorType.Q4_0,
            Dimensions = new ulong[] { 32 }
        };

        float[] result = _decoder.Decode(info, BuildOneBlock(Scale1_0_Fp16, data));

        for (int j = 0; j < 16; j++)
        {
            float expectedLow  = (j % 16) - 8;
            float expectedHigh = ((j + 1) % 16) - 8;
            Assert.Equal(expectedLow,  result[j],      precision: 4);
            Assert.Equal(expectedHigh, result[j + 16], precision: 4);
        }
    }

    // ------------------------------------------------------------------
    // 2. Split layout: low nibbles → elements 0-15, high nibbles → 16-31
    // ------------------------------------------------------------------

    [Fact]
    public void Q4_0_Decode_SplitLayout_LowNibblesFirstHalf_HighNibblesSecondHalf()
    {
        // Set low nibbles to encode -8 (nibble=0), high nibbles to encode +7 (nibble=15).
        // Expected: elements 0-15 = -8.0, elements 16-31 = +7.0
        var data = new byte[16];
        for (int j = 0; j < 16; j++)
            data[j] = (byte)(0xF0 | 0x00); // low=0 (→-8), high=15 (→+7)

        var info = new GgufTensorInfo
        {
            Name = "test",
            Type = GgufTensorType.Q4_0,
            Dimensions = new ulong[] { 32 }
        };

        float[] result = _decoder.Decode(info, BuildOneBlock(Scale1_0_Fp16, data));

        for (int j = 0; j < 16; j++)
            Assert.Equal(-8.0f, result[j]);   // low nibbles  → first half
        for (int j = 16; j < 32; j++)
            Assert.Equal(7.0f, result[j]);    // high nibbles → second half
    }

    [Fact]
    public void Q4_0_Decode_SplitLayout_NotInterleaved()
    {
        // If the implementation were (wrongly) interleaved, element 1 would come from
        // high nibble of byte 0. Verify element 1 comes from LOW nibble of byte 1 instead.
        var data = new byte[16];
        // byte 0: low=8 (→0), high=0 (→-8) — if interleaved, element 1 = -8
        // byte 1: low=15 (→7), high=8 (→0) — if split, element 1 = +7
        data[0] = (byte)(0x00 << 4 | 0x08); // low nibble=8, high nibble=0
        data[1] = (byte)(0x08 << 4 | 0x0F); // low nibble=15, high nibble=8
        for (int j = 2; j < 16; j++) data[j] = 0x88; // neutral

        var info = new GgufTensorInfo
        {
            Name = "test",
            Type = GgufTensorType.Q4_0,
            Dimensions = new ulong[] { 32 }
        };

        float[] result = _decoder.Decode(info, BuildOneBlock(Scale1_0_Fp16, data));

        // With correct split layout:
        //   element 0  = low nibble of byte 0  = 8 → 0.0
        //   element 1  = low nibble of byte 1  = 15 → 7.0   (NOT -8.0 from high nibble of byte 0)
        //   element 16 = high nibble of byte 0 = 0 → -8.0
        //   element 17 = high nibble of byte 1 = 8 → 0.0
        Assert.Equal(0.0f, result[0]);
        Assert.Equal(7.0f, result[1]);   // proves split, not interleaved
        Assert.Equal(-8.0f, result[16]);
        Assert.Equal(0.0f, result[17]);
    }

    // ------------------------------------------------------------------
    // 3. Scale factor applied correctly (including negative scale)
    // ------------------------------------------------------------------

    [Fact]
    public void Q4_0_Decode_ScaleApplied_PositiveScale()
    {
        // fp16 2.0 = 0x4000
        ushort scale2_fp16 = 0x4000;

        var data = new byte[16];
        // byte 0: low=12 (→12-8=4), high=4 (→4-8=-4)
        data[0] = (byte)(0x04 << 4 | 0x0C);
        for (int j = 1; j < 16; j++) data[j] = 0x88; // neutral zeros

        var info = new GgufTensorInfo
        {
            Name = "test",
            Type = GgufTensorType.Q4_0,
            Dimensions = new ulong[] { 32 }
        };

        float[] result = _decoder.Decode(info, BuildOneBlock(scale2_fp16, data));

        // element 0  = (12-8) * 2.0 = 8.0
        // element 16 = (4-8)  * 2.0 = -8.0
        Assert.Equal(8.0f, result[0], precision: 5);
        Assert.Equal(-8.0f, result[16], precision: 5);
    }

    [Fact]
    public void Q4_0_Decode_NegativeScale_GgmlConvention()
    {
        // ggml quantize_row_q4_0 uses d = max / -8 (scale can be negative).
        // Verify negative scale works correctly.
        // fp16 -1.0 = 0xBC00
        ushort scaleNeg1_fp16 = 0xBC00;

        var data = new byte[16];
        // byte 0: low=4 (→4-8=-4), high=12 (→12-8=4)
        data[0] = (byte)(0x0C << 4 | 0x04);
        for (int j = 1; j < 16; j++) data[j] = 0x88;

        var info = new GgufTensorInfo
        {
            Name = "test",
            Type = GgufTensorType.Q4_0,
            Dimensions = new ulong[] { 32 }
        };

        float[] result = _decoder.Decode(info, BuildOneBlock(scaleNeg1_fp16, data));

        // element 0  = (4-8)  * (-1.0) = 4.0
        // element 16 = (12-8) * (-1.0) = -4.0
        Assert.Equal(4.0f, result[0], precision: 5);
        Assert.Equal(-4.0f, result[16], precision: 5);
    }

    // ------------------------------------------------------------------
    // 4. Multi-block: blocks are independent and sequential
    // ------------------------------------------------------------------

    [Fact]
    public void Q4_0_Decode_TwoBlocks_IndependentScales()
    {
        // Block 0: scale=1.0, all bytes=0x88 (both nibbles=8→0.0)
        // Block 1: scale=2.0, byte[0]=0x8C (low=12→4.0, high=8→0.0)
        ushort scale1_fp16 = Scale1_0_Fp16;
        ushort scale2_fp16 = 0x4000;

        var buf = new byte[36]; // 2 × (2+16) = 36
        // Block 0
        buf[0] = (byte)(scale1_fp16 & 0xFF);
        buf[1] = (byte)(scale1_fp16 >> 8);
        for (int j = 0; j < 16; j++) buf[2 + j] = 0x88;
        // Block 1
        buf[18] = (byte)(scale2_fp16 & 0xFF);
        buf[19] = (byte)(scale2_fp16 >> 8);
        buf[20] = 0x8C; // low=12→4*2=8, high=8→0*2=0
        for (int j = 1; j < 16; j++) buf[20 + j] = 0x88;

        var info = new GgufTensorInfo
        {
            Name = "test",
            Type = GgufTensorType.Q4_0,
            Dimensions = new ulong[] { 64 }  // 2 blocks × 32 elements
        };

        float[] result = _decoder.Decode(info, buf);

        Assert.Equal(64, result.Length);

        // Block 0: all zeros
        for (int i = 0; i < 32; i++)
            Assert.Equal(0.0f, result[i]);

        // Block 1: element 32 = (12-8)*2.0=8.0, rest=0
        Assert.Equal(8.0f, result[32], precision: 5);
        Assert.Equal(0.0f, result[32 + 16], precision: 5); // high nibble of byte 0 = 8 → 0
        for (int i = 33; i < 48; i++)
            Assert.Equal(0.0f, result[i]);
    }
}
