using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace CodeProfiler
{
    /// <summary>
    /// Compares two profiling reports and generates a comprehensive comparison analysis.
    /// </summary>
    public static class ProfileComparator
    {
        public class ProfileData
        {
            public string Name { get; set; } = "";
            public double TimeMs { get; set; }
            public int Calls { get; set; }
            public double AvgMs { get; set; }
            public double AllocMB { get; set; }
        }

        public class ComparisonResult
        {
            public string Method { get; set; } = "";
            public double PreviousTime { get; set; }
            public double CurrentTime { get; set; }
            public double TimeDelta { get; set; }
            public double TimeChangePercent { get; set; }
            public double PreviousAlloc { get; set; }
            public double CurrentAlloc { get; set; }
            public double AllocDelta { get; set; }
            public string Status { get; set; } = ""; // "Improved", "Regressed", "Unchanged"
        }

        public static Dictionary<string, ProfileData> ParseProfileReport(string reportPath)
        {
            var profiles = new Dictionary<string, ProfileData>();
            
            if (!File.Exists(reportPath))
            {
                Console.WriteLine($"Warning: Report file not found: {reportPath}");
                return profiles;
            }

            var lines = File.ReadAllLines(reportPath);
            bool inHotPathsSection = false;

            foreach (var line in lines)
            {
                if (line.Contains("🔥 Hot Paths"))
                {
                    inHotPathsSection = true;
                    continue;
                }

                if (line.Contains("💾 Top Allocators"))
                {
                    inHotPathsSection = false;
                    continue;
                }

                if (inHotPathsSection && line.StartsWith("| ") && !line.Contains("Rank") && !line.Contains("---"))
                {
                    var parts = line.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    if (parts.Length >= 5)
                    {
                        try
                        {
                            var methodName = parts[1].Trim('`');
                            var timeStr = parts[2].Replace(",", "");
                            var callsStr = parts[3].Replace(",", "");
                            var avgStr = parts[4].Replace(",", "");
                            var allocStr = parts.Length > 5 ? parts[5].Replace(",", "") : "0";

                            profiles[methodName] = new ProfileData
                            {
                                Name = methodName,
                                TimeMs = double.Parse(timeStr),
                                Calls = int.Parse(callsStr),
                                AvgMs = double.Parse(avgStr),
                                AllocMB = double.Parse(allocStr)
                            };
                        }
                        catch
                        {
                            // Skip malformed lines
                        }
                    }
                }
            }

            return profiles;
        }

        public static List<ComparisonResult> CompareReports(
            Dictionary<string, ProfileData> previous,
            Dictionary<string, ProfileData> current)
        {
            var results = new List<ComparisonResult>();

            // Get all unique method names
            var allMethods = previous.Keys.Union(current.Keys).ToHashSet();

            foreach (var method in allMethods)
            {
                var prevExists = previous.TryGetValue(method, out var prevData);
                var currExists = current.TryGetValue(method, out var currData);

                if (prevExists && currExists)
                {
                    var timeDelta = currData.TimeMs - prevData.TimeMs;
                    var timeChangePercent = prevData.TimeMs > 0 ? (timeDelta / prevData.TimeMs) * 100 : 0;
                    var allocDelta = currData.AllocMB - prevData.AllocMB;

                    string status;
                    if (Math.Abs(timeChangePercent) < 5)
                        status = "Unchanged";
                    else if (timeChangePercent < 0)
                        status = "Improved";
                    else
                        status = "Regressed";

                    results.Add(new ComparisonResult
                    {
                        Method = method,
                        PreviousTime = prevData.TimeMs,
                        CurrentTime = currData.TimeMs,
                        TimeDelta = timeDelta,
                        TimeChangePercent = timeChangePercent,
                        PreviousAlloc = prevData.AllocMB,
                        CurrentAlloc = currData.AllocMB,
                        AllocDelta = allocDelta,
                        Status = status
                    });
                }
            }

            return results.OrderByDescending(r => Math.Abs(r.TimeDelta)).ToList();
        }

        public static string GenerateComparisonReport(
            Dictionary<string, ProfileData> previous,
            Dictionary<string, ProfileData> current,
            string previousTimestamp,
            string currentTimestamp)
        {
            var sb = new StringBuilder();
            var comparisons = CompareReports(previous, current);

            sb.AppendLine("# Profile Comparison Report");
            sb.AppendLine();
            sb.AppendLine($"**Previous Run:** {previousTimestamp}");
            sb.AppendLine($"**Current Run:** {currentTimestamp}");
            sb.AppendLine();

            // Overall summary
            var previousTotal = previous.Values.Sum(p => p.TimeMs);
            var currentTotal = current.Values.Sum(p => p.TimeMs);
            var totalDelta = currentTotal - previousTotal;
            var totalChangePercent = previousTotal > 0 ? (totalDelta / previousTotal) * 100 : 0;

            var previousAllocTotal = previous.Values.Sum(p => p.AllocMB);
            var currentAllocTotal = current.Values.Sum(p => p.AllocMB);

            sb.AppendLine("## 📊 Overall Performance Summary");
            sb.AppendLine();
            sb.AppendLine("| Metric | Previous | Current | Delta | Change % |");
            sb.AppendLine("|--------|----------|---------|-------|----------|");
            sb.AppendLine($"| **Total Runtime** | {previousTotal:F2} ms | {currentTotal:F2} ms | {(totalDelta >= 0 ? "+" : "")}{totalDelta:F2} ms | {(totalChangePercent >= 0 ? "+" : "")}{totalChangePercent:F1}% |");
            var allocDelta = currentAllocTotal - previousAllocTotal;
            var allocPercent = previousAllocTotal > 0 ? (allocDelta / previousAllocTotal * 100) : 0;
            sb.AppendLine($"| **Total Allocations** | {previousAllocTotal:F2} MB | {currentAllocTotal:F2} MB | {(allocDelta >= 0 ? "+" : "")}{allocDelta:F2} MB | {(allocPercent >= 0 ? "+" : "")}{allocPercent:F1}% |");
            sb.AppendLine($"| **Methods Profiled** | {previous.Count} | {current.Count} | {(current.Count >= previous.Count ? "+" : "")}{current.Count - previous.Count} | - |");
            sb.AppendLine();

            // Performance verdict
            sb.AppendLine("### 🎯 Performance Verdict");
            sb.AppendLine();
            if (totalChangePercent < -10)
                sb.AppendLine($"✅ **IMPROVED**: Overall performance improved by {-totalChangePercent:F1}%");
            else if (totalChangePercent > 10)
                sb.AppendLine($"⚠️ **REGRESSED**: Overall performance degraded by {totalChangePercent:F1}%");
            else
                sb.AppendLine($"➡️ **STABLE**: Performance remained within 10% tolerance ({totalChangePercent:+F1}%)");
            sb.AppendLine();

            // Top improvements
            var improvements = comparisons.Where(c => c.Status == "Improved").Take(10).ToList();
            if (improvements.Any())
            {
                sb.AppendLine("## 🚀 Top 10 Improvements");
                sb.AppendLine();
                sb.AppendLine("| Method | Previous (ms) | Current (ms) | Delta (ms) | Change % |");
                sb.AppendLine("|--------|---------------|--------------|------------|----------|");
                foreach (var comp in improvements)
                {
                    sb.AppendLine($"| `{comp.Method}` | {comp.PreviousTime:F2} | {comp.CurrentTime:F2} | {comp.TimeDelta:F2} | {comp.TimeChangePercent:F1}% |");
                }
                sb.AppendLine();
            }

            // Top regressions
            var regressions = comparisons.Where(c => c.Status == "Regressed").Take(10).ToList();
            if (regressions.Any())
            {
                sb.AppendLine("## ⚠️ Top 10 Regressions");
                sb.AppendLine();
                sb.AppendLine("| Method | Previous (ms) | Current (ms) | Delta (ms) | Change % |");
                sb.AppendLine("|--------|---------------|--------------|------------|----------|");
                foreach (var comp in regressions)
                {
                    var deltaSign = comp.TimeDelta >= 0 ? "+" : "";
                    var percentSign = comp.TimeChangePercent >= 0 ? "+" : "";
                    sb.AppendLine($"| `{comp.Method}` | {comp.PreviousTime:F2} | {comp.CurrentTime:F2} | {deltaSign}{comp.TimeDelta:F2} | {percentSign}{comp.TimeChangePercent:F1}% |");
                }
                sb.AppendLine();
            }

            // Detailed comparison table
            sb.AppendLine("## 📋 Detailed Method Comparison");
            sb.AppendLine();
            sb.AppendLine("| Method | Prev Time (ms) | Curr Time (ms) | Time Δ | Time Δ% | Prev Alloc (MB) | Curr Alloc (MB) | Status |");
            sb.AppendLine("|--------|----------------|----------------|--------|---------|-----------------|-----------------|--------|");

            foreach (var comp in comparisons.Take(30))
            {
                var statusIcon = comp.Status switch
                {
                    "Improved" => "✅",
                    "Regressed" => "⚠️",
                    _ => "➡️"
                };

                var deltaSign = comp.TimeDelta >= 0 ? "+" : "";
                var percentSign = comp.TimeChangePercent >= 0 ? "+" : "";

                sb.AppendLine($"| `{comp.Method}` | {comp.PreviousTime:F2} | {comp.CurrentTime:F2} | " +
                    $"{deltaSign}{comp.TimeDelta:F2} | {percentSign}{comp.TimeChangePercent:F1}% | " +
                    $"{comp.PreviousAlloc:F2} | {comp.CurrentAlloc:F2} | {statusIcon} {comp.Status} |");
            }

            sb.AppendLine();

            // Model-specific analysis
            GenerateModelComparison(sb, comparisons);

            return sb.ToString();
        }

        private static void GenerateModelComparison(StringBuilder sb, List<ComparisonResult> comparisons)
        {
            sb.AppendLine("## 🔬 Model Size Comparison");
            sb.AppendLine();

            // Extract model-specific comparisons
            var smallModel = comparisons.FirstOrDefault(c => c.Method == "Model_Small_Inference");
            var mediumModel = comparisons.FirstOrDefault(c => c.Method == "Model_Medium_Inference");

            if (smallModel != null && mediumModel != null)
            {
                sb.AppendLine("### Small vs Medium Model Performance");
                sb.AppendLine();
                sb.AppendLine("| Model | Previous (ms) | Current (ms) | Delta | Change % | Alloc (MB) |");
                sb.AppendLine("|-------|---------------|--------------|-------|----------|------------|");
                var smallDelta = smallModel.TimeDelta >= 0 ? "+" : "";
                var smallPercent = smallModel.TimeChangePercent >= 0 ? "+" : "";
                var medDelta = mediumModel.TimeDelta >= 0 ? "+" : "";
                var medPercent = mediumModel.TimeChangePercent >= 0 ? "+" : "";
                sb.AppendLine($"| **Small** (128 dim, 2 layers) | {smallModel.PreviousTime:F2} | {smallModel.CurrentTime:F2} | {smallDelta}{smallModel.TimeDelta:F2} | {smallPercent}{smallModel.TimeChangePercent:F1}% | {smallModel.CurrentAlloc:F2} |");
                sb.AppendLine($"| **Medium** (256 dim, 4 layers) | {mediumModel.PreviousTime:F2} | {mediumModel.CurrentTime:F2} | {medDelta}{mediumModel.TimeDelta:F2} | {medPercent}{mediumModel.TimeChangePercent:F1}% | {mediumModel.CurrentAlloc:F2} |");
                sb.AppendLine();

                // Calculate ratios
                var currentRatio = mediumModel.CurrentTime / smallModel.CurrentTime;
                sb.AppendLine("### Scaling Analysis");
                sb.AppendLine();
                sb.AppendLine($"- **Medium/Small Time Ratio:** {currentRatio:F2}x");
                sb.AppendLine($"- **Medium Model Parameters:** ~7.3x more parameters than Small");
                sb.AppendLine($"- **Computational Efficiency:** {(7.3 / currentRatio):F2}x (ideal: 1.0x, higher is better)");
                sb.AppendLine();

                // Throughput analysis
                var smallThroughput = 25.0 / (smallModel.CurrentTime / 1000.0); // tokens per second
                var mediumThroughput = 25.0 / (mediumModel.CurrentTime / 1000.0);
                
                sb.AppendLine("### Inference Throughput (25 tokens)");
                sb.AppendLine();
                sb.AppendLine("| Model | Tokens/Second | Latency/Token (ms) | Memory/Token (MB) |");
                sb.AppendLine("|-------|---------------|-------------------|-------------------|");
                sb.AppendLine($"| **Small** | {smallThroughput:F2} | {smallModel.CurrentTime / 25.0:F2} | {smallModel.CurrentAlloc / 25.0:F2} |");
                sb.AppendLine($"| **Medium** | {mediumThroughput:F2} | {mediumModel.CurrentTime / 25.0:F2} | {mediumModel.CurrentAlloc / 25.0:F2} |");
                sb.AppendLine();
            }

            // MatMul performance analysis
            sb.AppendLine("### SIMD Operation Performance");
            sb.AppendLine();
            sb.AppendLine("| Operation | Previous (ms) | Current (ms) | Delta | Change % | GFLOPS |");
            sb.AppendLine("|-----------|---------------|--------------|-------|----------|--------|");

            var matmulOps = new[] { "MatMul_128x128", "MatMul_256x256", "MatMul_512x512" };
            foreach (var op in matmulOps)
            {
                var comp = comparisons.FirstOrDefault(c => c.Method == op);
                if (comp != null)
                {
                    // Calculate GFLOPS: 2*M*N*K / (time_in_seconds * 1e9)
                    var size = int.Parse(op.Split('_')[1].Split('x')[0]);
                    var gflops = (2.0 * size * size * size) / (comp.CurrentTime / 1000.0 * 1e9);
                    
                    var deltaSign = comp.TimeDelta >= 0 ? "+" : "";
                    var percentSign = comp.TimeChangePercent >= 0 ? "+" : "";
                    
                    sb.AppendLine($"| `{comp.Method}` | {comp.PreviousTime:F2} | {comp.CurrentTime:F2} | " +
                        $"{deltaSign}{comp.TimeDelta:F2} | {percentSign}{comp.TimeChangePercent:F1}% | {gflops:F2} |");
                }
            }
            sb.AppendLine();
        }

        public static void GenerateModelOnlyComparison(
            Dictionary<string, ProfileData> current,
            string outputPath)
        {
            var sb = new StringBuilder();

            sb.AppendLine("# Model Size Performance Comparison");
            sb.AppendLine();
            sb.AppendLine($"**Generated:** {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();

            // Extract model data
            var smallInference = current.GetValueOrDefault("Model_Small_Inference");
            var mediumInference = current.GetValueOrDefault("Model_Medium_Inference");
            var smallForward = current.GetValueOrDefault("Model_Small_Forward");
            var mediumForward = current.GetValueOrDefault("Model_Medium_Forward");

            if (smallInference != null && mediumInference != null)
            {
                sb.AppendLine("## 📊 Model Specifications");
                sb.AppendLine();
                sb.AppendLine("| Model | Dimensions | Layers | Heads | Parameters | Context |");
                sb.AppendLine("|-------|-----------|--------|-------|------------|---------|");
                sb.AppendLine("| **Small** | 128 | 2 | 4 | 470,528 | 64 |");
                sb.AppendLine("| **Medium** | 256 | 4 | 8 | 3,454,464 | 128 |");
                sb.AppendLine();

                sb.AppendLine("## ⏱️ Performance Metrics");
                sb.AppendLine();
                sb.AppendLine("| Metric | Small Model | Medium Model | Ratio (Med/Small) |");
                sb.AppendLine("|--------|-------------|--------------|-------------------|");
                
                var timeRatio = mediumInference.TimeMs / smallInference.TimeMs;
                var allocRatio = mediumInference.AllocMB / smallInference.AllocMB;
                var paramRatio = 3454464.0 / 470528.0;

                sb.AppendLine($"| **Total Inference Time** | {smallInference.TimeMs:F2} ms | {mediumInference.TimeMs:F2} ms | {timeRatio:F2}x |");
                sb.AppendLine($"| **Avg Time per Token** | {smallInference.AvgMs / 25.0:F2} ms | {mediumInference.AvgMs / 25.0:F2} ms | {(mediumInference.AvgMs / smallInference.AvgMs):F2}x |");
                sb.AppendLine($"| **Tokens per Second** | {25000.0 / smallInference.TimeMs:F2} | {25000.0 / mediumInference.TimeMs:F2} | {(smallInference.TimeMs / mediumInference.TimeMs):F2}x |");
                sb.AppendLine($"| **Memory Allocated** | {smallInference.AllocMB:F2} MB | {mediumInference.AllocMB:F2} MB | {allocRatio:F2}x |");
                sb.AppendLine($"| **Memory per Token** | {smallInference.AllocMB / 25.0:F2} MB | {mediumInference.AllocMB / 25.0:F2} MB | {allocRatio:F2}x |");
                sb.AppendLine();

                sb.AppendLine("## 📈 Scaling Efficiency Analysis");
                sb.AppendLine();
                sb.AppendLine($"- **Parameter Scaling:** Medium model has **{paramRatio:F2}x** more parameters");
                sb.AppendLine($"- **Time Scaling:** Medium model takes **{timeRatio:F2}x** longer");
                sb.AppendLine($"- **Memory Scaling:** Medium model uses **{allocRatio:F2}x** more memory");
                sb.AppendLine();
                
                var computeEfficiency = paramRatio / timeRatio;
                sb.AppendLine($"### Computational Efficiency: **{computeEfficiency:F2}x**");
                sb.AppendLine();
                if (computeEfficiency > 0.9)
                    sb.AppendLine("✅ **Excellent** - Nearly linear scaling with parameter count");
                else if (computeEfficiency > 0.7)
                    sb.AppendLine("✅ **Good** - Reasonable scaling efficiency");
                else if (computeEfficiency > 0.5)
                    sb.AppendLine("⚠️ **Fair** - Some optimization opportunities exist");
                else
                    sb.AppendLine("❌ **Poor** - Significant optimization needed");
                sb.AppendLine();

                sb.AppendLine("*Ideal efficiency is 1.0x, meaning time scales linearly with parameters.*");
                sb.AppendLine();

                // Forward pass breakdown
                if (smallForward != null && mediumForward != null)
                {
                    sb.AppendLine("## 🔬 Forward Pass Analysis");
                    sb.AppendLine();
                    sb.AppendLine("| Model | Avg Forward Time | Calls | Total Forward Time |");
                    sb.AppendLine("|-------|------------------|-------|--------------------|");
                    sb.AppendLine($"| **Small** | {smallForward.AvgMs:F2} ms | {smallForward.Calls} | {smallForward.TimeMs:F2} ms |");
                    sb.AppendLine($"| **Medium** | {mediumForward.AvgMs:F2} ms | {mediumForward.Calls} | {mediumForward.TimeMs:F2} ms |");
                    sb.AppendLine();
                }
            }

            File.WriteAllText(outputPath, sb.ToString());
            Console.WriteLine($"✓ Model comparison saved to: {outputPath}");
        }
    }
}
