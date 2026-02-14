using System.Text.Json.Serialization;

namespace SmallMind.Runtime.PretrainedModels
{
    /// <summary>
    /// Definition of a single category.
    /// </summary>
    internal class CategoryDefinition
    {
        /// <summary>
        /// Category identifier.
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Category name.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Category description.
        /// </summary>
        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Example texts for this category.
        /// </summary>
        [JsonPropertyName("examples")]
        public List<string> Examples { get; set; } = new();
    }
}
