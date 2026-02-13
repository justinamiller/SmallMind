using System;
using System.Collections.Generic;
using SmallMind.Quantization;
using SmallMind.Quantization.IO.Gguf;

namespace SmallMind.Runtime.Gguf.TensorDecoders
{
    /// <summary>
    /// Registry for managing tensor decoders. Allows lookup of the appropriate decoder
    /// for a given GGUF tensor type.
    /// </summary>
    internal sealed class TensorDecoderRegistry
    {
        private readonly List<ITensorDecoder> _decoders;

        /// <summary>
        /// Initializes a new instance with default decoders registered.
        /// </summary>
        public TensorDecoderRegistry()
        {
            _decoders = new List<ITensorDecoder>();
            RegisterDefaultDecoders();
        }

        /// <summary>
        /// Registers a new decoder in the registry.
        /// </summary>
        /// <param name="decoder">The decoder to register.</param>
        public void Register(ITensorDecoder decoder)
        {
            if (decoder == null)
                throw new ArgumentNullException(nameof(decoder));

            _decoders.Add(decoder);
        }

        /// <summary>
        /// Gets the first decoder that can handle the specified tensor type.
        /// </summary>
        /// <param name="type">GGUF tensor type.</param>
        /// <returns>A decoder capable of handling the tensor type.</returns>
        /// <exception cref="UnsupportedQuantizationException">
        /// Thrown when no decoder is found for the specified type.
        /// </exception>
        public ITensorDecoder GetDecoder(GgufTensorType type)
        {
            foreach (var decoder in _decoders)
            {
                if (decoder.CanDecode(type))
                    return decoder;
            }

            throw new UnsupportedQuantizationException(
                $"No decoder registered for GGUF tensor type: {type}");
        }

        /// <summary>
        /// Checks if any decoder can handle the specified tensor type.
        /// </summary>
        /// <param name="type">GGUF tensor type to check.</param>
        /// <returns>True if a decoder exists for this type, false otherwise.</returns>
        public bool IsSupported(GgufTensorType type)
        {
            foreach (var decoder in _decoders)
            {
                if (decoder.CanDecode(type))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Registers all default (built-in) tensor decoders.
        /// </summary>
        private void RegisterDefaultDecoders()
        {
            // Register decoders in priority order (most common first for efficiency)
            Register(new FloatingPointDecoder());      // F32, F16
            Register(new Q4_0Decoder());               // Q4_0
            Register(new Q8_0Decoder());               // Q8_0
            Register(new Q4_1Decoder());               // Q4_1
            Register(new Q5_0Decoder());               // Q5_0
            Register(new Q5_1Decoder());               // Q5_1
            Register(new Q4KDecoder());                // Q4_K
            Register(new Q5KDecoder());                // Q5_K
            Register(new Q6KDecoder());                // Q6_K
            Register(new Q8KDecoder());                // Q8_K (new!)
        }
    }
}
