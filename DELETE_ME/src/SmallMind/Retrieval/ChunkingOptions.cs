namespace SmallMind.Retrieval
{
    /// <summary>
    /// Configuration options for text chunking.
    /// </summary>
    public class ChunkingOptions
    {
        /// <summary>
        /// Maximum characters per chunk.
        /// </summary>
        public int MaxChars { get; set; } = 500;

        /// <summary>
        /// Number of characters to overlap between consecutive chunks.
        /// </summary>
        public int OverlapChars { get; set; } = 50;

        /// <summary>
        /// Minimum chunk size in characters. Chunks smaller than this will be merged or skipped.
        /// </summary>
        public int MinChunkChars { get; set; } = 50;

        /// <summary>
        /// Whether to prefer splitting on paragraph boundaries (double newlines).
        /// </summary>
        public bool PreferParagraphBoundaries { get; set; } = true;

        /// <summary>
        /// Whether to prefer splitting on sentence boundaries (periods, question marks, exclamation points).
        /// </summary>
        public bool PreferSentenceBoundaries { get; set; } = true;
    }
}
