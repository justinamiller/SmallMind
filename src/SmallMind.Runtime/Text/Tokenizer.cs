using SmallMind.Tokenizers;
using SmallMind.Core.Core;
using SmallMind.Transformers;
namespace SmallMind.Runtime
{
    /// <summary>
    /// Character-level tokenizer. Builds a vocabulary from the training text
    /// and provides encode/decode methods to convert between strings and token IDs.
    /// This is a type alias for CharTokenizer for backwards compatibility.
    /// </summary>
    public class Tokenizer : CharTokenizer
    {
        /// <summary>
        /// Creates a new Tokenizer (CharTokenizer) from the given training text.
        /// </summary>
        /// <param name="text">Training text to extract vocabulary from</param>
        public Tokenizer(string text) : base(text)
        {
        }
    }
}
