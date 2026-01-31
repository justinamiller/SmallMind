using System;
using System.Collections.Generic;
using System.Text;
using TinyLLM.Core;
using TinyLLM.Text;

namespace TinyLLM.RAG
{
    /// <summary>
    /// Answer with citations from source chunks.
    /// </summary>
    public class AnswerWithCitations
    {
        public string Answer { get; set; } = "";
        public List<RetrievedChunk> Citations { get; set; } = new List<RetrievedChunk>();
        public string Question { get; set; } = "";
    }

    /// <summary>
    /// Answerer that uses the LLM with RAG (Retrieval-Augmented Generation).
    /// Combines retrieval, prompt building, and LLM generation.
    /// </summary>
    public class Answerer
    {
        private readonly Retriever _retriever;
        private readonly PromptBuilder _promptBuilder;
        private readonly Sampling _sampler;
        private readonly int _maxTokens;
        private readonly double _temperature;
        private readonly int _topK;

        /// <summary>
        /// Create a new answerer.
        /// </summary>
        /// <param name="retriever">Retriever for finding relevant chunks</param>
        /// <param name="promptBuilder">Prompt builder for formatting prompts</param>
        /// <param name="sampler">Sampling instance for LLM generation</param>
        /// <param name="maxTokens">Maximum tokens to generate (default: 150)</param>
        /// <param name="temperature">Sampling temperature (default: 0.7)</param>
        /// <param name="topK">Top-k sampling parameter (default: 40)</param>
        public Answerer(
            Retriever retriever, 
            PromptBuilder promptBuilder, 
            Sampling sampler,
            int maxTokens = 150,
            double temperature = 0.7,
            int topK = 40)
        {
            _retriever = retriever ?? throw new ArgumentNullException(nameof(retriever));
            _promptBuilder = promptBuilder ?? throw new ArgumentNullException(nameof(promptBuilder));
            _sampler = sampler ?? throw new ArgumentNullException(nameof(sampler));
            _maxTokens = maxTokens;
            _temperature = temperature;
            _topK = topK;
        }

        /// <summary>
        /// Answer a question using RAG.
        /// </summary>
        /// <param name="question">Question to answer</param>
        /// <param name="numChunks">Number of chunks to retrieve (null for default)</param>
        /// <param name="seed">Random seed for generation (null for random)</param>
        /// <returns>Answer with citations</returns>
        public AnswerWithCitations Answer(string question, int? numChunks = null, int? seed = null)
        {
            // Step 1: Retrieve relevant chunks
            var chunks = _retriever.Retrieve(question, numChunks);

            // Step 2: Build prompt
            var prompt = _promptBuilder.BuildPrompt(question, chunks);

            // Step 3: Generate answer
            var fullResponse = _sampler.Generate(
                prompt: prompt,
                maxNewTokens: _maxTokens,
                temperature: _temperature,
                topK: _topK,
                seed: seed,
                showPerf: false
            );

            // Step 4: Extract answer (remove the prompt part)
            var answer = ExtractAnswer(fullResponse, prompt);

            return new AnswerWithCitations
            {
                Answer = answer,
                Citations = chunks,
                Question = question
            };
        }

        /// <summary>
        /// Answer a question without retrieval (direct LLM call).
        /// </summary>
        public string AnswerDirect(string question, int? seed = null)
        {
            var prompt = _promptBuilder.BuildSimplePrompt(question);
            
            var fullResponse = _sampler.Generate(
                prompt: prompt,
                maxNewTokens: _maxTokens,
                temperature: _temperature,
                topK: _topK,
                seed: seed,
                showPerf: false
            );

            return ExtractAnswer(fullResponse, prompt);
        }

        /// <summary>
        /// Extract the answer from the full response by removing the prompt.
        /// </summary>
        private string ExtractAnswer(string fullResponse, string prompt)
        {
            if (fullResponse.StartsWith(prompt, StringComparison.Ordinal))
            {
                var answer = fullResponse.Substring(prompt.Length).Trim();
                
                // Clean up - stop at natural boundaries
                int qIndex = answer.IndexOf("\nQuestion:", StringComparison.Ordinal);
                if (qIndex > 0)
                {
                    answer = answer.Substring(0, qIndex).Trim();
                }
                
                return answer;
            }
            
            return fullResponse.Trim();
        }

        /// <summary>
        /// Format the answer with citations for display.
        /// </summary>
        public string FormatAnswerWithCitations(AnswerWithCitations result)
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("Question:");
            sb.AppendLine(result.Question);
            sb.AppendLine();
            
            sb.AppendLine("Answer:");
            sb.AppendLine(result.Answer);
            sb.AppendLine();
            
            if (result.Citations != null && result.Citations.Count > 0)
            {
                sb.AppendLine("Sources:");
                for (int i = 0; i < result.Citations.Count; i++)
                {
                    var citation = result.Citations[i];
                    sb.AppendLine($"  [{i + 1}] Score: {citation.Score:F3}");
                    
                    // Truncate long texts for display
                    string preview = citation.Text.Length > 100 
                        ? citation.Text.Substring(0, 100) + "..." 
                        : citation.Text;
                    sb.AppendLine($"      {preview}");
                }
            }
            
            return sb.ToString();
        }
    }
}
