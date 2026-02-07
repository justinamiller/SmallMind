using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace SmallMind.Retrieval
{
    /// <summary>
    /// In-memory lexical retrieval index using BM25 scoring.
    /// No external dependencies, deterministic, and memory-efficient.
    /// </summary>
    public class InMemoryLexicalIndex : IRetrievalIndex
    {
        private readonly Dictionary<string, Document> _documents;
        private readonly Dictionary<string, DocumentChunk> _chunks;
        private readonly Dictionary<string, Dictionary<string, double>> _chunkTermFrequencies;
        private readonly Dictionary<string, int> _documentFrequencies;
        private readonly ChunkingOptions _chunkingOptions;
        private int _totalChunks;

        /// <summary>
        /// Create a new in-memory lexical index.
        /// </summary>
        /// <param name="chunkingOptions">Options for document chunking.</param>
        public InMemoryLexicalIndex(ChunkingOptions? chunkingOptions = null)
        {
            _documents = new Dictionary<string, Document>();
            _chunks = new Dictionary<string, DocumentChunk>();
            _chunkTermFrequencies = new Dictionary<string, Dictionary<string, double>>();
            _documentFrequencies = new Dictionary<string, int>();
            _chunkingOptions = chunkingOptions ?? new ChunkingOptions();
            _totalChunks = 0;
        }

        /// <summary>
        /// Add or update a document in the index.
        /// </summary>
        public void Upsert(Document document)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            // Remove existing document if present
            if (_documents.ContainsKey(document.Id))
            {
                RemoveDocument(document.Id);
            }

            // Store the document
            _documents[document.Id] = document;

            // Chunk the document
            var chunks = DocumentChunker.Chunk(document, _chunkingOptions);

            // Index each chunk
            foreach (var chunk in chunks)
            {
                IndexChunk(chunk);
            }
        }

        /// <summary>
        /// Add or update multiple documents in the index.
        /// </summary>
        public void Upsert(IEnumerable<Document> documents)
        {
            if (documents == null)
                throw new ArgumentNullException(nameof(documents));

            foreach (var document in documents)
            {
                Upsert(document);
            }
        }

        /// <summary>
        /// Search the index for relevant chunks.
        /// </summary>
        public RetrievalResult Search(string query, RetrievalOptions options, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(query))
                throw new ArgumentException("Query cannot be null or empty", nameof(query));
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            var result = new RetrievalResult
            {
                Query = query,
                TotalCandidates = _totalChunks
            };

            if (_totalChunks == 0)
            {
                result.Warnings.Add("Index is empty");
                return result;
            }

            // Tokenize the query
            var queryTerms = Tokenize(query);
            if (queryTerms.Count == 0)
            {
                result.Warnings.Add("Query produced no tokens");
                return result;
            }

            // Score all chunks
            var scoredChunks = new List<(string chunkId, double score)>();

            foreach (var kvp in _chunks)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var chunkId = kvp.Key;
                var chunk = kvp.Value;

                double score = CalculateBM25Score(queryTerms, chunkId);
                
                if (score > 0)
                {
                    scoredChunks.Add((chunkId, score));
                }
            }

            // Sort by score (descending), then by chunkId (ascending) for determinism
            if (options.Deterministic)
            {
                scoredChunks.Sort((a, b) =>
                {
                    int scoreComparison = b.score.CompareTo(a.score);
                    if (scoreComparison != 0)
                        return scoreComparison;
                    return string.CompareOrdinal(a.chunkId, b.chunkId);
                });
            }
            else
            {
                scoredChunks.Sort((a, b) => b.score.CompareTo(a.score));
            }

            // Apply per-document limit and topK
            var documentChunkCounts = new Dictionary<string, int>();
            var retrievedChunks = new List<RetrievedChunkWithCitation>();

            foreach (var (chunkId, score) in scoredChunks)
            {
                if (retrievedChunks.Count >= options.TopK)
                    break;

                var chunk = _chunks[chunkId];
                
                // Check per-document limit
                if (!documentChunkCounts.TryGetValue(chunk.DocumentId, out int count))
                {
                    count = 0;
                }

                if (count >= options.MaxChunksPerDocument)
                {
                    continue; // Skip this chunk, already have enough from this document
                }

                documentChunkCounts[chunk.DocumentId] = count + 1;

                // Create retrieved chunk with citation
                var text = chunk.Text;
                if (options.IncludeSnippets && text.Length > options.MaxSnippetChars)
                {
                    text = text.Substring(0, options.MaxSnippetChars) + "...";
                }

                var retrievedChunk = new RetrievedChunkWithCitation
                {
                    DocumentId = chunk.DocumentId,
                    ChunkId = chunk.ChunkId,
                    Score = score,
                    Text = text,
                    Metadata = new Dictionary<string, string>(chunk.Metadata),
                    Citation = new Citation
                    {
                        Title = chunk.Metadata.TryGetValue("title", out var title) ? title : null,
                        SourceUri = chunk.Metadata.TryGetValue("source_uri", out var uri) ? uri : null,
                        StartOffset = chunk.StartOffset,
                        EndOffset = chunk.EndOffset
                    }
                };

                retrievedChunks.Add(retrievedChunk);
            }

            result.Chunks = retrievedChunks;
            return result;
        }

        /// <summary>
        /// Calculate BM25 score for a chunk given query terms.
        /// BM25 is a ranking function used by search engines.
        /// </summary>
        private double CalculateBM25Score(List<string> queryTerms, string chunkId)
        {
            const double k1 = 1.2;
            const double b = 0.75;
            
            if (!_chunkTermFrequencies.TryGetValue(chunkId, out var chunkTf))
                return 0;

            var chunk = _chunks[chunkId];
            int docLength = chunk.Text.Length;
            double avgDocLength = CalculateAverageDocumentLength();

            double score = 0;

            foreach (var term in queryTerms)
            {
                if (!chunkTf.TryGetValue(term, out double tf))
                    continue;

                // IDF component
                int df = _documentFrequencies.GetValueOrDefault(term, 0);
                if (df == 0)
                    continue;

                double idf = Math.Log((_totalChunks - df + 0.5) / (df + 0.5) + 1.0);

                // BM25 formula
                double numerator = tf * (k1 + 1);
                double denominator = tf + k1 * (1 - b + b * (docLength / avgDocLength));
                
                score += idf * (numerator / denominator);
            }

            return score;
        }

        /// <summary>
        /// Index a single chunk.
        /// </summary>
        private void IndexChunk(DocumentChunk chunk)
        {
            _chunks[chunk.ChunkId] = chunk;
            _totalChunks++;

            // Tokenize and calculate term frequencies
            var terms = Tokenize(chunk.Text);
            var termFrequency = new Dictionary<string, double>();
            
            foreach (var term in terms)
            {
                if (termFrequency.TryGetValue(term, out double count))
                {
                    termFrequency[term] = count + 1;
                }
                else
                {
                    termFrequency[term] = 1;
                }
            }

            // Normalize by document length (TF)
            var termKeys = new List<string>(termFrequency.Count);
            foreach (var key in termFrequency.Keys)
            {
                termKeys.Add(key);
            }
            
            for (int i = 0; i < termKeys.Count; i++)
            {
                termFrequency[termKeys[i]] = termFrequency[termKeys[i]] / terms.Count;
            }

            _chunkTermFrequencies[chunk.ChunkId] = termFrequency;

            // Update document frequencies (DF)
            var uniqueTerms = new HashSet<string>(termFrequency.Keys);
            foreach (var term in uniqueTerms)
            {
                if (_documentFrequencies.TryGetValue(term, out int df))
                {
                    _documentFrequencies[term] = df + 1;
                }
                else
                {
                    _documentFrequencies[term] = 1;
                }
            }
        }

        /// <summary>
        /// Remove a document and its chunks from the index.
        /// </summary>
        private void RemoveDocument(string documentId)
        {
            // Find all chunks belonging to this document
            var chunksToRemove = new List<string>();
            foreach (var chunk in _chunks.Values)
            {
                if (chunk.DocumentId == documentId)
                {
                    chunksToRemove.Add(chunk.ChunkId);
                }
            }

            foreach (var chunkId in chunksToRemove)
            {
                var chunk = _chunks[chunkId];
                
                // Update document frequencies
                if (_chunkTermFrequencies.TryGetValue(chunkId, out var termFreqs))
                {
                    var uniqueTerms = new HashSet<string>(termFreqs.Keys);
                    foreach (var term in uniqueTerms)
                    {
                        if (_documentFrequencies.TryGetValue(term, out int df))
                        {
                            _documentFrequencies[term] = df - 1;
                            if (_documentFrequencies[term] <= 0)
                            {
                                _documentFrequencies.Remove(term);
                            }
                        }
                    }

                    _chunkTermFrequencies.Remove(chunkId);
                }

                _chunks.Remove(chunkId);
                _totalChunks--;
            }

            _documents.Remove(documentId);
        }

        /// <summary>
        /// Calculate average document length across all chunks.
        /// </summary>
        private double CalculateAverageDocumentLength()
        {
            if (_totalChunks == 0)
                return 0;

            long totalLength = 0;
            foreach (var chunk in _chunks.Values)
            {
                totalLength += chunk.Text.Length;
            }

            return (double)totalLength / _totalChunks;
        }

        /// <summary>
        /// Tokenize text into terms (deterministic, lowercased, alphanumeric only).
        /// </summary>
        private static List<string> Tokenize(string text)
        {
            var tokens = new List<string>();
            var currentToken = new System.Text.StringBuilder();

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                
                if (char.IsLetterOrDigit(c))
                {
                    currentToken.Append(char.ToLowerInvariant(c));
                }
                else
                {
                    if (currentToken.Length > 0)
                    {
                        // Only include tokens with 2+ characters
                        if (currentToken.Length >= 2)
                        {
                            tokens.Add(currentToken.ToString());
                        }
                        currentToken.Clear();
                    }
                }
            }

            // Add final token if any
            if (currentToken.Length >= 2)
            {
                tokens.Add(currentToken.ToString());
            }

            return tokens;
        }
    }
}
