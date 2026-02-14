using System.Text.Json;

namespace SmallMind.Benchmarks.Reporting
{
    /// <summary>
    /// Generates JSON reports from benchmark results.
    /// </summary>
    internal static class JsonReportGenerator
    {
        private static readonly JsonSerializerOptions s_jsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public static void GenerateReport(BenchmarkResults results, string outputPath)
        {
            try
            {
                var directory = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonSerializer.Serialize(results, s_jsonOptions);
                File.WriteAllText(outputPath, json);

                Console.WriteLine($"JSON report written to: {outputPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to write JSON report: {ex.Message}");
            }
        }

        public static BenchmarkResults? LoadReport(string path)
        {
            try
            {
                if (!File.Exists(path))
                    return null;

                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<BenchmarkResults>(json, s_jsonOptions);
            }
            catch
            {
                return null;
            }
        }
    }
}
