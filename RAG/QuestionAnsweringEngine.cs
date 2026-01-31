using System;
using System.Collections.Generic;
using TinyLLM.Core;
using TinyLLM.Text;
using TinyLLM.Embeddings;
using TinyLLM.Indexing;

namespace TinyLLM.RAG
{
    /// <summary>
    /// Question-answering engine that uses the trained LLM to answer questions
    /// based on the model's training data and context.
    /// </summary>
    public class QuestionAnsweringEngine
    {
        private readonly TransformerModel _model;
        private readonly Tokenizer _tokenizer;
        private readonly int _blockSize;
        private readonly Sampling _sampler;
        private readonly string _trainingCorpus;

        // Q&A prompt templates
        private const string QA_TEMPLATE = @"Answer the following question based on the context provided.

Context: {context}

Question: {question}

Answer:";

        private const string SIMPLE_QA_TEMPLATE = @"Q: {question}
A:";

        public QuestionAnsweringEngine(TransformerModel model, Tokenizer tokenizer, int blockSize, string trainingCorpus = "")
        {
            _model = model;
            _tokenizer = tokenizer;
            _blockSize = blockSize;
            _sampler = new Sampling(model, tokenizer, blockSize);
            _trainingCorpus = trainingCorpus;
        }

        /// <summary>
        /// Answer a question using the LLM with context from training data
        /// </summary>
        public string AnswerQuestion(string question, int maxTokens = 150, double temperature = 0.7, int topK = 40, int? seed = null, bool useContext = true)
        {
            string prompt;
            
            if (useContext && !string.IsNullOrEmpty(_trainingCorpus))
            {
                // Extract relevant context from training corpus
                string context = ExtractRelevantContext(question, maxContextLength: 500);
                prompt = QA_TEMPLATE.Replace("{context}", context).Replace("{question}", question);
            }
            else
            {
                // Simple Q&A format
                prompt = SIMPLE_QA_TEMPLATE.Replace("{question}", question);
            }

            // Generate answer using the model
            string fullResponse = _sampler.Generate(
                prompt: prompt,
                maxNewTokens: maxTokens,
                temperature: temperature,
                topK: topK,
                seed: seed,
                showPerf: false
            );

            // Extract just the answer part (remove the prompt)
            string answer = ExtractAnswer(fullResponse, prompt);
            return answer;
        }

        /// <summary>
        /// Answer a question within a conversation context
        /// </summary>
        public string AnswerQuestionWithContext(string question, string conversationContext, int maxTokens = 150, double temperature = 0.7, int topK = 40, int? seed = null)
        {
            // Build prompt with conversation context
            string prompt = conversationContext + "\nUser: " + question + "\nAssistant:";

            // Generate answer
            string fullResponse = _sampler.Generate(
                prompt: prompt,
                maxNewTokens: maxTokens,
                temperature: temperature,
                topK: topK,
                seed: seed,
                showPerf: false
            );

            // Extract the answer (everything after "Assistant:")
            string answer = ExtractAnswer(fullResponse, prompt);
            return answer;
        }

        /// <summary>
        /// Extract relevant context from training corpus based on question keywords
        /// Simple keyword-based retrieval for educational purposes
        /// </summary>
        private string ExtractRelevantContext(string question, int maxContextLength = 500)
        {
            if (string.IsNullOrEmpty(_trainingCorpus))
            {
                return "";
            }

            // Extract keywords from question (simple approach: remove common words)
            // Use OrdinalIgnoreCase for case-insensitive matching
            var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase) 
            { 
                "the", "a", "an", "is", "are", "was", "were", 
                "what", "when", "where", "who", "why", "how", 
                "do", "does", "did" 
            };
            var questionWords = new List<string>();
            
            // Manual parsing to avoid string.Split allocation
            int wordStart = -1;
            ReadOnlySpan<char> questionSpan = question.AsSpan();
            
            for (int i = 0; i <= questionSpan.Length; i++)
            {
                bool isDelimiter = i == questionSpan.Length || 
                    questionSpan[i] == ' ' || questionSpan[i] == '?' || 
                    questionSpan[i] == '!' || questionSpan[i] == '.' || 
                    questionSpan[i] == ',' || questionSpan[i] == ';' || 
                    questionSpan[i] == ':';
                
                if (isDelimiter)
                {
                    if (wordStart >= 0)
                    {
                        int wordLength = i - wordStart;
                        if (wordLength > 2)
                        {
                            // Extract word - stopWords comparer handles case-insensitive matching
                            string word = question.Substring(wordStart, wordLength);
                            if (!stopWords.Contains(word))
                            {
                                questionWords.Add(word);
                            }
                        }
                        wordStart = -1;
                    }
                }
                else if (wordStart < 0)
                {
                    wordStart = i;
                }
            }

            if (questionWords.Count == 0)
            {
                // If no keywords, return beginning of corpus
                return _trainingCorpus.Length > maxContextLength 
                    ? _trainingCorpus.Substring(0, maxContextLength) 
                    : _trainingCorpus;
            }

            // Split corpus into sentences - manual parsing to avoid string.Split allocation
            var sentences = new List<string>();
            int sentenceStart = 0;
            ReadOnlySpan<char> corpusSpan = _trainingCorpus.AsSpan();
            
            for (int i = 0; i < corpusSpan.Length; i++)
            {
                if (corpusSpan[i] == '.' || corpusSpan[i] == '!' || corpusSpan[i] == '?')
                {
                    int sentenceLength = i - sentenceStart;
                    if (sentenceLength > 0)
                    {
                        string sentence = _trainingCorpus.Substring(sentenceStart, sentenceLength).Trim();
                        if (sentence.Length > 0)
                        {
                            sentences.Add(sentence);
                        }
                    }
                    sentenceStart = i + 1;
                }
            }
            
            // Add remaining text as last sentence if any
            if (sentenceStart < _trainingCorpus.Length)
            {
                string sentence = _trainingCorpus.Substring(sentenceStart).Trim();
                if (sentence.Length > 0)
                {
                    sentences.Add(sentence);
                }
            }

            // Score sentences by keyword matches (using case-insensitive comparison)
            var scoredSentences = new List<(string sentence, int score)>(sentences.Count);
            
            for (int s = 0; s < sentences.Count; s++)
            {
                string sentence = sentences[s];
                int score = 0;
                
                // Count keyword matches using ordinal ignore case to avoid ToLower() allocation
                for (int i = 0; i < questionWords.Count; i++)
                {
                    if (sentence.IndexOf(questionWords[i], StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        score++;
                    }
                }
                
                if (score > 0)
                {
                    scoredSentences.Add((sentence, score));
                }
            }

            // Sort by score and take top sentences (manual sort to avoid LINQ OrderBy)
            scoredSentences.Sort((a, b) => b.score.CompareTo(a.score));
            
            var relevantSentences = new List<string>();
            int numToTake = Math.Min(5, scoredSentences.Count);
            for (int i = 0; i < numToTake; i++)
            {
                relevantSentences.Add(scoredSentences[i].sentence);
            }

            if (relevantSentences.Count == 0)
            {
                // No relevant sentences found, return beginning of corpus
                return _trainingCorpus.Length > maxContextLength 
                    ? _trainingCorpus.Substring(0, maxContextLength) 
                    : _trainingCorpus;
            }

            // Join sentences and truncate if needed
            string context = string.Join(". ", relevantSentences) + ".";
            if (context.Length > maxContextLength)
            {
                context = context.Substring(0, maxContextLength);
                // Try to end at a sentence boundary
                int lastPeriod = context.LastIndexOf('.');
                if (lastPeriod > maxContextLength / 2)
                {
                    context = context.Substring(0, lastPeriod + 1);
                }
            }

            return context;
        }

        /// <summary>
        /// Extract the answer from the full response by removing the prompt
        /// </summary>
        private string ExtractAnswer(string fullResponse, string prompt)
        {
            if (fullResponse.StartsWith(prompt))
            {
                string answer = fullResponse.Substring(prompt.Length).Trim();
                
                // Clean up the answer - stop at natural boundaries
                // Stop at next "Q:" or "User:" to avoid continuing into next question
                int qIndex = answer.IndexOf("\nQ:");
                if (qIndex > 0)
                {
                    answer = answer.Substring(0, qIndex).Trim();
                }
                
                int userIndex = answer.IndexOf("\nUser:");
                if (userIndex > 0)
                {
                    answer = answer.Substring(0, userIndex).Trim();
                }

                return answer;
            }
            
            return fullResponse.Trim();
        }

        /// <summary>
        /// Generate multiple answer candidates and select the best one (beam search approximation)
        /// </summary>
        public string AnswerQuestionWithVariants(string question, int numVariants = 3, int maxTokens = 150, double temperature = 0.8, int topK = 40)
        {
            var answers = new List<string>();
            var random = new Random();

            for (int i = 0; i < numVariants; i++)
            {
                string answer = AnswerQuestion(
                    question: question,
                    maxTokens: maxTokens,
                    temperature: temperature,
                    topK: topK,
                    seed: random.Next(),
                    useContext: true
                );
                answers.Add(answer);
            }

            // Select the longest answer (simple heuristic - avoid LINQ OrderBy)
            string longestAnswer = "";
            int maxLength = 0;
            for (int i = 0; i < answers.Count; i++)
            {
                if (answers[i].Length > maxLength)
                {
                    maxLength = answers[i].Length;
                    longestAnswer = answers[i];
                }
            }
            
            return longestAnswer;
        }
    }
}
