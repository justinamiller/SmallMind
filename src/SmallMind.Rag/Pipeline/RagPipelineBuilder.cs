using SmallMind.Rag.Security;
using SmallMind.Rag.Telemetry;

namespace SmallMind.Rag.Pipeline
{
    /// <summary>
    /// Builder pattern for easily configuring and creating RAG pipeline instances.
    /// Provides a fluent API for setting options and dependencies.
    /// </summary>
    internal sealed class RagPipelineBuilder
    {
        private readonly RagOptions _options = new RagOptions();
        private IAuthorizer? _authorizer;
        private IRagLogger? _logger;
        private IRagMetrics? _metrics;

        /// <summary>
        /// Creates a new RAG pipeline builder with default options.
        /// </summary>
        public RagPipelineBuilder()
        {
        }

        /// <summary>
        /// Sets the index directory where the RAG index will be stored.
        /// </summary>
        /// <param name="dir">Path to the index directory.</param>
        /// <returns>This builder for method chaining.</returns>
        public RagPipelineBuilder WithIndexDirectory(string dir)
        {
            if (string.IsNullOrWhiteSpace(dir))
                throw new ArgumentException("Index directory cannot be null or whitespace", nameof(dir));

            _options.IndexDirectory = dir;
            return this;
        }

        /// <summary>
        /// Configures chunking options for document ingestion.
        /// </summary>
        /// <param name="minChars">Minimum chunk size in characters.</param>
        /// <param name="maxChars">Maximum chunk size in characters.</param>
        /// <param name="overlap">Number of overlapping characters between consecutive chunks.</param>
        /// <returns>This builder for method chaining.</returns>
        public RagPipelineBuilder WithChunkingOptions(int minChars, int maxChars, int overlap)
        {
            if (minChars <= 0)
                throw new ArgumentException("Minimum chunk size must be positive", nameof(minChars));
            if (maxChars <= minChars)
                throw new ArgumentException("Maximum chunk size must be greater than minimum", nameof(maxChars));
            if (overlap < 0 || overlap >= maxChars)
                throw new ArgumentException("Overlap must be non-negative and less than max chunk size", nameof(overlap));

            _options.Chunking.MinChunkSize = minChars;
            _options.Chunking.MaxChunkSize = maxChars;
            _options.Chunking.OverlapSize = overlap;
            return this;
        }

        /// <summary>
        /// Configures retrieval options for searching the index.
        /// </summary>
        /// <param name="topK">Number of top chunks to retrieve.</param>
        /// <param name="minScore">Minimum relevance score threshold for retrieved chunks.</param>
        /// <returns>This builder for method chaining.</returns>
        public RagPipelineBuilder WithRetrievalOptions(int topK, float minScore)
        {
            if (topK <= 0)
                throw new ArgumentException("TopK must be positive", nameof(topK));
            if (minScore < 0)
                throw new ArgumentException("Minimum score must be non-negative", nameof(minScore));

            _options.Retrieval.TopK = topK;
            _options.Retrieval.MinScore = minScore;
            return this;
        }

        /// <summary>
        /// Sets the maximum number of context tokens to include in prompts.
        /// </summary>
        /// <param name="maxTokens">Maximum number of tokens.</param>
        /// <returns>This builder for method chaining.</returns>
        public RagPipelineBuilder WithMaxContextTokens(int maxTokens)
        {
            if (maxTokens <= 0)
                throw new ArgumentException("Max context tokens must be positive", nameof(maxTokens));

            // Note: MaxContextTokens is not in RagOptions, but we could extend it later
            // For now, this is a placeholder for future enhancement
            return this;
        }

        /// <summary>
        /// Sets a custom authorizer for access control.
        /// </summary>
        /// <param name="authorizer">The authorizer implementation to use.</param>
        /// <returns>This builder for method chaining.</returns>
        public RagPipelineBuilder WithAuthorizer(IAuthorizer authorizer)
        {
            _authorizer = authorizer ?? throw new ArgumentNullException(nameof(authorizer));
            return this;
        }

        /// <summary>
        /// Sets a custom logger for the pipeline.
        /// </summary>
        /// <param name="logger">The logger implementation to use.</param>
        /// <returns>This builder for method chaining.</returns>
        public RagPipelineBuilder WithLogger(IRagLogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            return this;
        }

        /// <summary>
        /// Sets a custom metrics collector for the pipeline.
        /// </summary>
        /// <param name="metrics">The metrics implementation to use.</param>
        /// <returns>This builder for method chaining.</returns>
        public RagPipelineBuilder WithMetrics(IRagMetrics metrics)
        {
            _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
            return this;
        }

        /// <summary>
        /// Enables deterministic mode with a fixed random seed for reproducible results.
        /// Useful for testing and debugging.
        /// </summary>
        /// <param name="seed">Random seed value.</param>
        /// <returns>This builder for method chaining.</returns>
        public RagPipelineBuilder WithDeterministicMode(int seed)
        {
            _options.Deterministic = true;
            _options.Seed = seed;
            return this;
        }

        /// <summary>
        /// Builds and returns a configured RAG pipeline instance.
        /// The IndexDirectory option must be set before calling Build().
        /// </summary>
        /// <returns>A new RAG pipeline instance.</returns>
        /// <exception cref="InvalidOperationException">Thrown if IndexDirectory is not set.</exception>
        public RagPipeline Build()
        {
            if (string.IsNullOrWhiteSpace(_options.IndexDirectory))
            {
                throw new InvalidOperationException(
                    "IndexDirectory must be set before building the pipeline. Call WithIndexDirectory() first.");
            }

            return new RagPipeline(_options, _authorizer, _logger, _metrics);
        }
    }
}
