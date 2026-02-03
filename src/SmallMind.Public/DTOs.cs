using System;
using System.Collections.Generic;

namespace SmallMind.Public
{
    /// <summary>
    /// Request for text generation.
    /// Use as 'in' parameter to avoid copying.
    /// </summary>
    public readonly struct TextGenerationRequest
    {
        /// <summary>
        /// Gets the input prompt.
        /// </summary>
        public ReadOnlyMemory<char> Prompt { get; init; }

        /// <summary>
        /// Gets the optional maximum output tokens override for this request only.
        /// If null, uses the session's MaxOutputTokens.
        /// </summary>
        public int? MaxOutputTokensOverride { get; init; }

        /// <summary>
        /// Gets the optional stop sequences override for this request only.
        /// If empty, uses the session's StopSequences.
        /// </summary>
        public ReadOnlyMemory<string> StopSequencesOverride { get; init; }

        /// <summary>
        /// Gets the optional seed for deterministic generation.
        /// If null, uses non-deterministic sampling.
        /// </summary>
        public uint? Seed { get; init; }
    }

    /// <summary>
    /// Request for embedding generation.
    /// Use as 'in' parameter to avoid copying.
    /// </summary>
    public readonly struct EmbeddingRequest
    {
        /// <summary>
        /// Gets the input text to embed.
        /// </summary>
        public ReadOnlyMemory<char> Input { get; init; }
    }

    /// <summary>
    /// Result of text generation (non-streaming).
    /// </summary>
    public sealed class GenerationResult
    {
        /// <summary>
        /// Gets the generated text.
        /// </summary>
        public string Text { get; init; } = string.Empty;

        /// <summary>
        /// Gets the token usage for this generation.
        /// </summary>
        public Usage Usage { get; init; }

        /// <summary>
        /// Gets the timing information for this generation.
        /// </summary>
        public GenerationTimings Timings { get; init; }

        /// <summary>
        /// Gets the reason generation finished.
        /// </summary>
        public FinishReason FinishReason { get; init; }

        /// <summary>
        /// Gets optional warnings encountered during generation.
        /// </summary>
        public IReadOnlyList<string>? Warnings { get; init; }
    }

    /// <summary>
    /// Token usage information.
    /// </summary>
    public readonly struct Usage
    {
        /// <summary>
        /// Gets the number of prompt tokens.
        /// </summary>
        public int PromptTokens { get; init; }

        /// <summary>
        /// Gets the number of completion (generated) tokens.
        /// </summary>
        public int CompletionTokens { get; init; }

        /// <summary>
        /// Gets the total number of tokens (prompt + completion).
        /// </summary>
        public int TotalTokens => PromptTokens + CompletionTokens;
    }

    /// <summary>
    /// Timing information for generation.
    /// </summary>
    public readonly struct GenerationTimings
    {
        /// <summary>
        /// Gets the time to first token in milliseconds.
        /// </summary>
        public double TimeToFirstTokenMs { get; init; }

        /// <summary>
        /// Gets the total generation time in milliseconds.
        /// </summary>
        public double TotalMs { get; init; }

        /// <summary>
        /// Gets the generation speed in tokens per second.
        /// </summary>
        public double TokensPerSecond => CompletionTokens > 0 && TotalMs > 0
            ? CompletionTokens / (TotalMs / 1000.0)
            : 0.0;

        /// <summary>
        /// Gets the number of completion tokens for rate calculation.
        /// </summary>
        private int CompletionTokens { get; init; }

        /// <summary>
        /// Creates timing information.
        /// </summary>
        /// <param name="timeToFirstTokenMs">Time to first token.</param>
        /// <param name="totalMs">Total time.</param>
        /// <param name="completionTokens">Number of generated tokens.</param>
        public GenerationTimings(double timeToFirstTokenMs, double totalMs, int completionTokens)
        {
            TimeToFirstTokenMs = timeToFirstTokenMs;
            TotalMs = totalMs;
            CompletionTokens = completionTokens;
        }
    }

    /// <summary>
    /// Partial usage information during streaming.
    /// </summary>
    public readonly struct UsagePartial
    {
        /// <summary>
        /// Gets the number of prompt tokens.
        /// </summary>
        public int PromptTokens { get; init; }

        /// <summary>
        /// Gets the number of completion tokens generated so far.
        /// </summary>
        public int CompletionTokensSoFar { get; init; }
    }

    /// <summary>
    /// Partial timing information during streaming.
    /// </summary>
    public readonly struct GenerationTimingsPartial
    {
        /// <summary>
        /// Gets the time to first token in milliseconds (if available).
        /// </summary>
        public double? TimeToFirstTokenMs { get; init; }

        /// <summary>
        /// Gets the elapsed time so far in milliseconds.
        /// </summary>
        public double ElapsedMs { get; init; }
    }

    /// <summary>
    /// Reason why text generation finished.
    /// </summary>
    public enum FinishReason
    {
        /// <summary>
        /// Generation completed naturally (EOS token or max length).
        /// </summary>
        Completed,

        /// <summary>
        /// Generation stopped due to a stop sequence.
        /// </summary>
        StopSequence,

        /// <summary>
        /// Generation stopped due to length limit.
        /// </summary>
        Length,

        /// <summary>
        /// Generation was cancelled by the user.
        /// </summary>
        Cancelled,

        /// <summary>
        /// Generation stopped due to timeout.
        /// </summary>
        Timeout,

        /// <summary>
        /// Generation stopped due to an error.
        /// </summary>
        Error
    }

    /// <summary>
    /// Result of a single token during streaming.
    /// </summary>
    public sealed class TokenResult
    {
        /// <summary>
        /// Gets the text representation of this token.
        /// </summary>
        public string TokenText { get; init; } = string.Empty;

        /// <summary>
        /// Gets the token ID.
        /// </summary>
        public int TokenId { get; init; }

        /// <summary>
        /// Gets whether this is a special token (e.g., EOS, BOS).
        /// </summary>
        public bool IsSpecial { get; init; }

        /// <summary>
        /// Gets the partial usage information (if available).
        /// </summary>
        public UsagePartial? Usage { get; init; }

        /// <summary>
        /// Gets the partial timing information (if available).
        /// </summary>
        public GenerationTimingsPartial? Timings { get; init; }
    }

    /// <summary>
    /// Result of embedding generation.
    /// </summary>
    public sealed class EmbeddingResult
    {
        /// <summary>
        /// Gets the embedding vector.
        /// </summary>
        public float[] Vector { get; init; } = Array.Empty<float>();

        /// <summary>
        /// Gets the number of dimensions in the embedding.
        /// </summary>
        public int Dimensions => Vector.Length;

        /// <summary>
        /// Gets the token usage for this embedding.
        /// </summary>
        public Usage Usage { get; init; }
    }
}
