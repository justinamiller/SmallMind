using System.Text.Json.Serialization;

namespace SmallMind.Runtime.PretrainedModels
{
    /// <summary>
    /// Task-specific metadata.
    /// </summary>
    internal class TaskMetadata
    {
        /// <summary>
        /// Task type (e.g., "sentiment_analysis", "text_classification").
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Labels used in this task.
        /// </summary>
        [JsonPropertyName("labels")]
        public List<string> Labels { get; set; } = new();

        /// <summary>
        /// Description of the task.
        /// </summary>
        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;
    }
}
