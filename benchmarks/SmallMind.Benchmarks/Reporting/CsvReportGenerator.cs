using System.Text;

namespace SmallMind.Benchmarks.Reporting
{
    /// <summary>
    /// Generates CSV reports from benchmark results.
    /// </summary>
    internal static class CsvReportGenerator
    {
        public static void GenerateReport(BenchmarkResults results, string outputPath)
        {
            try
            {
                var directory = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var sb = new StringBuilder();

                // Header
                sb.AppendLine("Metric,Category,Value,Unit,Mean,Median,Min,Max,P50,P95,P99,StdDev");

                // Data rows
                foreach (var metric in results.Metrics.OrderBy(m => m.Category).ThenBy(m => m.Name))
                {
                    var stats = metric.Statistics;

                    sb.Append(EscapeCsv(metric.Name));
                    sb.Append(',');
                    sb.Append(EscapeCsv(metric.Category));
                    sb.Append(',');
                    sb.Append(metric.Value.ToString("F2"));
                    sb.Append(',');
                    sb.Append(EscapeCsv(metric.Unit));

                    if (stats != null)
                    {
                        sb.Append(',');
                        sb.Append(stats.Mean.ToString("F2"));
                        sb.Append(',');
                        sb.Append(stats.Median.ToString("F2"));
                        sb.Append(',');
                        sb.Append(stats.Min.ToString("F2"));
                        sb.Append(',');
                        sb.Append(stats.Max.ToString("F2"));
                        sb.Append(',');
                        sb.Append(stats.P50.ToString("F2"));
                        sb.Append(',');
                        sb.Append(stats.P95.ToString("F2"));
                        sb.Append(',');
                        sb.Append(stats.P99.ToString("F2"));
                        sb.Append(',');
                        sb.Append(stats.StdDev.ToString("F2"));
                    }
                    else
                    {
                        sb.Append(",,,,,,,,");
                    }

                    sb.AppendLine();
                }

                File.WriteAllText(outputPath, sb.ToString());

                Console.WriteLine($"CSV report written to: {outputPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to write CSV report: {ex.Message}");
            }
        }

        private static string EscapeCsv(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            {
                return $"\"{value.Replace("\"", "\"\"")}\"";
            }

            return value;
        }
    }
}
