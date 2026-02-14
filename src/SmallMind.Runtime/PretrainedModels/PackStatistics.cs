using System.Text.Json.Serialization;

namespace SmallMind.Runtime.PretrainedModels
{
    /// <summary>
    /// Statistical information about a pack.
    /// </summary>
    internal class PackStatistics
    {
        /// <summary>
        /// Total number of samples.
        /// </summary>
        [JsonPropertyName("total_samples")]
        public int TotalSamples { get; set; }

        /// <summary>
        /// Distribution of labels.
        /// </summary>
        [JsonPropertyName("label_distribution")]
        public Dictionary<string, int> LabelDistribution { get; set; } = new();
    }
}
