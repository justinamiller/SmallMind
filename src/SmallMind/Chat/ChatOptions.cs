using System.Collections.Generic;

namespace SmallMind.Chat
{
    /// <summary>
    /// Options for chat completion requests.
    /// </summary>
    public class ChatOptions
    {
        /// <summary>
        /// Whether to use deterministic mode (fixed seed).
        /// </summary>
        public bool Deterministic { get; set; } = true;

        /// <summary>
        /// Random seed for deterministic mode.
        /// </summary>
        public int? Seed { get; set; } = 42;

        /// <summary>
        /// Maximum tokens to generate.
        /// </summary>
        public int MaxTokens { get; set; } = 200;

        /// <summary>
        /// Temperature for sampling (0.0 = deterministic, higher = more random).
        /// </summary>
        public double Temperature { get; set; } = 0.7;

        /// <summary>
        /// Top-K sampling parameter.
        /// </summary>
        public int TopK { get; set; } = 40;

        /// <summary>
        /// Maximum context characters for prompt assembly.
        /// </summary>
        public int MaxContextChars { get; set; } = 4000;

        /// <summary>
        /// Maximum workflow steps.
        /// </summary>
        public int MaxSteps { get; set; } = 10;

        /// <summary>
        /// Number of top chunks to retrieve.
        /// </summary>
        public int TopKRetrieval { get; set; } = 5;

        /// <summary>
        /// Whether to return citations in the response.
        /// </summary>
        public bool ReturnCitations { get; set; } = true;

        /// <summary>
        /// Whether to return structured output.
        /// </summary>
        public bool ReturnStructured { get; set; } = false;

        /// <summary>
        /// Whether to use RAG (retrieval-augmented generation).
        /// </summary>
        public bool UseRag { get; set; } = true;
    }

    /// <summary>
    /// Response from a chat completion request.
    /// </summary>
    public class ChatResponse
    {
        /// <summary>
        /// The generated text response.
        /// </summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>
        /// Citations included in the response (if requested).
        /// </summary>
        public List<string> Citations { get; set; } = new List<string>();

        /// <summary>
        /// Diagnostic information (if available).
        /// </summary>
        public Dictionary<string, string> Diagnostics { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Structured output (if requested).
        /// </summary>
        public object? StructuredOutput { get; set; }

        /// <summary>
        /// State keys that were updated during this turn.
        /// </summary>
        public List<string> UpdatedSessionStateKeys { get; set; } = new List<string>();

        /// <summary>
        /// Session ID for this conversation.
        /// </summary>
        public string SessionId { get; set; } = string.Empty;
    }
}
