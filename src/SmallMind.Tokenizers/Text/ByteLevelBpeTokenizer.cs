using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SmallMind.Tokenizers;

/// <summary>
/// Byte-level BPE tokenizer with training capability.
/// Operates on raw UTF-8 byte sequences (0-255) with GPT-2 style byte-to-unicode mapping.
/// Supports training from text, encoding, decoding, and vocabulary persistence.
/// </summary>
internal sealed class ByteLevelBpeTokenizer : ITokenizer
{
    // Base vocabulary: 256 bytes
    private const int BaseVocabSize = 256;
    
    // Special token IDs (fixed positions after base vocab)
    private const int PadTokenIdValue = 256;
    private const int UnkTokenIdValue = 257;
    private const int BosTokenIdValue = 258;
    private const int EosTokenIdValue = 259;
    private const int FirstMergeTokenId = 260;

    // Byte-to-unicode mapping (GPT-2 style - maps bytes to printable unicode)
    private readonly Dictionary<int, int> _byteToUnicode;
    private readonly Dictionary<int, byte> _unicodeToByte;

    // Vocabulary: token ID -> byte sequence
    private readonly Dictionary<int, byte[]> _tokenToBytes;

    // Merge rules: ordered list of (tokenA, tokenB) pairs
    // Index in list = priority (lower index = higher priority, applied first)
    private readonly List<(int, int)> _merges;

    // Fast lookup: (tokenA, tokenB) -> newTokenId
    private readonly Dictionary<(int, int), int> _mergeDict;

    private readonly IPreTokenizer _preTokenizer;

    public int VocabSize { get; private set; }
    public TokenizerInfo Info { get; private set; }

    // Implement ITokenizer special token properties via Info
    public int PadTokenId => Info.PadTokenId;
    public int BosTokenId => Info.BosTokenId;
    public int EosTokenId => Info.EosTokenId;
    public int UnkTokenId => Info.UnkTokenId;

    /// <summary>
    /// Creates a new ByteLevelBpeTokenizer with only the base 256-byte vocabulary and special tokens.
    /// Call Train() to learn merge rules from training data.
    /// </summary>
    public ByteLevelBpeTokenizer(IPreTokenizer? preTokenizer = null)
    {
        _preTokenizer = preTokenizer ?? NoOpPreTokenizer.Instance;
        _byteToUnicode = new Dictionary<int, int>();
        _unicodeToByte = new Dictionary<int, byte>();
        _tokenToBytes = new Dictionary<int, byte[]>();
        _merges = new List<(int, int)>();
        _mergeDict = new Dictionary<(int, int), int>();

        InitializeByteMappings();
        InitializeBaseVocabulary();

        VocabSize = FirstMergeTokenId;
        Info = CreateTokenizerInfo();
    }

    /// <summary>
    /// Initialize GPT-2 style byte-to-unicode mapping.
    /// Maps bytes to printable Unicode characters to avoid control characters in vocabulary.
    /// </summary>
    private void InitializeByteMappings()
    {
        // Printable ASCII and extended characters
        int n = 0;
        for (int b = 0; b < 256; b++)
        {
            // Directly usable ranges (printable ASCII-ish)
            if ((b >= 33 && b <= 126) || (b >= 161 && b <= 172) || (b >= 174 && b <= 255))
            {
                _byteToUnicode[b] = b;
                _unicodeToByte[b] = (byte)b;
            }
            else
            {
                // Map to high Unicode range (256+)
                int unicodePoint = 256 + n;
                _byteToUnicode[b] = unicodePoint;
                _unicodeToByte[unicodePoint] = (byte)b;
                n++;
            }
        }
    }

    /// <summary>
    /// Initialize base vocabulary with 256 byte tokens + 4 special tokens.
    /// </summary>
    private void InitializeBaseVocabulary()
    {
        // Base 256 byte tokens (0-255)
        for (int i = 0; i < BaseVocabSize; i++)
        {
            _tokenToBytes[i] = new byte[] { (byte)i };
        }

        // Special tokens (256-259)
        _tokenToBytes[PadTokenIdValue] = Encoding.UTF8.GetBytes("<|pad|>");
        _tokenToBytes[UnkTokenIdValue] = Encoding.UTF8.GetBytes("<|unk|>");
        _tokenToBytes[BosTokenIdValue] = Encoding.UTF8.GetBytes("<|bos|>");
        _tokenToBytes[EosTokenIdValue] = Encoding.UTF8.GetBytes("<|eos|>");
    }

    /// <summary>
    /// Train the tokenizer on the provided text to learn BPE merge rules.
    /// </summary>
    /// <param name="trainingText">Text to train on</param>
    /// <param name="targetVocabSize">Target vocabulary size (must be >= 260)</param>
    /// <param name="minFrequency">Minimum frequency for a pair to be merged (default: 2)</param>
    public void Train(string trainingText, int targetVocabSize, int minFrequency = 2)
    {
        if (targetVocabSize < FirstMergeTokenId)
        {
            throw new ArgumentException(
                $"Target vocabulary size must be at least {FirstMergeTokenId} " +
                $"(256 bytes + 4 special tokens). Got: {targetVocabSize}",
                nameof(targetVocabSize));
        }

        if (string.IsNullOrEmpty(trainingText))
        {
            throw new ArgumentException("Training text cannot be null or empty", nameof(trainingText));
        }

        Console.WriteLine($"Training BPE tokenizer on {trainingText.Length} characters...");
        Console.WriteLine($"Target vocabulary size: {targetVocabSize}");

        // Clear any existing merges
        _merges.Clear();
        _mergeDict.Clear();

        // Convert training text to UTF-8 bytes
        byte[] utf8Bytes = Encoding.UTF8.GetBytes(trainingText);
        
        // Initialize token sequence: each byte is a token
        int[] tokens = new int[utf8Bytes.Length];
        for (int i = 0; i < utf8Bytes.Length; i++)
        {
            tokens[i] = utf8Bytes[i];
        }

        int currentVocabSize = FirstMergeTokenId;

        // Iteratively find and merge the most frequent pair
        while (currentVocabSize < targetVocabSize)
        {
            // Count all adjacent pairs
            var pairCounts = new Dictionary<long, int>();
            var counter = new BpeMergeEngine.PairCounter();
            counter.CountPairs(tokens, pairCounts);

            if (pairCounts.Count == 0)
            {
                Console.WriteLine("No more pairs to merge.");
                break;
            }

            // Find most frequent pair
            var (tokenA, tokenB, frequency) = BpeMergeEngine.PairCounter.FindMostFrequentPair(pairCounts);

            if (frequency < minFrequency)
            {
                Console.WriteLine($"Most frequent pair has frequency {frequency} < {minFrequency}. Stopping.");
                break;
            }

            // Create new token ID for this merge
            int newTokenId = currentVocabSize;

            // Record the merge rule
            _merges.Add((tokenA, tokenB));
            _mergeDict[(tokenA, tokenB)] = newTokenId;

            // Update vocabulary: new token = concatenation of byte sequences
            byte[] bytesA = _tokenToBytes[tokenA];
            byte[] bytesB = _tokenToBytes[tokenB];
            byte[] mergedBytes = new byte[bytesA.Length + bytesB.Length];
            Array.Copy(bytesA, 0, mergedBytes, 0, bytesA.Length);
            Array.Copy(bytesB, 0, mergedBytes, bytesA.Length, bytesB.Length);
            _tokenToBytes[newTokenId] = mergedBytes;

            // Apply merge to token sequence
            int newLength = ApplyMergeToSequence(tokens, tokens.Length, tokenA, tokenB, newTokenId);
            Array.Resize(ref tokens, newLength);

            currentVocabSize++;

            if (currentVocabSize % 100 == 0 || currentVocabSize == targetVocabSize)
            {
                Console.WriteLine(
                    $"Vocab size: {currentVocabSize}, " +
                    $"Last merge: ({tokenA},{tokenB})->{newTokenId}, " +
                    $"Frequency: {frequency}");
            }
        }

        VocabSize = currentVocabSize;
        Info = CreateTokenizerInfo();

        Console.WriteLine($"Training complete. Final vocabulary size: {VocabSize}");
        Console.WriteLine($"Learned {_merges.Count} merge rules.");
    }

    /// <summary>
    /// Apply a single merge to a token sequence in-place.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ApplyMergeToSequence(
        Span<int> tokens,
        int currentLength,
        int tokenA,
        int tokenB,
        int newTokenId)
    {
        int writePos = 0;
        int readPos = 0;

        while (readPos < currentLength)
        {
            if (readPos < currentLength - 1 &&
                tokens[readPos] == tokenA &&
                tokens[readPos + 1] == tokenB)
            {
                // Found pair - replace with merged token
                tokens[writePos++] = newTokenId;
                readPos += 2;
            }
            else
            {
                // Copy token as-is
                if (writePos != readPos)
                {
                    tokens[writePos] = tokens[readPos];
                }
                writePos++;
                readPos++;
            }
        }

        return writePos;
    }

    /// <summary>
    /// Encode a string into token IDs.
    /// </summary>
    public List<int> Encode(string text)
    {
        if (string.IsNullOrEmpty(text))
            return new List<int>();

        byte[] utf8Bytes = Encoding.UTF8.GetBytes(text);
        int[] buffer = ArrayPool<int>.Shared.Rent(utf8Bytes.Length * 2);

        try
        {
            int count = Encode(utf8Bytes, buffer);
            var result = new List<int>(count);
            for (int i = 0; i < count; i++)
            {
                result.Add(buffer[i]);
            }
            return result;
        }
        finally
        {
            ArrayPool<int>.Shared.Return(buffer);
        }
    }

    /// <summary>
    /// Encode UTF-8 bytes into token IDs (fast path).
    /// </summary>
    public int Encode(ReadOnlySpan<byte> utf8, Span<int> tokensOut)
    {
        if (utf8.Length == 0)
            return 0;

        // Start with each byte as a token
        int[] symbols = ArrayPool<int>.Shared.Rent(utf8.Length);

        try
        {
            for (int i = 0; i < utf8.Length; i++)
            {
                symbols[i] = utf8[i];
            }

            int symbolCount = utf8.Length;

            // Apply merges in priority order (iterative approach)
            symbolCount = ApplyMergesIterative(symbols.AsSpan(), symbolCount);

            // Copy result to output
            int count = Math.Min(symbolCount, tokensOut.Length);
            symbols.AsSpan(0, count).CopyTo(tokensOut);
            return count;
        }
        finally
        {
            ArrayPool<int>.Shared.Return(symbols);
        }
    }

    /// <summary>
    /// Apply BPE merges: find pairs in merge dict, apply in greedy left-to-right order.
    /// Uses O(1) dictionary lookup for efficiency.
    /// </summary>
    private int ApplyMergesIterative(Span<int> tokens, int currentLength)
    {
        if (_mergeDict.Count == 0 || currentLength < 2)
            return currentLength;

        bool changed = true;
        while (changed && currentLength > 1)
        {
            changed = false;
            int i = 0;
            
            while (i < currentLength - 1)
            {
                var pair = (tokens[i], tokens[i + 1]);
                
                // Check if this pair has a merge rule (O(1) lookup)
                if (_mergeDict.TryGetValue(pair, out int mergedTokenId))
                {
                    // Replace pair with merged token
                    tokens[i] = mergedTokenId;
                    
                    // Shift remaining tokens left
                    for (int j = i + 1; j < currentLength - 1; j++)
                    {
                        tokens[j] = tokens[j + 1];
                    }
                    currentLength--;
                    changed = true;
                    
                    // Don't increment i - check new pair at same position
                }
                else
                {
                    i++;
                }
            }
        }

        return currentLength;
    }

    /// <summary>
    /// Decode token IDs back into UTF-8 bytes.
    /// </summary>
    public int Decode(ReadOnlySpan<int> tokens, Span<byte> utf8Out)
    {
        if (tokens.Length == 0)
            return 0;

        int bytePos = 0;

        foreach (int tokenId in tokens)
        {
            // Skip special tokens (except for the base byte tokens)
            if (tokenId >= PadTokenIdValue && tokenId < FirstMergeTokenId)
                continue;

            if (!_tokenToBytes.TryGetValue(tokenId, out byte[]? tokenBytes))
            {
                // Unknown token - skip it
                continue;
            }

            // Copy bytes to output
            if (bytePos + tokenBytes.Length > utf8Out.Length)
            {
                // Not enough space - copy what we can
                int remaining = utf8Out.Length - bytePos;
                tokenBytes.AsSpan(0, remaining).CopyTo(utf8Out.Slice(bytePos));
                return utf8Out.Length;
            }

            tokenBytes.CopyTo(utf8Out.Slice(bytePos));
            bytePos += tokenBytes.Length;
        }

        return bytePos;
    }

    /// <summary>
    /// Decode token IDs back into a string.
    /// </summary>
    public string Decode(List<int> tokens)
    {
        if (tokens == null || tokens.Count == 0)
            return string.Empty;

        byte[] buffer = ArrayPool<byte>.Shared.Rent(tokens.Count * 4);
        int[] tokenArray = ArrayPool<int>.Shared.Rent(tokens.Count);

        try
        {
            for (int i = 0; i < tokens.Count; i++)
            {
                tokenArray[i] = tokens[i];
            }

            int byteCount = Decode(tokenArray.AsSpan(0, tokens.Count), buffer);
            return Encoding.UTF8.GetString(buffer, 0, byteCount);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
            ArrayPool<int>.Shared.Return(tokenArray);
        }
    }

    /// <summary>
    /// Decode token IDs to string (convenience method).
    /// </summary>
    public string DecodeToString(ReadOnlySpan<int> tokens)
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(tokens.Length * 4);

        try
        {
            int byteCount = Decode(tokens, buffer);
            return Encoding.UTF8.GetString(buffer, 0, byteCount);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>
    /// Save vocabulary and merge rules to a JSON file.
    /// </summary>
    public void SaveVocabulary(string path)
    {
        var vocabDict = new Dictionary<string, int>();

        // Build vocabulary: tokenId -> string representation
        foreach (var kvp in _tokenToBytes)
        {
            int tokenId = kvp.Key;
            byte[] bytes = kvp.Value;

            // Convert bytes to display string using byte-to-unicode mapping
            var sb = new StringBuilder();
            foreach (byte b in bytes)
            {
                int unicodePoint = _byteToUnicode[b];
                sb.Append((char)unicodePoint);
            }

            vocabDict[sb.ToString()] = tokenId;
        }

        // Build merges list
        var mergeStrings = new List<string>();
        foreach (var (tokenA, tokenB) in _merges)
        {
            mergeStrings.Add($"{tokenA} {tokenB}");
        }

        // Create JSON structure
        var vocabData = new VocabularyData
        {
            Version = "smallmind-bpe-v1",
            VocabSize = VocabSize,
            SpecialTokens = new Dictionary<string, int>
            {
                ["<|pad|>"] = PadTokenIdValue,
                ["<|unk|>"] = UnkTokenIdValue,
                ["<|bos|>"] = BosTokenIdValue,
                ["<|eos|>"] = EosTokenIdValue
            },
            Merges = mergeStrings,
            Vocab = vocabDict
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never
        };

        string json = JsonSerializer.Serialize(vocabData, options);
        File.WriteAllText(path, json);

        Console.WriteLine($"Vocabulary saved to: {path}");
        Console.WriteLine($"Vocab size: {VocabSize}, Merges: {_merges.Count}");
    }

    /// <summary>
    /// Load vocabulary and merge rules from a JSON file.
    /// </summary>
    public void LoadVocabulary(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Vocabulary file not found: {path}");
        }

        string json = File.ReadAllText(path);
        var vocabData = JsonSerializer.Deserialize<VocabularyData>(json);

        if (vocabData == null)
        {
            throw new InvalidOperationException($"Failed to deserialize vocabulary from: {path}");
        }

        if (vocabData.Version != "smallmind-bpe-v1")
        {
            throw new InvalidOperationException(
                $"Unsupported vocabulary version: {vocabData.Version}. Expected: smallmind-bpe-v1");
        }

        Console.WriteLine($"Loading vocabulary from: {path}");

        // Clear existing state
        _tokenToBytes.Clear();
        _merges.Clear();
        _mergeDict.Clear();

        // Rebuild base vocabulary
        InitializeBaseVocabulary();

        // Build reverse mapping: string -> tokenId
        var stringToId = new Dictionary<string, int>();
        foreach (var kvp in vocabData.Vocab)
        {
            stringToId[kvp.Key] = kvp.Value;
        }

        // Rebuild token-to-bytes mapping from vocab
        foreach (var kvp in vocabData.Vocab)
        {
            string tokenStr = kvp.Key;
            int tokenId = kvp.Value;

            // Convert string back to bytes using unicode-to-byte mapping
            var bytes = new List<byte>();
            foreach (char c in tokenStr)
            {
                if (_unicodeToByte.TryGetValue(c, out byte b))
                {
                    bytes.Add(b);
                }
                else
                {
                    // Might be a special token or invalid
                    // For special tokens, just use UTF-8 encoding
                    bytes.AddRange(Encoding.UTF8.GetBytes(c.ToString()));
                }
            }

            _tokenToBytes[tokenId] = bytes.ToArray();
        }

        // Rebuild merges
        foreach (string mergeStr in vocabData.Merges)
        {
            string[] parts = mergeStr.Split(' ');
            if (parts.Length == 2 &&
                int.TryParse(parts[0], out int tokenA) &&
                int.TryParse(parts[1], out int tokenB))
            {
                int newTokenId = FirstMergeTokenId + _merges.Count;
                _merges.Add((tokenA, tokenB));
                _mergeDict[(tokenA, tokenB)] = newTokenId;
            }
        }

        VocabSize = vocabData.VocabSize;
        Info = CreateTokenizerInfo();

        Console.WriteLine($"Vocabulary loaded. Size: {VocabSize}, Merges: {_merges.Count}");
    }

    /// <summary>
    /// Save tokenizer to file (implements ITokenizer.Save).
    /// </summary>
    public void Save(string path)
    {
        SaveVocabulary(path);
    }

    /// <summary>
    /// Create TokenizerInfo instance.
    /// </summary>
    private TokenizerInfo CreateTokenizerInfo()
    {
        return new TokenizerInfo(
            name: "ByteLevelBpe",
            vocabSize: VocabSize,
            bosTokenId: BosTokenIdValue,
            eosTokenId: EosTokenIdValue,
            padTokenId: PadTokenIdValue,
            unkTokenId: UnkTokenIdValue,
            supportsByteFallback: true
        );
    }

    /// <summary>
    /// JSON structure for vocabulary persistence.
    /// </summary>
    private sealed class VocabularyData
    {
        [JsonPropertyName("version")]
        public string Version { get; set; } = "smallmind-bpe-v1";

        [JsonPropertyName("vocab_size")]
        public int VocabSize { get; set; }

        [JsonPropertyName("special_tokens")]
        public Dictionary<string, int> SpecialTokens { get; set; } = new();

        [JsonPropertyName("merges")]
        public List<string> Merges { get; set; } = new();

        [JsonPropertyName("vocab")]
        public Dictionary<string, int> Vocab { get; set; } = new();
    }
}
