using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace CodeProfiler;

/// <summary>
/// Performance profiler that tracks method-level timings, allocations, and call hierarchies.
/// Implements hot path analysis with detailed performance data.
/// </summary>
public sealed class PerformanceProfiler : IDisposable
{
    private readonly Dictionary<string, MethodProfile> _profiles = new();
    private readonly Stack<ProfileScope> _callStack = new();
    private readonly object _lock = new();
    private readonly Stopwatch _totalTimer = new();
    private long _totalAllocatedStart;
    private bool _isEnabled;

    public PerformanceProfiler()
    {
        _isEnabled = true;
        _totalTimer.Start();
        _totalAllocatedStart = GC.GetTotalAllocatedBytes();
    }

    /// <summary>
    /// Begin profiling a method or code section.
    /// </summary>
    public ProfileScope BeginScope(string name)
    {
        if (!_isEnabled) return ProfileScope.Null;

        lock (_lock)
        {
            if (!_profiles.ContainsKey(name))
            {
                _profiles[name] = new MethodProfile { Name = name };
            }

            var scope = new ProfileScope(this, name, _callStack.Count);
            _callStack.Push(scope);
            return scope;
        }
    }

    /// <summary>
    /// End profiling scope.
    /// </summary>
    internal void EndScope(ProfileScope scope)
    {
        if (!_isEnabled || scope.IsNull) return;

        lock (_lock)
        {
            if (_callStack.Count > 0 && _callStack.Peek().Name == scope.Name)
            {
                _callStack.Pop();
            }

            if (_profiles.TryGetValue(scope.Name, out var profile))
            {
                profile.CallCount++;
                profile.TotalTicks += scope.ElapsedTicks;
                profile.TotalAllocatedBytes += scope.AllocatedBytes;

                if (scope.ElapsedTicks > profile.MaxTicks)
                {
                    profile.MaxTicks = scope.ElapsedTicks;
                }

                if (profile.MinTicks == 0 || scope.ElapsedTicks < profile.MinTicks)
                {
                    profile.MinTicks = scope.ElapsedTicks;
                }

                // Track parent-child relationships
                if (_callStack.Count > 0)
                {
                    var parent = _callStack.Peek();
                    if (!profile.CalledBy.ContainsKey(parent.Name))
                    {
                        profile.CalledBy[parent.Name] = 0;
                    }
                    profile.CalledBy[parent.Name]++;
                }
            }
        }
    }

    /// <summary>
    /// Get hot paths - methods that consume the most time.
    /// </summary>
    public List<MethodProfile> GetHotPaths(int topN = 20)
    {
        lock (_lock)
        {
            return _profiles.Values
                .OrderByDescending(p => p.TotalTicks)
                .Take(topN)
                .ToList();
        }
    }

    /// <summary>
    /// Get methods with most allocations.
    /// </summary>
    public List<MethodProfile> GetTopAllocators(int topN = 20)
    {
        lock (_lock)
        {
            return _profiles.Values
                .OrderByDescending(p => p.TotalAllocatedBytes)
                .Take(topN)
                .ToList();
        }
    }

    /// <summary>
    /// Get methods called most frequently.
    /// </summary>
    public List<MethodProfile> GetMostCalled(int topN = 20)
    {
        lock (_lock)
        {
            return _profiles.Values
                .OrderByDescending(p => p.CallCount)
                .Take(topN)
                .ToList();
        }
    }

    /// <summary>
    /// Generate a comprehensive profiling report.
    /// </summary>
    public string GenerateReport()
    {
        lock (_lock)
        {
            var sb = new StringBuilder();
            var totalMs = _totalTimer.Elapsed.TotalMilliseconds;
            var totalAllocatedMB = (GC.GetTotalAllocatedBytes() - _totalAllocatedStart) / (1024.0 * 1024.0);

            sb.AppendLine("# Performance Profile Report");
            sb.AppendLine();
            sb.AppendLine($"**Generated:** {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"**Total Runtime:** {totalMs:F2} ms");
            sb.AppendLine($"**Total Allocations:** {totalAllocatedMB:F2} MB");
            sb.AppendLine($"**Methods Profiled:** {_profiles.Count}");
            sb.AppendLine();

            // System information
            sb.AppendLine("## System Information");
            sb.AppendLine();
            sb.AppendLine($"- **OS:** {RuntimeInformation.OSDescription}");
            sb.AppendLine($"- **Architecture:** {RuntimeInformation.ProcessArchitecture}");
            sb.AppendLine($"- **CPU Cores:** {Environment.ProcessorCount}");
            sb.AppendLine($"- **.NET Version:** {Environment.Version}");
            sb.AppendLine($"- **GC Mode:** {(System.Runtime.GCSettings.IsServerGC ? "Server" : "Workstation")}");
            sb.AppendLine();

            // Hot paths by time
            sb.AppendLine("## 🔥 Hot Paths (by Time)");
            sb.AppendLine();
            sb.AppendLine("Methods consuming the most CPU time:");
            sb.AppendLine();
            sb.AppendLine("| Rank | Method | Total Time (ms) | % of Total | Calls | Avg Time (ms) | Min (ms) | Max (ms) |");
            sb.AppendLine("|------|--------|----------------|-----------|-------|---------------|----------|----------|");

            var hotPaths = GetHotPaths(20);
            for (int i = 0; i < hotPaths.Count; i++)
            {
                var profile = hotPaths[i];
                var totalMethodMs = TicksToMs(profile.TotalTicks);
                var avgMs = profile.CallCount > 0 ? totalMethodMs / profile.CallCount : 0;
                var minMs = TicksToMs(profile.MinTicks);
                var maxMs = TicksToMs(profile.MaxTicks);
                var percentOfTotal = totalMs > 0 ? (totalMethodMs / totalMs) * 100 : 0;

                sb.AppendLine($"| {i + 1} | `{profile.Name}` | {totalMethodMs:F3} | {percentOfTotal:F2}% | {profile.CallCount:N0} | {avgMs:F3} | {minMs:F3} | {maxMs:F3} |");
            }
            sb.AppendLine();

            // Top allocators
            sb.AppendLine("## 💾 Top Allocators (by Memory)");
            sb.AppendLine();
            sb.AppendLine("Methods allocating the most memory:");
            sb.AppendLine();
            sb.AppendLine("| Rank | Method | Total Alloc (MB) | % of Total | Calls | Avg Alloc (KB) |");
            sb.AppendLine("|------|--------|------------------|-----------|-------|----------------|");

            var topAllocators = GetTopAllocators(20);
            for (int i = 0; i < topAllocators.Count; i++)
            {
                var profile = topAllocators[i];
                var totalAllocMB = profile.TotalAllocatedBytes / (1024.0 * 1024.0);
                var avgAllocKB = profile.CallCount > 0 
                    ? (profile.TotalAllocatedBytes / (double)profile.CallCount) / 1024.0 
                    : 0;
                var percentOfTotal = totalAllocatedMB > 0 ? (totalAllocMB / totalAllocatedMB) * 100 : 0;

                sb.AppendLine($"| {i + 1} | `{profile.Name}` | {totalAllocMB:F3} | {percentOfTotal:F2}% | {profile.CallCount:N0} | {avgAllocKB:F3} |");
            }
            sb.AppendLine();

            // Most called methods
            sb.AppendLine("## 📞 Most Called Methods");
            sb.AppendLine();
            sb.AppendLine("Methods called most frequently:");
            sb.AppendLine();
            sb.AppendLine("| Rank | Method | Calls | Total Time (ms) | Avg Time (μs) |");
            sb.AppendLine("|------|--------|-------|----------------|---------------|");

            var mostCalled = GetMostCalled(20);
            for (int i = 0; i < mostCalled.Count; i++)
            {
                var profile = mostCalled[i];
                var totalMethodMs = TicksToMs(profile.TotalTicks);
                var avgUs = profile.CallCount > 0 ? (totalMethodMs * 1000.0) / profile.CallCount : 0;

                sb.AppendLine($"| {i + 1} | `{profile.Name}` | {profile.CallCount:N0} | {totalMethodMs:F3} | {avgUs:F3} |");
            }
            sb.AppendLine();

            // Call hierarchy for top methods
            sb.AppendLine("## 🌲 Call Hierarchy");
            sb.AppendLine();
            sb.AppendLine("Parent-child relationships for hot paths:");
            sb.AppendLine();

            foreach (var profile in hotPaths.Take(5))
            {
                sb.AppendLine($"### `{profile.Name}`");
                sb.AppendLine();
                if (profile.CalledBy.Count > 0)
                {
                    sb.AppendLine("**Called by:**");
                    foreach (var kvp in profile.CalledBy.OrderByDescending(x => x.Value))
                    {
                        sb.AppendLine($"- `{kvp.Key}` ({kvp.Value:N0} times)");
                    }
                }
                else
                {
                    sb.AppendLine("*Entry point method*");
                }
                sb.AppendLine();
            }

            // Performance insights
            sb.AppendLine("## 💡 Performance Insights");
            sb.AppendLine();

            var totalProfiledMs = hotPaths.Sum(p => TicksToMs(p.TotalTicks));
            var top5Ms = hotPaths.Take(5).Sum(p => TicksToMs(p.TotalTicks));
            
            // Note: Due to nested scopes, percentages may exceed 100%
            // We compare against the top-level entry point time instead
            var entryPointMs = hotPaths.FirstOrDefault()?.TotalTicks ?? 0;
            var top5OfEntryPoint = entryPointMs > 0 ? (top5Ms / TicksToMs(entryPointMs)) * 100 : 0;

            if (top5OfEntryPoint > 100)
            {
                // Nested scopes detected - report differently
                sb.AppendLine($"- **Top 5 methods** include nested operations (some time is counted in multiple scopes)");
            }
            else
            {
                sb.AppendLine($"- **Top 5 hot paths** account for **{top5OfEntryPoint:F1}%** of total runtime");
            }
            
            var highAllocMethods = _profiles.Values
                .Where(p => p.CallCount > 0 && (p.TotalAllocatedBytes / p.CallCount) > 1024 * 1024)
                .Count();
            
            if (highAllocMethods > 0)
            {
                sb.AppendLine($"- **{highAllocMethods} methods** allocate more than 1 MB per call on average");
            }

            var highFreqMethods = _profiles.Values.Where(p => p.CallCount > 10000).Count();
            if (highFreqMethods > 0)
            {
                sb.AppendLine($"- **{highFreqMethods} methods** called more than 10,000 times");
            }

            sb.AppendLine();

            return sb.ToString();
        }
    }

    private static double TicksToMs(long ticks)
    {
        return (ticks * 1000.0) / Stopwatch.Frequency;
    }

    public void Dispose()
    {
        _isEnabled = false;
        _totalTimer.Stop();
    }

    public readonly struct ProfileScope : IDisposable
    {
        private readonly PerformanceProfiler? _profiler;
        private readonly long _startTicks;
        private readonly long _startAllocated;
        public readonly string Name;
        public readonly bool IsNull;

        internal ProfileScope(PerformanceProfiler profiler, string name, int depth)
        {
            _profiler = profiler;
            Name = name;
            IsNull = false;
            _startTicks = Stopwatch.GetTimestamp();
            _startAllocated = GC.GetTotalAllocatedBytes();
        }

        internal long ElapsedTicks => Stopwatch.GetTimestamp() - _startTicks;
        internal long AllocatedBytes => Math.Max(0, GC.GetTotalAllocatedBytes() - _startAllocated);

        public void Dispose()
        {
            _profiler?.EndScope(this);
        }

        public static ProfileScope Null => new();
    }
}

/// <summary>
/// Profile data for a single method.
/// </summary>
public sealed class MethodProfile
{
    public string Name { get; set; } = "";
    public long CallCount { get; set; }
    public long TotalTicks { get; set; }
    public long MinTicks { get; set; }
    public long MaxTicks { get; set; }
    public long TotalAllocatedBytes { get; set; }
    public Dictionary<string, long> CalledBy { get; } = new();
}
