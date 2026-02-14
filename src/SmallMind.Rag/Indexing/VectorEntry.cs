namespace SmallMind.Rag.Indexing
{
    /// <summary>
    /// Represents a stored vector with its metadata.
    /// </summary>
    internal class VectorEntry
    {
        public string Id { get; set; } = "";
        public float[] Vector { get; set; } = Array.Empty<float>();
        public string Text { get; set; } = "";
        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
    }
}
