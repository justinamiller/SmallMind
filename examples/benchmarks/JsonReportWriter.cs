using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SmallMind.Benchmarks
{
    /// <summary>
    /// Generates JSON-formatted benchmark reports with full system metadata.
    /// </summary>
    public static class JsonReportWriter
    {
        private static readonly JsonSerializerOptions _options = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never
        };

        /// <summary>
        /// Generates a complete JSON report from benchmark results and system info.
        /// </summary>
        public static string GenerateReport(BenchmarkReport report)
        {
            return JsonSerializer.Serialize(report, _options);
        }
    }
}
