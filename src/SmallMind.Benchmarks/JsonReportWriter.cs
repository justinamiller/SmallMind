using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SmallMind.Benchmarks
{
    /// <summary>
    /// Writes benchmark results to JSON format.
    /// </summary>
    public static class JsonReportWriter
    {
        private static readonly JsonSerializerOptions _options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        /// <summary>
        /// Write benchmark results to JSON file.
        /// </summary>
        public static void WriteReport(string filePath, List<BenchmarkResult> results)
        {
            var report = new
            {
                timestamp = DateTime.UtcNow,
                results = results
            };

            var json = JsonSerializer.Serialize(report, _options);
            File.WriteAllText(filePath, json);
        }

        /// <summary>
        /// Read benchmark results from JSON file.
        /// </summary>
        public static List<BenchmarkResult>? ReadBaseline(string filePath)
        {
            if (!File.Exists(filePath))
                return null;

            try
            {
                var json = File.ReadAllText(filePath);
                var document = JsonDocument.Parse(json);
                
                if (document.RootElement.TryGetProperty("results", out var resultsElement))
                {
                    return JsonSerializer.Deserialize<List<BenchmarkResult>>(resultsElement.GetRawText(), _options);
                }
            }
            catch
            {
                // Ignore errors reading baseline
            }

            return null;
        }
    }
}
