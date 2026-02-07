using System;
using System.Collections.Generic;

namespace SmallMind.Retrieval
{
    /// <summary>
    /// Represents a document in the retrieval system.
    /// </summary>
    public class Document
    {
        /// <summary>
        /// Unique identifier for the document.
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Optional title of the document.
        /// </summary>
        public string? Title { get; set; }

        /// <summary>
        /// Optional source URI for the document.
        /// </summary>
        public string? SourceUri { get; set; }

        /// <summary>
        /// The full content of the document.
        /// </summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// Optional tags for categorization and filtering.
        /// </summary>
        public HashSet<string> Tags { get; set; } = new HashSet<string>();

        /// <summary>
        /// Optional metadata as key-value pairs.
        /// </summary>
        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
    }
}
