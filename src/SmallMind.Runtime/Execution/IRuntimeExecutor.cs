namespace SmallMind.Runtime.Execution
{
    /// <summary>
    /// Runtime executor interface for hard prefill/decode separation.
    /// Provides explicit APIs for prompt processing and single-token generation.
    /// Internal interface - not exposed in public API.
    /// </summary>
    internal interface IRuntimeExecutor
    {
        /// <summary>
        /// Prefill phase: Processes the entire prompt to populate KV cache.
        /// Returns logits for the last token and a cache handle for decode.
        /// </summary>
        /// <param name="promptTokens">Full prompt token sequence</param>
        /// <param name="context">Execution context (cache will be populated)</param>
        /// <returns>Prefill result with logits and cache handle</returns>
        PrefillResult Prefill(ReadOnlySpan<int> promptTokens, ExecutionContext context);

        /// <summary>
        /// Decode phase: Processes a single token to generate the next token.
        /// Uses KV cache from context to avoid recomputing previous tokens.
        /// </summary>
        /// <param name="nextToken">Single token to process</param>
        /// <param name="context">Execution context with populated cache</param>
        /// <returns>Decode result with logits for sampling</returns>
        DecodeResult Decode(int nextToken, ExecutionContext context);
    }
}
