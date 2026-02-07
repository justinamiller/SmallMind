using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using SmallMind.Rag.Retrieval;
using SmallMind.Core.Validation;

namespace SmallMind.Rag.Indexing
{
    /// <summary>
    /// Represents a stored vector with its metadata.
    /// </summary>
    public class VectorEntry
    {
        public string Id { get; set; } = "";
        public float[] Vector { get; set; } = Array.Empty<float>();
        public string Text { get; set; } = "";
        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
    }

    /// <summary>
    /// Result from a vector search query.
    /// </summary>
    public class SearchResult
    {
        public string Id { get; set; } = "";
        public string Text { get; set; } = "";
        public float Score { get; set; }
        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
    }

    /// <summary>
    /// Vector index for storing and searching embeddings.
    /// Supports kNN search with cosine similarity.
    /// Stores vectors on disk in JSONL format.
    /// </summary>
    public class VectorIndex : IDisposable
    {
        private const string DEFAULT_INDEX_FILENAME = "vectors.jsonl";
        
        private readonly string _indexDirectory;
        private readonly string _indexFilePath;
        private readonly List<VectorEntry> _entries;
        private readonly IEmbeddingProvider _embeddingProvider;
        private int _nextId;
        private bool _disposed;

        public int Count => _entries.Count;

        /// <summary>
        /// Create a new VectorIndex.
        /// </summary>
        /// <param name="embeddingProvider">Embedding provider to use</param>
        /// <param name="indexDirectory">Directory to store the index (default: ./index)</param>
        /// <param name="indexFileName">Name of the index file (default: vectors.jsonl)</param>
        /// <exception cref="Core.Exceptions.ValidationException">Thrown when parameters are invalid.</exception>
        public VectorIndex(IEmbeddingProvider embeddingProvider, string indexDirectory = "./index", string indexFileName = DEFAULT_INDEX_FILENAME)
        {
            Guard.NotNull(embeddingProvider);
            Guard.NotNullOrWhiteSpace(indexDirectory);
            Guard.NotNullOrWhiteSpace(indexFileName);
            
            _embeddingProvider = embeddingProvider;
            _indexDirectory = indexDirectory;
            _indexFilePath = Path.Combine(_indexDirectory, indexFileName);
            _entries = new List<VectorEntry>();
            _nextId = 0;

            // Create index directory if it doesn't exist
            if (!Directory.Exists(_indexDirectory))
            {
                Directory.CreateDirectory(_indexDirectory);
            }
        }

        /// <summary>
        /// Add a text chunk to the index.
        /// </summary>
        /// <param name="text">Text to index</param>
        /// <param name="metadata">Optional metadata</param>
        /// <returns>ID of the added entry</returns>
        /// <exception cref="Exceptions.SmallMindObjectDisposedException">Thrown when index has been disposed.</exception>
        /// <exception cref="Core.Exceptions.ValidationException">Thrown when text is null or empty.</exception>
        public string Add(string text, Dictionary<string, string>? metadata = null)
        {
            Guard.NotDisposed(_disposed, nameof(VectorIndex));
            Guard.NotNullOrEmpty(text);
            
            var vector = _embeddingProvider.Embed(text);
            var id = (_nextId++).ToString();
            
            var entry = new VectorEntry
            {
                Id = id,
                Vector = vector,
                Text = text,
                Metadata = metadata ?? new Dictionary<string, string>()
            };

            _entries.Add(entry);
            return id;
        }

        /// <summary>
        /// Add multiple text chunks to the index.
        /// </summary>
        public List<string> AddBatch(List<string> texts, List<Dictionary<string, string>>? metadataList = null)
        {
            var ids = new List<string>(texts.Count);
            var embeddings = _embeddingProvider.EmbedBatch(texts);

            for (int i = 0; i < texts.Count; i++)
            {
                var id = (_nextId++).ToString();
                var metadata = (metadataList != null && i < metadataList.Count) 
                    ? metadataList[i] 
                    : new Dictionary<string, string>();

                var entry = new VectorEntry
                {
                    Id = id,
                    Vector = embeddings[i],
                    Text = texts[i],
                    Metadata = metadata
                };

                _entries.Add(entry);
                ids.Add(id);
            }

            return ids;
        }

        /// <summary>
        /// Search for the k most similar vectors using cosine similarity.
        /// </summary>
        /// <param name="queryText">Query text</param>
        /// <param name="k">Number of results to return</param>
        /// <returns>List of search results ordered by similarity (descending)</returns>
        /// <exception cref="Exceptions.SmallMindObjectDisposedException">Thrown when index has been disposed.</exception>
        /// <exception cref="Core.Exceptions.ValidationException">Thrown when parameters are invalid.</exception>
        public List<SearchResult> Search(string queryText, int k = 5)
        {
            Guard.NotDisposed(_disposed, nameof(VectorIndex));
            Guard.NotNullOrEmpty(queryText);
            Guard.GreaterThan(k, 0);
            
            if (_entries.Count == 0)
            {
                return new List<SearchResult>();
            }

            var queryVector = _embeddingProvider.Embed(queryText);
            return SearchByVector(queryVector, k);
        }

        /// <summary>
        /// Search using a pre-computed query vector.
        /// Optimized with partial sort for top-k retrieval.
        /// </summary>
        /// <param name="queryVector">Pre-computed query vector</param>
        /// <param name="k">Number of results to return</param>
        /// <returns>List of search results ordered by similarity (descending)</returns>
        /// <exception cref="Exceptions.SmallMindObjectDisposedException">Thrown when index has been disposed.</exception>
        /// <exception cref="Core.Exceptions.ValidationException">Thrown when parameters are invalid.</exception>
        public List<SearchResult> SearchByVector(float[] queryVector, int k = 5)
        {
            Guard.NotDisposed(_disposed, nameof(VectorIndex));
            Guard.NotNull(queryVector);
            Guard.GreaterThan(k, 0);
            
            if (_entries.Count == 0)
            {
                return new List<SearchResult>();
            }

            // Pre-allocate results list
            int numResults = Math.Min(k, _entries.Count);
            var results = new List<SearchResult>(numResults);
            
            // For small k, use a min-heap approach (top-k selection)
            // For large k (>= half the entries), just do full sort
            if (k < _entries.Count / 2)
            {
                // Top-K selection using partial sort
                var topK = new List<(int index, float score)>(k);
                
                for (int i = 0; i < _entries.Count; i++)
                {
                    float similarity = CosineSimilarity(queryVector, _entries[i].Vector);
                    
                    if (topK.Count < k)
                    {
                        // Still building up to k items
                        topK.Add((i, similarity));
                        if (topK.Count == k)
                        {
                            // Sort once when we reach k items
                            topK.Sort((a, b) => a.score.CompareTo(b.score));
                        }
                    }
                    else if (similarity > topK[0].score)
                    {
                        // Replace minimum and re-sort (binary insertion would be better but this is simpler)
                        topK[0] = (i, similarity);
                        
                        // Bubble the new item to its correct position
                        int pos = 0;
                        while (pos < k - 1 && topK[pos].score > topK[pos + 1].score)
                        {
                            var temp = topK[pos];
                            topK[pos] = topK[pos + 1];
                            topK[pos + 1] = temp;
                            pos++;
                        }
                    }
                }
                
                // Convert to results (reverse order for descending scores)
                for (int i = topK.Count - 1; i >= 0; i--)
                {
                    int entryIndex = topK[i].index;
                    var entry = _entries[entryIndex];
                    
                    results.Add(new SearchResult
                    {
                        Id = entry.Id,
                        Text = entry.Text,
                        Score = topK[i].score,
                        Metadata = entry.Metadata
                    });
                }
            }
            else
            {
                // For large k, full sort is more efficient
                var scores = new List<(int index, float score)>(_entries.Count);
                
                for (int i = 0; i < _entries.Count; i++)
                {
                    float similarity = CosineSimilarity(queryVector, _entries[i].Vector);
                    scores.Add((i, similarity));
                }

                // Sort by score descending
                scores.Sort((a, b) => b.score.CompareTo(a.score));

                // Take top k results
                for (int i = 0; i < numResults; i++)
                {
                    int entryIndex = scores[i].index;
                    var entry = _entries[entryIndex];
                    
                    results.Add(new SearchResult
                    {
                        Id = entry.Id,
                        Text = entry.Text,
                        Score = scores[i].score,
                        Metadata = entry.Metadata
                    });
                }
            }

            return results;
        }

        /// <summary>
        /// Calculate cosine similarity between two vectors.
        /// </summary>
        private float CosineSimilarity(float[] a, float[] b)
        {
            if (a.Length != b.Length)
            {
                throw new ArgumentException("Vectors must have the same length");
            }

            float dotProduct = 0f;
            float normA = 0f;
            float normB = 0f;

            for (int i = 0; i < a.Length; i++)
            {
                dotProduct += a[i] * b[i];
                normA += a[i] * a[i];
                normB += b[i] * b[i];
            }

            if (normA == 0f || normB == 0f)
            {
                return 0f;
            }

            return dotProduct / (MathF.Sqrt(normA) * MathF.Sqrt(normB));
        }

        /// <summary>
        /// Save the index to disk (JSONL format).
        /// </summary>
        public void Save()
        {
            using var writer = new StreamWriter(_indexFilePath, false, Encoding.UTF8);
            
            for (int i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                var json = JsonSerializer.Serialize(entry);
                writer.WriteLine(json);
            }

            Console.WriteLine($"Index saved to {_indexFilePath} ({_entries.Count} entries)");
        }

        /// <summary>
        /// Load the index from disk.
        /// </summary>
        public void Load()
        {
            if (!File.Exists(_indexFilePath))
            {
                Console.WriteLine($"Index file not found: {_indexFilePath}");
                return;
            }

            _entries.Clear();
            _nextId = 0;

            using var reader = new StreamReader(_indexFilePath, Encoding.UTF8);
            string? line;
            
            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                try
                {
                    var entry = JsonSerializer.Deserialize<VectorEntry>(line);
                    if (entry != null)
                    {
                        _entries.Add(entry);
                        
                        // Update next ID
                        if (int.TryParse(entry.Id, out int id))
                        {
                            _nextId = Math.Max(_nextId, id + 1);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Failed to parse line: {ex.Message}");
                }
            }

            Console.WriteLine($"Index loaded from {_indexFilePath} ({_entries.Count} entries)");
        }

        /// <summary>
        /// Rebuild the index from a corpus of documents.
        /// </summary>
        /// <param name="documents">Documents to index</param>
        /// <param name="metadataList">Optional metadata for each document</param>
        public void Rebuild(List<string> documents, List<Dictionary<string, string>>? metadataList = null)
        {
            Console.WriteLine("Rebuilding index...");
            
            _entries.Clear();
            _nextId = 0;

            // If using TF-IDF, fit the vocabulary first
            if (_embeddingProvider is TfidfEmbeddingProvider tfidf)
            {
                tfidf.Fit(documents);
            }

            // Add all documents
            AddBatch(documents, metadataList);

            Console.WriteLine($"Index rebuilt with {_entries.Count} entries");
        }

        /// <summary>
        /// Clear the index.
        /// </summary>
        public void Clear()
        {
            _entries.Clear();
            _nextId = 0;
        }

        /// <summary>
        /// Get an entry by ID.
        /// </summary>
        /// <param name="id">Entry ID to retrieve.</param>
        /// <returns>The vector entry if found; otherwise, null.</returns>
        /// <exception cref="Exceptions.SmallMindObjectDisposedException">Thrown when index has been disposed.</exception>
        public VectorEntry? GetById(string id)
        {
            Guard.NotDisposed(_disposed, nameof(VectorIndex));
            
            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].Id == id)
                {
                    return _entries[i];
                }
            }
            return null;
        }
        
        /// <summary>
        /// Disposes the vector index, clearing all entries.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            
            _entries.Clear();
            _disposed = true;
        }
    }
}
