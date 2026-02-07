namespace SmallMind.Retrieval
{
    /// <summary>
    /// Options for RAG prompt building.
    /// </summary>
    public class RagPromptOptions
    {
        /// <summary>
        /// Maximum total characters allowed in the assembled prompt.
        /// </summary>
        public int MaxContextChars { get; set; } = 4000;

        /// <summary>
        /// Maximum number of chunks to include in the prompt.
        /// </summary>
        public int MaxChunksToInclude { get; set; } = 5;

        /// <summary>
        /// Optional system instruction template.
        /// Use {context} and {question} placeholders.
        /// </summary>
        public string? SystemInstructionTemplate { get; set; }

        /// <summary>
        /// Whether to include a sources section at the end listing all citations.
        /// </summary>
        public bool IncludeSourcesSection { get; set; } = true;

        /// <summary>
        /// Citation format for inline citations.
        /// Default: [doc:Title#chunkId]
        /// </summary>
        public string CitationFormat { get; set; } = "[doc:{title}#{chunkId}]";
    }
}
