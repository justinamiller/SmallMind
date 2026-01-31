using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace SmallMind;

/// <summary>
/// Simple but efficient tokenizer using word-level tokenization.
/// Uses ConcurrentDictionary for thread-safe vocabulary management.
/// </summary>
public class Tokenizer
{
    private readonly ConcurrentDictionary<string, int> _wordToId;
    private readonly ConcurrentDictionary<int, string> _idToWord;
    private int _nextId;
    private readonly object _lockObject = new object();

    public Tokenizer()
    {
        _wordToId = new ConcurrentDictionary<string, int>();
        _idToWord = new ConcurrentDictionary<int, string>();
        _nextId = 0;
    }

    public int[] Tokenize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Array.Empty<int>();

        // Simple word tokenization (split on whitespace and punctuation)
        var words = Regex.Split(text.ToLowerInvariant(), @"\s+")
            .Where(w => !string.IsNullOrWhiteSpace(w))
            .ToArray();

        var tokens = new int[words.Length];
        
        for (int i = 0; i < words.Length; i++)
        {
            tokens[i] = GetOrAddToken(words[i]);
        }

        return tokens;
    }

    public string Detokenize(int[] tokens)
    {
        if (tokens == null || tokens.Length == 0)
            return string.Empty;

        var words = new string[tokens.Length];
        for (int i = 0; i < tokens.Length; i++)
        {
            words[i] = _idToWord.TryGetValue(tokens[i], out var word) ? word : "<unk>";
        }

        return string.Join(" ", words);
    }

    private int GetOrAddToken(string word)
    {
        if (_wordToId.TryGetValue(word, out int id))
            return id;

        lock (_lockObject)
        {
            // Double-check after acquiring lock
            if (_wordToId.TryGetValue(word, out id))
                return id;

            id = _nextId++;
            _wordToId[word] = id;
            _idToWord[id] = word;
            return id;
        }
    }

    public int VocabularySize => _wordToId.Count;
}
