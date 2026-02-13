using System;
using System.IO;
using System.Text;

namespace SmallMind.Benchmarks.Core.Output
{
    /// <summary>
    /// CSV output writer for benchmark results.
    /// Compatible with Excel and data analysis tools.
    /// </summary>
    public static class CsvOutputWriter
    {
        /// <summary>
        /// Write benchmark results to CSV file.
        /// </summary>
        public static void WriteToFile(Measurement.BenchmarkRunResults results, string outputPath)
        {
            if (results == null)
                throw new ArgumentNullException(nameof(results));
            if (string.IsNullOrWhiteSpace(outputPath))
                throw new ArgumentException("Output path cannot be null or empty", nameof(outputPath));

            string csv = GenerateCsv(results);

            // Ensure directory exists
            string? directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(outputPath, csv);
        }

        /// <summary>
        /// Generate CSV string from benchmark results.
        /// </summary>
        public static string GenerateCsv(Measurement.BenchmarkRunResults results)
        {
            if (results == null)
                throw new ArgumentNullException(nameof(results));

            var sb = new StringBuilder();

            // Metadata comments
            sb.AppendLine($"# SmallMind Benchmark Results");
            sb.AppendLine($"# Run ID: {results.RunId}");
            sb.AppendLine($"# Start Time: {results.StartTime:yyyy-MM-dd HH:mm:ss} UTC");
            sb.AppendLine($"# End Time: {results.EndTime:yyyy-MM-dd HH:mm:ss} UTC");
            sb.AppendLine($"# Git Commit: {results.GitCommitSha}");
            
            if (results.Environment != null)
            {
                sb.AppendLine($"# OS: {EscapeCsvValue(results.Environment.OperatingSystem)}");
                sb.AppendLine($"# Runtime: {results.Environment.RuntimeVersion}");
                sb.AppendLine($"# CPU: {EscapeCsvValue(results.Environment.CpuModel ?? "unknown")}");
                sb.AppendLine($"# Cores: {results.Environment.LogicalCores}");
                
                if (results.Environment.CpuMaxFrequencyMhz.HasValue)
                {
                    sb.AppendLine($"# CPU Frequency: {results.Environment.CpuMaxFrequencyMhz.Value} MHz");
                }
                
                sb.AppendLine($"# SIMD: {string.Join(" ", results.Environment.SimdCapabilities)}");
            }

            sb.AppendLine();

            // Header row
            sb.AppendLine("Model,ContextSize,ThreadCount,PromptTokens,GeneratedTokens," +
                "TTFT_ms,TotalTime_ms,Tok_per_s,Tok_per_s_SteadyState," +
                "PeakRSS_MB,ModelLoadRSS_MB,BytesPerToken,BytesPerSecond," +
                "Gen0Collections,Gen1Collections,Gen2Collections," +
                "MedianTok_per_s,P90Tok_per_s,StdDevTok_per_s,Timestamp");

            // Data rows
            foreach (var result in results.Results)
            {
                sb.Append(EscapeCsvValue(result.ModelName));
                sb.Append(',');
                sb.Append(result.ContextSize);
                sb.Append(',');
                sb.Append(result.ThreadCount);
                sb.Append(',');
                sb.Append(result.PromptTokens);
                sb.Append(',');
                sb.Append(result.GeneratedTokens);
                sb.Append(',');
                sb.Append(result.TTFTMilliseconds.ToString("F3"));
                sb.Append(',');
                sb.Append(result.TotalTimeMilliseconds.ToString("F3"));
                sb.Append(',');
                sb.Append(result.TokensPerSecond.ToString("F3"));
                sb.Append(',');
                sb.Append(result.TokensPerSecondSteadyState.ToString("F3"));
                sb.Append(',');
                sb.Append((result.PeakRssBytes / (1024.0 * 1024.0)).ToString("F2"));
                sb.Append(',');
                sb.Append((result.ModelLoadRssBytes / (1024.0 * 1024.0)).ToString("F2"));
                sb.Append(',');
                sb.Append(result.BytesAllocatedPerToken);
                sb.Append(',');
                sb.Append(result.BytesAllocatedPerSecond);
                sb.Append(',');
                sb.Append(result.Gen0Collections);
                sb.Append(',');
                sb.Append(result.Gen1Collections);
                sb.Append(',');
                sb.Append(result.Gen2Collections);
                sb.Append(',');
                sb.Append(result.MedianTokensPerSecond.ToString("F3"));
                sb.Append(',');
                sb.Append(result.P90TokensPerSecond.ToString("F3"));
                sb.Append(',');
                sb.Append(result.StdDevTokensPerSecond.ToString("F3"));
                sb.Append(',');
                sb.Append(result.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"));
                sb.AppendLine();
            }

            // Normalized results in separate section
            if (results.NormalizedResults != null && results.NormalizedResults.Length > 0)
            {
                sb.AppendLine();
                sb.AppendLine("# Normalized Efficiency Metrics");
                sb.AppendLine("Model,ContextSize,ThreadCount," +
                    "Tok_per_s_per_Core,Tok_per_s_per_GHz_per_Core,Cycles_per_Token,Notes");

                foreach (var norm in results.NormalizedResults)
                {
                    sb.Append(EscapeCsvValue(norm.ModelName));
                    sb.Append(',');
                    sb.Append(norm.ContextSize);
                    sb.Append(',');
                    sb.Append(norm.ThreadCount);
                    sb.Append(',');
                    sb.Append(FormatCsvNumber(norm.TokensPerSecondPerCore));
                    sb.Append(',');
                    sb.Append(FormatCsvNumber(norm.TokensPerSecondPerGHzPerCore));
                    sb.Append(',');
                    sb.Append(FormatCsvNumber(norm.CyclesPerToken));
                    sb.Append(',');
                    sb.Append(EscapeCsvValue(norm.Notes));
                    sb.AppendLine();
                }
            }

            return sb.ToString();
        }

        private static string EscapeCsvValue(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            // If value contains comma, quotes, or newlines, wrap in quotes and escape quotes
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            {
                return '"' + value.Replace("\"", "\"\"") + '"';
            }

            return value;
        }

        private static string FormatCsvNumber(double value)
        {
            if (value <= 0)
                return string.Empty; // Empty for N/A values

            return value.ToString("F3");
        }
    }
}
