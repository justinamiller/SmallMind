using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace SmallMind.Core.Core
{
    /// <summary>
    /// Performance profiler for tracking operation timings during training
    /// </summary>
    internal sealed class TrainingProfiler
    {
        private readonly ConcurrentDictionary<string, OperationStats> _stats = new();
        
        internal struct OperationStats
        {
            public long TotalTicks;
            public long Count;
            public long MinTicks;
            public long MaxTicks;
            public long TotalBytes;  // Memory allocated/processed
            
            public double TotalMs => TotalTicks * 1000.0 / Stopwatch.Frequency;
            public double AvgMs => Count > 0 ? TotalMs / Count : 0;
            public double MinMs => MinTicks * 1000.0 / Stopwatch.Frequency;
            public double MaxMs => MaxTicks * 1000.0 / Stopwatch.Frequency;
        }
        
        public ProfileScope Profile(string operation, long bytes = 0)
        {
            return new ProfileScope(this, operation, bytes);
        }
        
        public void RecordOperation(string operation, long elapsedTicks, long bytes)
        {
            _stats.AddOrUpdate(operation,
                _ => new OperationStats 
                { 
                    TotalTicks = elapsedTicks, 
                    Count = 1, 
                    MinTicks = elapsedTicks, 
                    MaxTicks = elapsedTicks,
                    TotalBytes = bytes
                },
                (_, existing) => new OperationStats
                {
                    TotalTicks = existing.TotalTicks + elapsedTicks,
                    Count = existing.Count + 1,
                    MinTicks = Math.Min(existing.MinTicks, elapsedTicks),
                    MaxTicks = Math.Max(existing.MaxTicks, elapsedTicks),
                    TotalBytes = existing.TotalBytes + bytes
                });
        }
        
        internal readonly struct ProfileScope : IDisposable
        {
            private readonly TrainingProfiler? _profiler;
            private readonly string _operation;
            private readonly long _startTicks;
            private readonly long _bytes;
            
            public ProfileScope(TrainingProfiler profiler, string operation, long bytes)
            {
                _profiler = profiler;
                _operation = operation;
                _bytes = bytes;
                _startTicks = Stopwatch.GetTimestamp();
            }
            
            public void Dispose()
            {
                if (_profiler == null) return;
                
                long elapsed = Stopwatch.GetTimestamp() - _startTicks;
                _profiler.RecordOperation(_operation, elapsed, _bytes);
            }
        }
        
        public void PrintReport()
        {
            Console.WriteLine("\n╔══════════════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                         TRAINING PERFORMANCE REPORT                       ║");
            Console.WriteLine("╠══════════════════════════════════════════════════════════════════════════╣");
            Console.WriteLine("║ Operation                    │ Total (ms) │ Avg (ms) │ Count  │ % Time  ║");
            Console.WriteLine("╟──────────────────────────────┼────────────┼──────────┼────────┼─────────╢");
            
            // Sort by total ticks descending
            var statsList = new List<KeyValuePair<string, OperationStats>>(_stats.Count);
            foreach (var kvp in _stats)
            {
                statsList.Add(kvp);
            }
            
            // Bubble sort (simple and allocation-free for small lists)
            for (int i = 0; i < statsList.Count - 1; i++)
            {
                for (int j = 0; j < statsList.Count - i - 1; j++)
                {
                    if (statsList[j].Value.TotalTicks < statsList[j + 1].Value.TotalTicks)
                    {
                        var temp = statsList[j];
                        statsList[j] = statsList[j + 1];
                        statsList[j + 1] = temp;
                    }
                }
            }
            
            double totalTime = 0;
            for (int i = 0; i < statsList.Count; i++)
            {
                totalTime += statsList[i].Value.TotalMs;
            }
            
            for (int i = 0; i < statsList.Count; i++)
            {
                var op = statsList[i].Key;
                var stats = statsList[i].Value;
                double pct = totalTime > 0 ? (stats.TotalMs / totalTime * 100) : 0;
                Console.WriteLine($"║ {op,-28} │ {stats.TotalMs,10:F2} │ {stats.AvgMs,8:F3} │ {stats.Count,6} │ {pct,6:F1}% ║");
            }
            
            Console.WriteLine("╠══════════════════════════════════════════════════════════════════════════╣");
            Console.WriteLine($"║ TOTAL                        │ {totalTime,10:F2} │          │        │ 100.0% ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════════════════════════╝");
        }
        
        public void Clear()
        {
            _stats.Clear();
        }
    }
    
    /// <summary>
    /// Memory usage tracker for monitoring training memory consumption
    /// </summary>
    internal sealed class MemoryTracker
    {
        private long _peakManaged;
        private long _peakWorking;
        private readonly List<(string phase, long managed, long working)> _snapshots = new();
        
        public void Snapshot(string phase)
        {
            long managed = GC.GetTotalMemory(forceFullCollection: false);
            long working;
            using (var proc = Process.GetCurrentProcess())
                working = proc.WorkingSet64;
            
            _peakManaged = Math.Max(_peakManaged, managed);
            _peakWorking = Math.Max(_peakWorking, working);
            
            _snapshots.Add((phase, managed, working));
        }
        
        public void PrintReport()
        {
            Console.WriteLine("\n╔══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                    MEMORY USAGE REPORT                        ║");
            Console.WriteLine("╠══════════════════════════════════════════════════════════════╣");
            Console.WriteLine("║ Phase                    │ Managed (MB) │ Working Set (MB)   ║");
            Console.WriteLine("╟──────────────────────────┼──────────────┼────────────────────╢");
            
            foreach (var (phase, managed, working) in _snapshots)
            {
                Console.WriteLine($"║ {phase,-24} │ {managed / 1024.0 / 1024.0,12:F1} │ {working / 1024.0 / 1024.0,18:F1} ║");
            }
            
            Console.WriteLine("╠══════════════════════════════════════════════════════════════╣");
            Console.WriteLine($"║ PEAK                     │ {_peakManaged / 1024.0 / 1024.0,12:F1} │ {_peakWorking / 1024.0 / 1024.0,18:F1} ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
            
            Console.WriteLine($"\nGC Collections - Gen0: {GC.CollectionCount(0)}, Gen1: {GC.CollectionCount(1)}, Gen2: {GC.CollectionCount(2)}");
        }
        
        public void Clear()
        {
            _snapshots.Clear();
            _peakManaged = 0;
            _peakWorking = 0;
        }
    }
    
    /// <summary>
    /// Gradient diagnostics for detecting vanishing/exploding gradients
    /// </summary>
    public static class GradientDiagnostics
    {
        public static void CheckGradients(string name, ReadOnlySpan<float> gradients, bool verbose = false)
        {
            float min = float.MaxValue;
            float max = float.MinValue;
            float sum = 0;
            float sumSq = 0;
            int zeroCount = 0;
            int nanCount = 0;
            int infCount = 0;
            
            for (int i = 0; i < gradients.Length; i++)
            {
                float g = gradients[i];
                
                if (float.IsNaN(g)) { nanCount++; continue; }
                if (float.IsInfinity(g)) { infCount++; continue; }
                if (g == 0) zeroCount++;
                
                min = Math.Min(min, g);
                max = Math.Max(max, g);
                sum += g;
                sumSq += g * g;
            }
            
            int validCount = gradients.Length - nanCount - infCount;
            float mean = validCount > 0 ? sum / validCount : 0;
            float variance = validCount > 0 ? (sumSq / validCount) - (mean * mean) : 0;
            float std = MathF.Sqrt(Math.Max(0, variance));
            float norm = MathF.Sqrt(sumSq);
            
            // Warnings
            bool hasIssue = false;
            var sb = new StringBuilder();
            
            if (nanCount > 0)
            {
                sb.AppendLine($"  ⚠️  NaN gradients: {nanCount}");
                hasIssue = true;
            }
            if (infCount > 0)
            {
                sb.AppendLine($"  ⚠️  Inf gradients: {infCount}");
                hasIssue = true;
            }
            if (norm > 100)
            {
                sb.AppendLine($"  ⚠️  Exploding gradients (norm={norm:F2})");
                hasIssue = true;
            }
            if (norm < 1e-7 && validCount > 0)
            {
                sb.AppendLine($"  ⚠️  Vanishing gradients (norm={norm:E2})");
                hasIssue = true;
            }
            if (zeroCount > validCount * 0.9 && validCount > 0)
            {
                sb.AppendLine($"  ⚠️  >90% zero gradients ({zeroCount}/{gradients.Length})");
                hasIssue = true;
            }
            
            if (hasIssue || verbose)
            {
                if (hasIssue)
                {
                    Console.WriteLine($"\n[{name}] Gradient Issues Detected:");
                    Console.Write(sb.ToString());
                }
                Console.WriteLine($"[{name}] Stats: mean={mean:E2}, std={std:E2}, min={min:E2}, max={max:E2}, norm={norm:F4}");
            }
        }
        
        public static (float norm, bool hasIssue) GetGradientNorm(ReadOnlySpan<float> gradients)
        {
            float sumSq = 0;
            int nanCount = 0;
            int infCount = 0;
            
            for (int i = 0; i < gradients.Length; i++)
            {
                float g = gradients[i];
                
                if (float.IsNaN(g)) { nanCount++; continue; }
                if (float.IsInfinity(g)) { infCount++; continue; }
                
                sumSq += g * g;
            }
            
            float norm = MathF.Sqrt(sumSq);
            bool hasIssue = nanCount > 0 || infCount > 0 || norm > 100 || (norm > 0 && norm < 1e-7);
            
            return (norm, hasIssue);
        }
    }
}
