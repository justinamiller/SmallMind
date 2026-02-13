using System;
using SmallMind.Quantization.IO.Gguf;

namespace SmallMind.Runtime.Gguf.TensorDecoders
{
    /// <summary>
    /// Decoder for Q4_1 tensor type (4-bit with min/max).
    /// </summary>
    internal sealed class Q4_1Decoder : TensorDecoderBase
    {
        public override bool CanDecode(GgufTensorType type)
        {
            return type == GgufTensorType.Q4_1;
        }

        public override float[] Decode(GgufTensorInfo tensorInfo, byte[] rawData)
        {
            throw new NotImplementedException("Q4_1 decoder not yet implemented");
        }
    }

    /// <summary>
    /// Decoder for Q5_0 tensor type (5-bit symmetric quantization).
    /// </summary>
    internal sealed class Q5_0Decoder : TensorDecoderBase
    {
        public override bool CanDecode(GgufTensorType type)
        {
            return type == GgufTensorType.Q5_0;
        }

        public override float[] Decode(GgufTensorInfo tensorInfo, byte[] rawData)
        {
            throw new NotImplementedException("Q5_0 decoder not yet implemented");
        }
    }

    /// <summary>
    /// Decoder for Q5_1 tensor type (5-bit with min/max).
    /// </summary>
    internal sealed class Q5_1Decoder : TensorDecoderBase
    {
        public override bool CanDecode(GgufTensorType type)
        {
            return type == GgufTensorType.Q5_1;
        }

        public override float[] Decode(GgufTensorInfo tensorInfo, byte[] rawData)
        {
            throw new NotImplementedException("Q5_1 decoder not yet implemented");
        }
    }

    /// <summary>
    /// Decoder for Q4_K tensor type (K-quant 4-bit).
    /// </summary>
    internal sealed class Q4KDecoder : TensorDecoderBase
    {
        public override bool CanDecode(GgufTensorType type)
        {
            return type == GgufTensorType.Q4_K;
        }

        public override float[] Decode(GgufTensorInfo tensorInfo, byte[] rawData)
        {
            throw new NotImplementedException("Q4_K decoder not yet implemented");
        }
    }

    /// <summary>
    /// Decoder for Q5_K tensor type (K-quant 5-bit).
    /// </summary>
    internal sealed class Q5KDecoder : TensorDecoderBase
    {
        public override bool CanDecode(GgufTensorType type)
        {
            return type == GgufTensorType.Q5_K;
        }

        public override float[] Decode(GgufTensorInfo tensorInfo, byte[] rawData)
        {
            throw new NotImplementedException("Q5_K decoder not yet implemented");
        }
    }

    /// <summary>
    /// Decoder for Q6_K tensor type (K-quant 6-bit).
    /// </summary>
    internal sealed class Q6KDecoder : TensorDecoderBase
    {
        public override bool CanDecode(GgufTensorType type)
        {
            return type == GgufTensorType.Q6_K;
        }

        public override float[] Decode(GgufTensorInfo tensorInfo, byte[] rawData)
        {
            throw new NotImplementedException("Q6_K decoder not yet implemented");
        }
    }

    /// <summary>
    /// Decoder for Q8_K tensor type (K-quant 8-bit).
    /// This is a HIGH PRIORITY decoder that needs to be implemented.
    /// </summary>
    internal sealed class Q8KDecoder : TensorDecoderBase
    {
        public override bool CanDecode(GgufTensorType type)
        {
            return type == GgufTensorType.Q8_K;
        }

        public override float[] Decode(GgufTensorInfo tensorInfo, byte[] rawData)
        {
            throw new NotImplementedException(
                "Q8_K decoder not yet implemented. This is a HIGH PRIORITY decoder - see GGUF_TENSOR_COMPATIBILITY_MATRIX.md");
        }
    }
}
