using System;
using System.IO;
using SmallMind.Quantization.IO.Gguf;

namespace SmallMind.Runtime.Gguf.TensorDecoders
{
    /// <summary>
    /// Decoder for Q4_1 tensor type (4-bit with min/max).
    /// GGUF Q4_1 format: block_size=32, each block has fp16 scale + fp16 min + 16 bytes (32 x 4-bit unsigned values).
    /// </summary>
    internal sealed class Q4_1Decoder : TensorDecoderBase
    {
        public override bool CanDecode(GgufTensorType type)
        {
            return type == GgufTensorType.Q4_1;
        }

        public override float[] Decode(GgufTensorInfo tensorInfo, byte[] rawData)
        {
            int totalElements = CalculateTotalElements(tensorInfo.Dimensions);
            int numBlocks = (totalElements + GgufBlockSize - 1) / GgufBlockSize;

            var floatData = new float[totalElements];

            using (var ms = new MemoryStream(rawData))
            using (var br = new BinaryReader(ms))
            {
                for (int blockIdx = 0; blockIdx < numBlocks; blockIdx++)
                {
                    // Read fp16 scale and convert to fp32
                    ushort scaleHalf = br.ReadUInt16();
                    float scale = HalfToFloat(scaleHalf);

                    // Read fp16 min and convert to fp32
                    ushort minHalf = br.ReadUInt16();
                    float min = HalfToFloat(minHalf);

                    // Read 16 bytes (32 x 4-bit unsigned values packed)
                    int blockStart = blockIdx * GgufBlockSize;
                    int blockEnd = Math.Min(blockStart + GgufBlockSize, totalElements);

                    // Read packed nibbles for this block
                    byte[] blockData = new byte[16];
                    br.Read(blockData, 0, 16);

                    // Dequantize the block
                    for (int i = blockStart; i < blockEnd; i++)
                    {
                        int localIdx = i - blockStart;
                        int byteIdx = localIdx / 2;
                        byte packedByte = blockData[byteIdx];

                        // Extract unsigned nibble (0-15)
                        byte nibble = (localIdx % 2 == 0)
                            ? (byte)(packedByte & 0xF)
                            : (byte)((packedByte >> 4) & 0xF);

                        // Dequantize: value = scale * nibble + min
                        floatData[i] = scale * nibble + min;
                    }
                }
            }

            return floatData;
        }
    }

    /// <summary>
    /// Decoder for Q5_0 tensor type (5-bit symmetric quantization).
    /// GGUF Q5_0 format: block_size=32, each block has fp16 scale + 4 bytes high bits + 16 bytes low nibbles.
    /// </summary>
    internal sealed class Q5_0Decoder : TensorDecoderBase
    {
        public override bool CanDecode(GgufTensorType type)
        {
            return type == GgufTensorType.Q5_0;
        }

        public override float[] Decode(GgufTensorInfo tensorInfo, byte[] rawData)
        {
            int totalElements = CalculateTotalElements(tensorInfo.Dimensions);
            int numBlocks = (totalElements + GgufBlockSize - 1) / GgufBlockSize;

            var floatData = new float[totalElements];

            using (var ms = new MemoryStream(rawData))
            using (var br = new BinaryReader(ms))
            {
                for (int blockIdx = 0; blockIdx < numBlocks; blockIdx++)
                {
                    // Read fp16 scale and convert to fp32
                    ushort scaleHalf = br.ReadUInt16();
                    float scale = HalfToFloat(scaleHalf);

                    // Read 4 bytes of high bits (32 bits, 1 per value)
                    uint highBits = br.ReadUInt32();

                    // Read 16 bytes (32 x 4-bit low nibbles packed)
                    byte[] lowNibbles = new byte[16];
                    br.Read(lowNibbles, 0, 16);

                    int blockStart = blockIdx * GgufBlockSize;
                    int blockEnd = Math.Min(blockStart + GgufBlockSize, totalElements);

                    // Dequantize the block
                    for (int i = blockStart; i < blockEnd; i++)
                    {
                        int localIdx = i - blockStart;
                        int byteIdx = localIdx / 2;
                        byte packedByte = lowNibbles[byteIdx];

                        // Extract low 4 bits
                        byte low4 = (localIdx % 2 == 0)
                            ? (byte)(packedByte & 0xF)
                            : (byte)((packedByte >> 4) & 0xF);

                        // Extract high bit (5th bit)
                        uint highBit = (highBits >> localIdx) & 1;

                        // Combine to 5-bit value: [high bit][low 4 bits]
                        int value5bit = (int)((highBit << 4) | low4);

                        // Convert to signed using two's complement (range -16 to 15)
                        int signedValue = (value5bit < 16) ? value5bit : value5bit - 32;

                        // Dequantize
                        floatData[i] = signedValue * scale;
                    }
                }
            }

            return floatData;
        }
    }

    /// <summary>
    /// Decoder for Q5_1 tensor type (5-bit with min/max).
    /// GGUF Q5_1 format: block_size=32, each block has fp16 scale + fp16 min + 4 bytes high bits + 16 bytes low nibbles.
    /// </summary>
    internal sealed class Q5_1Decoder : TensorDecoderBase
    {
        public override bool CanDecode(GgufTensorType type)
        {
            return type == GgufTensorType.Q5_1;
        }

        public override float[] Decode(GgufTensorInfo tensorInfo, byte[] rawData)
        {
            int totalElements = CalculateTotalElements(tensorInfo.Dimensions);
            int numBlocks = (totalElements + GgufBlockSize - 1) / GgufBlockSize;

            var floatData = new float[totalElements];

            using (var ms = new MemoryStream(rawData))
            using (var br = new BinaryReader(ms))
            {
                for (int blockIdx = 0; blockIdx < numBlocks; blockIdx++)
                {
                    // Read fp16 scale and convert to fp32
                    ushort scaleHalf = br.ReadUInt16();
                    float scale = HalfToFloat(scaleHalf);

                    // Read fp16 min and convert to fp32
                    ushort minHalf = br.ReadUInt16();
                    float min = HalfToFloat(minHalf);

                    // Read 4 bytes of high bits (32 bits, 1 per value)
                    uint highBits = br.ReadUInt32();

                    // Read 16 bytes (32 x 4-bit low nibbles packed)
                    byte[] lowNibbles = new byte[16];
                    br.Read(lowNibbles, 0, 16);

                    int blockStart = blockIdx * GgufBlockSize;
                    int blockEnd = Math.Min(blockStart + GgufBlockSize, totalElements);

                    // Dequantize the block
                    for (int i = blockStart; i < blockEnd; i++)
                    {
                        int localIdx = i - blockStart;
                        int byteIdx = localIdx / 2;
                        byte packedByte = lowNibbles[byteIdx];

                        // Extract low 4 bits
                        byte low4 = (localIdx % 2 == 0)
                            ? (byte)(packedByte & 0xF)
                            : (byte)((packedByte >> 4) & 0xF);

                        // Extract high bit (5th bit)
                        uint highBit = (highBits >> localIdx) & 1;

                        // Combine to 5-bit unsigned value: [high bit][low 4 bits]
                        uint value5bit = (highBit << 4) | low4;

                        // Dequantize with scale and min
                        floatData[i] = scale * value5bit + min;
                    }
                }
            }

            return floatData;
        }
    }

    /// <summary>
    /// Decoder for Q4_K tensor type (K-quant 4-bit).
    /// Block size: 256 values per super-block (8 sub-blocks of 32 values).
    /// Bytes per block: 144 (2 + 2 + 12 + 128).
    /// </summary>
    internal sealed class Q4KDecoder : TensorDecoderBase
    {
        private const int BYTES_PER_BLOCK = 144;
        private const int SUB_BLOCK_COUNT = 8;
        private const int SUB_BLOCK_SIZE = 32;

        public override bool CanDecode(GgufTensorType type)
        {
            return type == GgufTensorType.Q4_K;
        }

        public override float[] Decode(GgufTensorInfo tensorInfo, byte[] rawData)
        {
            int totalElements = CalculateTotalElements(tensorInfo.Dimensions);
            if (totalElements % KQuantBlockSize != 0)
                throw new ArgumentException($"Q4_K tensor elements must be divisible by {KQuantBlockSize}");

            int numBlocks = totalElements / KQuantBlockSize;
            int expectedSize = numBlocks * BYTES_PER_BLOCK;
            ValidateDataSize(rawData, expectedSize, "Q4_K");

            var floatData = new float[totalElements];

            // Allocate buffers outside the loop to avoid stack overflow
            byte[] scales = new byte[SUB_BLOCK_COUNT];
            byte[] mins = new byte[SUB_BLOCK_COUNT];

            using (var ms = new MemoryStream(rawData))
            using (var br = new BinaryReader(ms))
            {
                for (int blockIdx = 0; blockIdx < numBlocks; blockIdx++)
                {
                    int dstOffset = blockIdx * KQuantBlockSize;

                    // Read super-block scale and min (fp16)
                    ushort dBits = br.ReadUInt16();
                    ushort dminBits = br.ReadUInt16();
                    float d = HalfToFloat(dBits);
                    float dmin = HalfToFloat(dminBits);

                    // Read scales (12 bytes encoding 6-bit scales and mins for 8 sub-blocks)
                    byte[] scalesBytes = br.ReadBytes(12);
                    
                    // Read quantized values (128 bytes, 2 values per byte)
                    byte[] qs = br.ReadBytes(128);

                    // Extract 6-bit scales and mins from 12-byte packed field
                    // Unpack scales (first 6 bytes)
                    scales[0] = (byte)(scalesBytes[0] & 0x3F);
                    scales[1] = (byte)((scalesBytes[0] >> 6) | ((scalesBytes[1] & 0x0F) << 2));
                    scales[2] = (byte)((scalesBytes[1] >> 4) | ((scalesBytes[2] & 0x03) << 4));
                    scales[3] = (byte)((scalesBytes[2] >> 2) & 0x3F);
                    scales[4] = (byte)(scalesBytes[3] & 0x3F);
                    scales[5] = (byte)((scalesBytes[3] >> 6) | ((scalesBytes[4] & 0x0F) << 2));
                    scales[6] = (byte)((scalesBytes[4] >> 4) | ((scalesBytes[5] & 0x03) << 4));
                    scales[7] = (byte)((scalesBytes[5] >> 2) & 0x3F);
                    
                    // Unpack mins (last 6 bytes)
                    mins[0] = (byte)(scalesBytes[6] & 0x3F);
                    mins[1] = (byte)((scalesBytes[6] >> 6) | ((scalesBytes[7] & 0x0F) << 2));
                    mins[2] = (byte)((scalesBytes[7] >> 4) | ((scalesBytes[8] & 0x03) << 4));
                    mins[3] = (byte)((scalesBytes[8] >> 2) & 0x3F);
                    mins[4] = (byte)(scalesBytes[9] & 0x3F);
                    mins[5] = (byte)((scalesBytes[9] >> 6) | ((scalesBytes[10] & 0x0F) << 2));
                    mins[6] = (byte)((scalesBytes[10] >> 4) | ((scalesBytes[11] & 0x03) << 4));
                    mins[7] = (byte)((scalesBytes[11] >> 2) & 0x3F);

                    // Decode each sub-block
                    for (int subBlock = 0; subBlock < SUB_BLOCK_COUNT; subBlock++)
                    {
                        float sc = d * scales[subBlock];
                        float m = dmin * mins[subBlock];
                        int subBlockDstOffset = dstOffset + subBlock * SUB_BLOCK_SIZE;
                        int qsOffset = subBlock * (SUB_BLOCK_SIZE / 2); // 16 bytes per sub-block

                        for (int i = 0; i < SUB_BLOCK_SIZE / 2; i++)
                        {
                            byte packed = qs[qsOffset + i];
                            int q0 = packed & 0xF;
                            int q1 = (packed >> 4) & 0xF;

                            floatData[subBlockDstOffset + i * 2] = sc * q0 - m;
                            floatData[subBlockDstOffset + i * 2 + 1] = sc * q1 - m;
                        }
                    }
                }
            }

            return floatData;
        }
    }

    /// <summary>
    /// Decoder for Q5_K tensor type (K-quant 5-bit).
    /// Block size: 256 values per super-block (8 sub-blocks of 32 values).
    /// Bytes per block: 176 (2 + 2 + 12 + 32 + 128).
    /// </summary>
    internal sealed class Q5KDecoder : TensorDecoderBase
    {
        private const int BYTES_PER_BLOCK = 176;
        private const int SUB_BLOCK_COUNT = 8;
        private const int SUB_BLOCK_SIZE = 32;

        public override bool CanDecode(GgufTensorType type)
        {
            return type == GgufTensorType.Q5_K;
        }

        public override float[] Decode(GgufTensorInfo tensorInfo, byte[] rawData)
        {
            int totalElements = CalculateTotalElements(tensorInfo.Dimensions);
            if (totalElements % KQuantBlockSize != 0)
                throw new ArgumentException($"Q5_K tensor elements must be divisible by {KQuantBlockSize}");

            int numBlocks = totalElements / KQuantBlockSize;
            int expectedSize = numBlocks * BYTES_PER_BLOCK;
            ValidateDataSize(rawData, expectedSize, "Q5_K");

            var floatData = new float[totalElements];

            // Allocate buffers outside the loop to avoid stack overflow
            byte[] scales = new byte[SUB_BLOCK_COUNT];
            byte[] mins = new byte[SUB_BLOCK_COUNT];

            using (var ms = new MemoryStream(rawData))
            using (var br = new BinaryReader(ms))
            {
                for (int blockIdx = 0; blockIdx < numBlocks; blockIdx++)
                {
                    int dstOffset = blockIdx * KQuantBlockSize;

                    // Read super-block scale and min (fp16)
                    ushort dBits = br.ReadUInt16();
                    ushort dminBits = br.ReadUInt16();
                    float d = HalfToFloat(dBits);
                    float dmin = HalfToFloat(dminBits);

                    // Read scales (12 bytes encoding 6-bit scales and mins for 8 sub-blocks)
                    byte[] scalesBytes = br.ReadBytes(12);
                    
                    // Read high bits (32 bytes, 1 bit per value)
                    byte[] qh = br.ReadBytes(32);
                    
                    // Read low 4-bit quantized values (128 bytes, 2 values per byte)
                    byte[] qs = br.ReadBytes(128);

                    // Extract 6-bit scales and mins from 12-byte packed field
                    // Unpack scales (first 6 bytes)
                    scales[0] = (byte)(scalesBytes[0] & 0x3F);
                    scales[1] = (byte)((scalesBytes[0] >> 6) | ((scalesBytes[1] & 0x0F) << 2));
                    scales[2] = (byte)((scalesBytes[1] >> 4) | ((scalesBytes[2] & 0x03) << 4));
                    scales[3] = (byte)((scalesBytes[2] >> 2) & 0x3F);
                    scales[4] = (byte)(scalesBytes[3] & 0x3F);
                    scales[5] = (byte)((scalesBytes[3] >> 6) | ((scalesBytes[4] & 0x0F) << 2));
                    scales[6] = (byte)((scalesBytes[4] >> 4) | ((scalesBytes[5] & 0x03) << 4));
                    scales[7] = (byte)((scalesBytes[5] >> 2) & 0x3F);
                    
                    // Unpack mins (last 6 bytes)
                    mins[0] = (byte)(scalesBytes[6] & 0x3F);
                    mins[1] = (byte)((scalesBytes[6] >> 6) | ((scalesBytes[7] & 0x0F) << 2));
                    mins[2] = (byte)((scalesBytes[7] >> 4) | ((scalesBytes[8] & 0x03) << 4));
                    mins[3] = (byte)((scalesBytes[8] >> 2) & 0x3F);
                    mins[4] = (byte)(scalesBytes[9] & 0x3F);
                    mins[5] = (byte)((scalesBytes[9] >> 6) | ((scalesBytes[10] & 0x0F) << 2));
                    mins[6] = (byte)((scalesBytes[10] >> 4) | ((scalesBytes[11] & 0x03) << 4));
                    mins[7] = (byte)((scalesBytes[11] >> 2) & 0x3F);

                    // Decode each sub-block
                    for (int subBlock = 0; subBlock < SUB_BLOCK_COUNT; subBlock++)
                    {
                        float sc = d * scales[subBlock];
                        float m = dmin * mins[subBlock];
                        int subBlockDstOffset = dstOffset + subBlock * SUB_BLOCK_SIZE;
                        int qsOffset = subBlock * (SUB_BLOCK_SIZE / 2); // 16 bytes per sub-block
                        int qhOffset = subBlock * 4; // 4 bytes of high bits per sub-block

                        for (int i = 0; i < SUB_BLOCK_SIZE / 2; i++)
                        {
                            byte packedLow = qs[qsOffset + i];
                            byte low0 = (byte)(packedLow & 0xF);
                            byte low1 = (byte)((packedLow >> 4) & 0xF);

                            // Extract high bits (1 bit per value from qh)
                            int bitIdx0 = i * 2;
                            int bitIdx1 = i * 2 + 1;
                            int byteIdx0 = qhOffset + (bitIdx0 / 8);
                            int byteIdx1 = qhOffset + (bitIdx1 / 8);
                            byte high0 = (byte)((qh[byteIdx0] >> (bitIdx0 % 8)) & 1);
                            byte high1 = (byte)((qh[byteIdx1] >> (bitIdx1 % 8)) & 1);

                            // Combine to 5-bit values
                            int q0 = low0 | (high0 << 4);
                            int q1 = low1 | (high1 << 4);

                            floatData[subBlockDstOffset + i * 2] = sc * q0 - m;
                            floatData[subBlockDstOffset + i * 2 + 1] = sc * q1 - m;
                        }
                    }
                }
            }

            return floatData;
        }
    }

    /// <summary>
    /// Decoder for Q6_K tensor type (K-quant 6-bit).
    /// Block size: 256 values per super-block (16 sub-blocks of 16 values).
    /// Bytes per block: 210 (128 + 64 + 16 + 2).
    /// </summary>
    internal sealed class Q6KDecoder : TensorDecoderBase
    {
        private const int BYTES_PER_BLOCK = 210;
        private const int SUB_BLOCK_COUNT = 16;
        private const int SUB_BLOCK_SIZE = 16;

        public override bool CanDecode(GgufTensorType type)
        {
            return type == GgufTensorType.Q6_K;
        }

        public override float[] Decode(GgufTensorInfo tensorInfo, byte[] rawData)
        {
            int totalElements = CalculateTotalElements(tensorInfo.Dimensions);
            if (totalElements % KQuantBlockSize != 0)
                throw new ArgumentException($"Q6_K tensor elements must be divisible by {KQuantBlockSize}");

            int numBlocks = totalElements / KQuantBlockSize;
            int expectedSize = numBlocks * BYTES_PER_BLOCK;
            ValidateDataSize(rawData, expectedSize, "Q6_K");

            var floatData = new float[totalElements];

            using (var ms = new MemoryStream(rawData))
            using (var br = new BinaryReader(ms))
            {
                for (int blockIdx = 0; blockIdx < numBlocks; blockIdx++)
                {
                    int dstOffset = blockIdx * KQuantBlockSize;

                    // Read ql (128 bytes - low 4 bits of 6-bit values)
                    byte[] ql = br.ReadBytes(128);

                    // Read qh (64 bytes - high 2 bits of 6-bit values)
                    byte[] qh = br.ReadBytes(64);

                    // Read scales (16 bytes - int8 per sub-block)
                    sbyte[] scales = new sbyte[16];
                    for (int i = 0; i < 16; i++)
                    {
                        scales[i] = br.ReadSByte();
                    }

                    // Read super-block scale d (fp16)
                    ushort dBits = br.ReadUInt16();
                    float d = HalfToFloat(dBits);

                    // Decode each sub-block (16 sub-blocks of 16 values each)
                    for (int subBlock = 0; subBlock < SUB_BLOCK_COUNT; subBlock++)
                    {
                        float sc = d * scales[subBlock];
                        int subBlockDstOffset = dstOffset + subBlock * SUB_BLOCK_SIZE;

                        // Decode 16 values in this sub-block
                        for (int i = 0; i < SUB_BLOCK_SIZE; i++)
                        {
                            int valueIdx = subBlock * SUB_BLOCK_SIZE + i;
                            
                            // Reconstruct 6-bit value from low 4 bits (ql) and high 2 bits (qh)
                            // ql packs 2 values per byte: even values in low nibble, odd in high nibble
                            int qlIdx = valueIdx / 2;
                            byte qlByte = ql[qlIdx];
                            byte low4 = (valueIdx % 2 == 0) ? (byte)(qlByte & 0xF) : (byte)((qlByte >> 4) & 0xF);
                            
                            // Extract high 2 bits from qh (4 values per byte)
                            int qhIdx = valueIdx / 4;
                            int qhShift = (valueIdx % 4) * 2;
                            byte high2 = (byte)((qh[qhIdx] >> qhShift) & 0x3);
                            
                            // Combine to form 6-bit value (range 0-63)
                            int q = low4 | (high2 << 4);
                            
                            // Dequantize: center around 0 with -32 bias
                            floatData[subBlockDstOffset + i] = sc * (q - 32);
                        }
                    }
                }
            }

            return floatData;
        }
    }

    /// <summary>
    /// Decoder for Q8_K tensor type (K-quant 8-bit).
    /// This is a HIGH PRIORITY decoder - used in ~10% of high-quality models.
    /// Block size: 256 values per super-block (8 sub-blocks of 32 values).
    /// Bytes per block: 292 (2 + 2 + 32 + 256).
    /// </summary>
    internal sealed class Q8KDecoder : TensorDecoderBase
    {
        private const int BYTES_PER_BLOCK = 292;
        private const int SUB_BLOCK_COUNT = 8;
        private const int SUB_BLOCK_SIZE = 32;

        public override bool CanDecode(GgufTensorType type)
        {
            return type == GgufTensorType.Q8_K;
        }

        public override float[] Decode(GgufTensorInfo tensorInfo, byte[] rawData)
        {
            int totalElements = CalculateTotalElements(tensorInfo.Dimensions);
            if (totalElements % KQuantBlockSize != 0)
                throw new ArgumentException($"Q8_K tensor elements must be divisible by {KQuantBlockSize}");

            int numBlocks = totalElements / KQuantBlockSize;
            int expectedSize = numBlocks * BYTES_PER_BLOCK;
            ValidateDataSize(rawData, expectedSize, "Q8_K");

            var floatData = new float[totalElements];

            using (var ms = new MemoryStream(rawData))
            using (var br = new BinaryReader(ms))
            {
                for (int blockIdx = 0; blockIdx < numBlocks; blockIdx++)
                {
                    int dstOffset = blockIdx * KQuantBlockSize;

                    // Read super-block scale (fp16)
                    ushort dBits = br.ReadUInt16();
                    float d = HalfToFloat(dBits);

                    // Read super-block min (fp16)  
                    ushort dminBits = br.ReadUInt16();
                    float dmin = HalfToFloat(dminBits);

                    // Read scales for 8 sub-blocks (fp16 each, 16 bytes total)
                    float[] scales = new float[SUB_BLOCK_COUNT];
                    for (int i = 0; i < SUB_BLOCK_COUNT; i++)
                    {
                        ushort scaleBits = br.ReadUInt16();
                        scales[i] = HalfToFloat(scaleBits);
                    }

                    // Read quantized values (256 bytes, 1 per value)
                    sbyte[] qs = new sbyte[KQuantBlockSize];
                    for (int i = 0; i < KQuantBlockSize; i++)
                    {
                        qs[i] = br.ReadSByte();
                    }

                    // Decode each sub-block
                    for (int subBlock = 0; subBlock < SUB_BLOCK_COUNT; subBlock++)
                    {
                        float sc = d * scales[subBlock];
                        int subBlockDstOffset = dstOffset + subBlock * SUB_BLOCK_SIZE;
                        int qsOffset = subBlock * SUB_BLOCK_SIZE;

                        // Dequantize 32 values in this sub-block
                        for (int i = 0; i < SUB_BLOCK_SIZE; i++)
                        {
                            floatData[subBlockDstOffset + i] = sc * qs[qsOffset + i] - dmin;
                        }
                    }
                }
            }

            return floatData;
        }
    }
}
