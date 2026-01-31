using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace TinyLLM
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
    public class VectorIndex
    {
        private readonly string _indexDirectory;
        private readonly string _indexFilePath;
        private readonly List<VectorEntry> _entries;
        private readonly IEmbeddingProvider _embeddingProvider;
        private int _nextId;

        public int Count => _entries.Count;

        /// <summary>
        /// Create a new VectorIndex.
        /// </summary>
        /// <param name="indexDirectory">Directory to store the index (default: ./index)</param>
        /// <param name="embeddingProvider">Embedding provider to use</param>
        public VectorIndex(IEmbeddingProvider embeddingProvider, string indexDirectory = "./index")
        {
            _embeddingProvider = embeddingProvider ?? throw new ArgumentNullException(nameof(embeddingProvider));
            _indexDirectory = indexDirectory;
            _indexFilePath = Path.Combine(_indexDirectory, "vectors.jsonl");
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
        public string Add(string text, Dictionary<string, string>? metadata = null)
        {
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
        public List<SearchResult> Search(string queryText, int k = 5)
        {
            if (_entries.Count == 0)
            {
                return new List<SearchResult>();
            }

            var queryVector = _embeddingProvider.Embed(queryText);
            return SearchByVector(queryVector, k);
        }

        /// <summary>
        /// Search using a pre-computed query vector.
        /// </summary>
        public List<SearchResult> SearchByVector(float[] queryVector, int k = 5)
        {
            if (_entries.Count == 0)
            {
                return new List<SearchResult>();
            }

            // Calculate cosine similarity for all entries
            var scores = new List<(int index, float score)>(_entries.Count);
            
            for (int i = 0; i < _entries.Count; i++)
            {
                float similarity = CosineSimilarity(queryVector, _entries[i].Vector);
                scores.Add((i, similarity));
            }

            // Sort by score descending (manual sort to avoid LINQ)
            scores.Sort((a, b) => b.score.CompareTo(a.score));

            // Take top k results
            int numResults = Math.Min(k, scores.Count);
            var results = new List<SearchResult>(numResults);
            
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
        public VectorEntry? GetById(string id)
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].Id == id)
                {
                    return _entries[i];
                }
            }
            return null;
        }
    }
}
