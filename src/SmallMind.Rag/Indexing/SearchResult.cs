namespace SmallMind.Rag.Indexing
{
    /// <summary>
    /// Result from a vector search query.
    /// </summary>
    internal class SearchResult
    {
        public string Id { get; set; } = "";
        public string Text { get; set; } = "";
        public float Score { get; set; }
        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
    }
}
