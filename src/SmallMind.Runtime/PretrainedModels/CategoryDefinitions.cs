using System.Text.Json.Serialization;

namespace SmallMind.Runtime.PretrainedModels
{
    /// <summary>
    /// Category definitions for classification tasks.
    /// </summary>
    internal class CategoryDefinitions
    {
        /// <summary>
        /// List of categories.
        /// </summary>
        [JsonPropertyName("categories")]
        public List<CategoryDefinition> Categories { get; set; } = new();

        /// <summary>
        /// Additional notes about categories.
        /// </summary>
        [JsonPropertyName("notes")]
        public string Notes { get; set; } = string.Empty;
    }
}
