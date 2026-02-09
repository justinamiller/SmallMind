namespace SmallMind.Tokenizers;

/// <summary>
/// No-operation pre-tokenizer that returns the input text as a single chunk.
/// This is the default pre-tokenizer for SmallMind's byte-level BPE.
/// </summary>
internal sealed class NoOpPreTokenizer : IPreTokenizer
{
    /// <summary>
    /// Singleton instance of NoOpPreTokenizer.
    /// </summary>
    public static readonly NoOpPreTokenizer Instance = new();

    private NoOpPreTokenizer() { }

    /// <summary>
    /// Returns the input text as a single chunk.
    /// </summary>
    public string[] Split(string text)
    {
        return [text];
    }
}
