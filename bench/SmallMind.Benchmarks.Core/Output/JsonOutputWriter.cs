using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SmallMind.Benchmarks.Core.Output
{
    /// <summary>
    /// JSON output writer for benchmark results.
    /// Uses System.Text.Json - no external dependencies.
    /// </summary>
    public static class JsonOutputWriter
    {
        private static readonly JsonSerializerOptions _options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter() }
        };

        /// <summary>
        /// Write benchmark results to JSON file.
        /// </summary>
        public static void WriteToFile(Measurement.BenchmarkRunResults results, string outputPath)
        {
            if (results == null)
                throw new ArgumentNullException(nameof(results));
            if (string.IsNullOrWhiteSpace(outputPath))
                throw new ArgumentException("Output path cannot be null or empty", nameof(outputPath));

            string json = JsonSerializer.Serialize(results, _options);
            
            // Ensure directory exists
            string? directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(outputPath, json);
        }

        /// <summary>
        /// Serialize benchmark results to JSON string.
        /// </summary>
        public static string Serialize(Measurement.BenchmarkRunResults results)
        {
            if (results == null)
                throw new ArgumentNullException(nameof(results));

            return JsonSerializer.Serialize(results, _options);
        }

        /// <summary>
        /// Deserialize benchmark results from JSON string.
        /// </summary>
        public static Measurement.BenchmarkRunResults? Deserialize(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("JSON string cannot be null or empty", nameof(json));

            return JsonSerializer.Deserialize<Measurement.BenchmarkRunResults>(json, _options);
        }

        /// <summary>
        /// Read benchmark results from JSON file.
        /// </summary>
        public static Measurement.BenchmarkRunResults? ReadFromFile(string inputPath)
        {
            if (!File.Exists(inputPath))
                throw new FileNotFoundException($"File not found: {inputPath}", inputPath);

            string json = File.ReadAllText(inputPath);
            return Deserialize(json);
        }
    }
}
