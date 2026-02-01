using System;
using System.Collections.Generic;

namespace SmallMind.Rag.Indexing.Sparse;

/// <summary>
/// In-memory inverted index for BM25 ranking.
/// Maps terms to postings lists containing chunk IDs and term frequencies.
/// </summary>
public sealed class InvertedIndex
{
    private readonly Dictionary<string, PostingsList> _index;
    private readonly Dictionary<string, int> _docLengths;
    private int _totalChunks;
    private double _avgDocLength;

    /// <summary>
    /// Initializes a new instance of the <see cref="InvertedIndex"/> class.
    /// </summary>
    public InvertedIndex()
    {
        _index = new Dictionary<string, PostingsList>();
        _docLengths = new Dictionary<string, int>();
        _totalChunks = 0;
        _avgDocLength = 0.0;
    }

    /// <summary>
    /// Gets the average document length in tokens across all indexed chunks.
    /// </summary>
    public double AvgDocLength => _avgDocLength;

    /// <summary>
    /// Gets the total number of chunks in the index.
    /// </summary>
    public int TotalChunks => _totalChunks;

    /// <summary>
    /// Adds a chunk to the inverted index by tokenizing the text and updating postings.
    /// If the chunk already exists, it is replaced.
    /// </summary>
    /// <param name="chunkId">The unique chunk identifier.</param>
    /// <param name="text">The text content to tokenize and index.</param>
    public void AddChunk(string chunkId, string text)
    {
        if (string.IsNullOrEmpty(chunkId))
            throw new ArgumentNullException(nameof(chunkId));
        
        if (text == null)
            throw new ArgumentNullException(nameof(text));

        // Remove existing chunk if present
        if (_docLengths.ContainsKey(chunkId))
        {
            RemoveChunk(chunkId);
        }

        // Tokenize text
        List<string> tokens = RagTokenizer.Tokenize(text);
        
        if (tokens.Count == 0)
            return;

        // Compute term frequencies
        var termFreqs = new Dictionary<string, int>();
        for (int i = 0; i < tokens.Count; i++)
        {
            string term = tokens[i];
            if (termFreqs.ContainsKey(term))
                termFreqs[term]++;
            else
                termFreqs[term] = 1;
        }

        // Add postings for each unique term
        foreach (var kvp in termFreqs)
        {
            string term = kvp.Key;
            int tf = kvp.Value;

            if (!_index.ContainsKey(term))
            {
                _index[term] = new PostingsList();
            }

            _index[term].Add(chunkId, tf);
        }

        // Update document length
        _docLengths[chunkId] = tokens.Count;
        _totalChunks++;

        // Recompute average document length
        UpdateAvgDocLength();
    }

    /// <summary>
    /// Removes a chunk from the inverted index.
    /// Updates postings lists and document statistics.
    /// </summary>
    /// <param name="chunkId">The chunk identifier to remove.</param>
    public void RemoveChunk(string chunkId)
    {
        if (string.IsNullOrEmpty(chunkId))
            throw new ArgumentNullException(nameof(chunkId));

        if (!_docLengths.ContainsKey(chunkId))
            return;

        // Remove from all postings lists
        var termsToRemove = new List<string>();
        
        foreach (var kvp in _index)
        {
            string term = kvp.Key;
            PostingsList postings = kvp.Value;
            
            postings.Remove(chunkId);
            
            // Mark term for removal if postings list is empty
            if (postings.Count == 0)
            {
                termsToRemove.Add(term);
            }
        }

        // Remove empty terms
        for (int i = 0; i < termsToRemove.Count; i++)
        {
            _index.Remove(termsToRemove[i]);
        }

        // Remove document length
        _docLengths.Remove(chunkId);
        _totalChunks--;

        // Recompute average document length
        UpdateAvgDocLength();
    }

    /// <summary>
    /// Gets the document frequency (number of chunks containing the term).
    /// </summary>
    /// <param name="term">The term to look up.</param>
    /// <returns>The number of chunks containing the term, or 0 if not found.</returns>
    public int GetDocumentFrequency(string term)
    {
        if (string.IsNullOrEmpty(term))
            return 0;

        if (_index.TryGetValue(term, out PostingsList? postings))
            return postings.Count;

        return 0;
    }

    /// <summary>
    /// Gets the term frequency for a specific term in a specific chunk.
    /// </summary>
    /// <param name="term">The term to look up.</param>
    /// <param name="chunkId">The chunk identifier.</param>
    /// <returns>The term frequency, or 0 if not found.</returns>
    public int GetTermFrequency(string term, string chunkId)
    {
        if (string.IsNullOrEmpty(term) || string.IsNullOrEmpty(chunkId))
            return 0;

        if (_index.TryGetValue(term, out PostingsList? postings))
            return postings.GetFrequency(chunkId);

        return 0;
    }

    /// <summary>
    /// Gets the document length (number of tokens) for a specific chunk.
    /// </summary>
    /// <param name="chunkId">The chunk identifier.</param>
    /// <returns>The document length in tokens, or 0 if not found.</returns>
    public int GetDocLength(string chunkId)
    {
        if (string.IsNullOrEmpty(chunkId))
            return 0;

        if (_docLengths.TryGetValue(chunkId, out int length))
            return length;

        return 0;
    }

    /// <summary>
    /// Gets all chunk IDs in the index.
    /// </summary>
    /// <returns>An enumerable collection of chunk IDs.</returns>
    public IEnumerable<string> GetAllChunkIds()
    {
        return _docLengths.Keys;
    }

    /// <summary>
    /// Gets all chunk IDs that contain a specific term.
    /// </summary>
    /// <param name="term">The term to look up.</param>
    /// <returns>An enumerable collection of chunk IDs containing the term, or empty if term not found.</returns>
    public IEnumerable<string> GetChunkIdsForTerm(string term)
    {
        if (string.IsNullOrEmpty(term))
            return Array.Empty<string>();

        if (_index.TryGetValue(term, out PostingsList? postings))
            return postings.GetChunkIds();

        return Array.Empty<string>();
    }

    /// <summary>
    /// Gets all terms in the vocabulary.
    /// </summary>
    /// <returns>An enumerable collection of all indexed terms.</returns>
    internal IEnumerable<string> GetAllTerms()
    {
        return _index.Keys;
    }

    /// <summary>
    /// Gets all postings (chunk ID and term frequency pairs) for a specific term.
    /// </summary>
    /// <param name="term">The term to look up.</param>
    /// <returns>An enumerable collection of (chunkId, frequency) tuples.</returns>
    internal IEnumerable<(string chunkId, int frequency)> GetPostingsForTerm(string term)
    {
        if (string.IsNullOrEmpty(term))
            return Array.Empty<(string, int)>();

        if (_index.TryGetValue(term, out PostingsList? postings))
            return postings.GetAllPostings();

        return Array.Empty<(string, int)>();
    }

    /// <summary>
    /// Gets all document lengths as an enumerable collection.
    /// </summary>
    /// <returns>An enumerable collection of (chunkId, length) pairs.</returns>
    internal IEnumerable<(string chunkId, int length)> GetAllDocLengths()
    {
        foreach (var kvp in _docLengths)
        {
            yield return (kvp.Key, kvp.Value);
        }
    }

    /// <summary>
    /// Sets internal state for deserialization. Used only by IndexSerializer.
    /// </summary>
    /// <param name="totalChunks">The total number of chunks.</param>
    /// <param name="avgDocLength">The average document length.</param>
    internal void SetState(int totalChunks, double avgDocLength)
    {
        _totalChunks = totalChunks;
        _avgDocLength = avgDocLength;
    }

    /// <summary>
    /// Adds a document length entry. Used only by IndexSerializer for deserialization.
    /// </summary>
    internal void AddDocLength(string chunkId, int length)
    {
        _docLengths[chunkId] = length;
    }

    /// <summary>
    /// Adds a posting entry. Used only by IndexSerializer for deserialization.
    /// </summary>
    internal void AddPosting(string term, string chunkId, int termFrequency)
    {
        if (!_index.ContainsKey(term))
        {
            _index[term] = new PostingsList();
        }
        _index[term].Add(chunkId, termFrequency);
    }

    /// <summary>
    /// Updates the average document length based on current document lengths.
    /// </summary>
    private void UpdateAvgDocLength()
    {
        if (_totalChunks == 0)
        {
            _avgDocLength = 0.0;
            return;
        }

        long totalLength = 0;
        foreach (var kvp in _docLengths)
        {
            totalLength += kvp.Value;
        }

        _avgDocLength = (double)totalLength / _totalChunks;
    }

    /// <summary>
    /// Represents a postings list mapping chunk IDs to term frequencies.
    /// </summary>
    private sealed class PostingsList
    {
        private readonly Dictionary<string, int> _postings;

        public PostingsList()
        {
            _postings = new Dictionary<string, int>();
        }

        public int Count => _postings.Count;

        public void Add(string chunkId, int termFrequency)
        {
            _postings[chunkId] = termFrequency;
        }

        public void Remove(string chunkId)
        {
            _postings.Remove(chunkId);
        }

        public int GetFrequency(string chunkId)
        {
            if (_postings.TryGetValue(chunkId, out int freq))
                return freq;
            return 0;
        }

        public IEnumerable<string> GetChunkIds()
        {
            return _postings.Keys;
        }

        public IEnumerable<(string chunkId, int frequency)> GetAllPostings()
        {
            foreach (var kvp in _postings)
            {
                yield return (kvp.Key, kvp.Value);
            }
        }
    }
}
