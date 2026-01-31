using System;
using System.Collections.Generic;
using System.Linq;
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
            var stopWords = new HashSet<string> { "the", "a", "an", "is", "are", "was", "were", "what", "when", "where", "who", "why", "how", "do", "does", "did" };
            var questionWords = new List<string>();
            var words = question.ToLower().Split(new[] { ' ', '?', '!', '.', ',', ';', ':' }, StringSplitOptions.RemoveEmptyEntries);
            
            for (int i = 0; i < words.Length; i++)
            {
                var word = words[i];
                if (!stopWords.Contains(word) && word.Length > 2)
                {
                    questionWords.Add(word);
                }
            }

            if (questionWords.Count == 0)
            {
                // If no keywords, return beginning of corpus
                return _trainingCorpus.Length > maxContextLength 
                    ? _trainingCorpus.Substring(0, maxContextLength) 
                    : _trainingCorpus;
            }

            // Split corpus into sentences
            var sentenceDelimiters = new[] { '.', '!', '?' };
            var sentencesList = new List<string>();
            var parts = _trainingCorpus.Split(sentenceDelimiters, StringSplitOptions.RemoveEmptyEntries);
            
            for (int i = 0; i < parts.Length; i++)
            {
                var sentence = parts[i].Trim();
                if (sentence.Length > 0)
                {
                    sentencesList.Add(sentence);
                }
            }
            var sentences = sentencesList;

            // Score sentences by keyword matches
            var scoredSentences = new List<(string sentence, int score)>();
            foreach (var sentence in sentences)
            {
                string lowerSentence = sentence.ToLower();
                int score = 0;
                for (int i = 0; i < questionWords.Count; i++)
                {
                    if (lowerSentence.Contains(questionWords[i]))
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
