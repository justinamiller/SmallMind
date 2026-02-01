using System;
using System.Collections.Generic;

namespace SmallMind.Retrieval
{
    /// <summary>
    /// Represents a chunk of text from a document.
    /// </summary>
    public class DocumentChunk
    {
        /// <summary>
        /// Unique identifier for this chunk.
        /// </summary>
        public string ChunkId { get; set; } = string.Empty;

        /// <summary>
        /// ID of the parent document.
        /// </summary>
        public string DocumentId { get; set; } = string.Empty;

        /// <summary>
        /// Start offset in the original document content (character index).
        /// </summary>
        public int StartOffset { get; set; }

        /// <summary>
        /// End offset in the original document content (character index).
        /// </summary>
        public int EndOffset { get; set; }

        /// <summary>
        /// The actual text content of this chunk.
        /// </summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>
        /// Optional metadata for this chunk.
        /// </summary>
        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
    }
}
