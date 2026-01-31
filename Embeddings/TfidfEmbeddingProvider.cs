using System;
using System.Collections.Generic;
using System.Text;

namespace TinyLLM.Embeddings
{
    /// <summary>
    /// TF-IDF based embedding provider.
    /// A simple, fast, local embedding implementation with no external dependencies.
    /// Provides reasonable semantic similarity for retrieval tasks.
    /// </summary>
    public class TfidfEmbeddingProvider : IEmbeddingProvider
    {
        private readonly Dictionary<string, int> _vocabulary;
        private readonly Dictionary<string, double> _idfScores;
        private readonly int _maxFeatures;
        private readonly HashSet<string> _stopWords;
        
        public int EmbeddingDimension => _maxFeatures;

        /// <summary>
        /// Create a TF-IDF embedding provider.
        /// </summary>
        /// <param name="maxFeatures">Maximum number of features (dimensions) in embedding</param>
        public TfidfEmbeddingProvider(int maxFeatures = 512)
        {
            _maxFeatures = maxFeatures;
            _vocabulary = new Dictionary<string, int>();
            _idfScores = new Dictionary<string, double>();
            _stopWords = CreateStopWords();
        }

        /// <summary>
        /// Build the vocabulary and IDF scores from a corpus of documents.
        /// Call this before using Embed() or EmbedBatch().
        /// </summary>
        public void Fit(List<string> documents)
        {
            if (documents == null || documents.Count == 0)
            {
                throw new ArgumentException("Documents cannot be null or empty", nameof(documents));
            }

            // Step 1: Build term frequency across all documents
            var documentFrequency = new Dictionary<string, int>();
            var allTerms = new HashSet<string>();

            for (int i = 0; i < documents.Count; i++)
            {
                var terms = Tokenize(documents[i]);
                var uniqueTerms = new HashSet<string>();
                
                for (int j = 0; j < terms.Count; j++)
                {
                    allTerms.Add(terms[j]);
                    uniqueTerms.Add(terms[j]);
                }

                // Count document frequency
                foreach (var term in uniqueTerms)
                {
                    if (documentFrequency.ContainsKey(term))
                    {
                        documentFrequency[term]++;
                    }
                    else
                    {
                        documentFrequency[term] = 1;
                    }
                }
            }

            // Step 2: Select top terms by document frequency (up to maxFeatures)
            var termFreqList = new List<(string term, int freq)>();
            foreach (var kvp in documentFrequency)
            {
                termFreqList.Add((kvp.Key, kvp.Value));
            }

            // Sort by frequency descending
            termFreqList.Sort((a, b) => b.freq.CompareTo(a.freq));

            // Take top maxFeatures
            int vocabSize = Math.Min(_maxFeatures, termFreqList.Count);
            for (int i = 0; i < vocabSize; i++)
            {
                _vocabulary[termFreqList[i].term] = i;
            }

            // Step 3: Calculate IDF scores (with smoothing to prevent division by zero)
            int totalDocs = documents.Count;
            foreach (var kvp in documentFrequency)
            {
                if (_vocabulary.ContainsKey(kvp.Key))
                {
                    double idf = Math.Log((totalDocs + 1.0) / (kvp.Value + 1.0));
                    _idfScores[kvp.Key] = idf;
                }
            }

            Console.WriteLine($"TF-IDF vocabulary built: {_vocabulary.Count} terms from {totalDocs} documents");
        }

        /// <summary>
        /// Generate a TF-IDF embedding for a text.
        /// </summary>
        public float[] Embed(string text)
        {
            if (_vocabulary.Count == 0)
            {
                throw new InvalidOperationException("Vocabulary not built. Call Fit() first.");
            }

            var embedding = new float[_maxFeatures];
            
            // Tokenize and count term frequencies
            var terms = Tokenize(text);
            var termCounts = new Dictionary<string, int>();
            
            for (int i = 0; i < terms.Count; i++)
            {
                var term = terms[i];
                if (termCounts.ContainsKey(term))
                {
                    termCounts[term]++;
                }
                else
                {
                    termCounts[term] = 1;
                }
            }

            // Calculate TF-IDF values
            int totalTerms = terms.Count;
            if (totalTerms == 0)
            {
                return embedding; // Return zero vector
            }

            foreach (var kvp in termCounts)
            {
                if (_vocabulary.TryGetValue(kvp.Key, out int index))
                {
                    double tf = (double)kvp.Value / totalTerms;
                    double idf = _idfScores.TryGetValue(kvp.Key, out double idfValue) ? idfValue : 0.0;
                    embedding[index] = (float)(tf * idf);
                }
            }

            // L2 normalize the embedding
            float norm = 0f;
            for (int i = 0; i < embedding.Length; i++)
            {
                norm += embedding[i] * embedding[i];
            }
            
            if (norm > 0f)
            {
                norm = MathF.Sqrt(norm);
                for (int i = 0; i < embedding.Length; i++)
                {
                    embedding[i] /= norm;
                }
            }

            return embedding;
        }

        /// <summary>
        /// Tokenize text into terms (simple word-based tokenization).
        /// </summary>
        private List<string> Tokenize(string text)
        {
            var terms = new List<string>();
            if (string.IsNullOrWhiteSpace(text))
            {
                return terms;
            }

            var sb = new StringBuilder();
            var lowerText = text.ToLowerInvariant();
            
            for (int i = 0; i < lowerText.Length; i++)
            {
                char c = lowerText[i];
                if (char.IsLetterOrDigit(c))
                {
                    sb.Append(c);
                }
                else if (sb.Length > 0)
                {
                    string word = sb.ToString();
                    if (word.Length >= 2 && !_stopWords.Contains(word))
                    {
                        terms.Add(word);
                    }
                    sb.Clear();
                }
            }

            // Add last word if any
            if (sb.Length > 0)
            {
                string word = sb.ToString();
                if (word.Length >= 2 && !_stopWords.Contains(word))
                {
                    terms.Add(word);
                }
            }

            return terms;
        }

        /// <summary>
        /// Create a basic set of English stop words.
        /// </summary>
        private HashSet<string> CreateStopWords()
        {
            return new HashSet<string>
            {
                "the", "a", "an", "and", "or", "but", "in", "on", "at", "to", "for",
                "of", "with", "by", "from", "as", "is", "was", "are", "were", "be",
                "been", "being", "have", "has", "had", "do", "does", "did", "will",
                "would", "should", "could", "may", "might", "must", "can", "this",
                "that", "these", "those", "i", "you", "he", "she", "it", "we", "they",
                "what", "which", "who", "when", "where", "why", "how"
            };
        }
    }
}
