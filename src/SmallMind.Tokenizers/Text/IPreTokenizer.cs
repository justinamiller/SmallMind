namespace SmallMind.Tokenizers;

/// <summary>
/// Interface for pre-tokenization strategies.
/// Pre-tokenizers split input text into chunks before applying BPE.
/// This allows implementing GPT-2 style regex-based splitting in the future.
/// </summary>
public interface IPreTokenizer
{
    /// <summary>
    /// Split input text into pre-tokenization chunks.
    /// </summary>
    /// <param name="text">Input text to split</param>
    /// <returns>Array of text chunks to tokenize separately</returns>
    string[] Split(string text);
}
