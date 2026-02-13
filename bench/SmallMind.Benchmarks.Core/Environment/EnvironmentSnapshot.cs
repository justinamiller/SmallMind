using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SmallMind.Benchmarks.Core.Environment;

/// <summary>
/// Represents a snapshot of the system environment at the time of benchmark execution.
/// </summary>
public sealed class EnvironmentSnapshot
{
    /// <summary>
    /// Operating system description (e.g., "Linux 5.15.0-1039-azure #46-Ubuntu").
    /// </summary>
    [JsonPropertyName("os")]
    public string OperatingSystem { get; set; } = string.Empty;

    /// <summary>
    /// .NET runtime version (e.g., ".NET 8.0.1").
    /// </summary>
    [JsonPropertyName("runtime")]
    public string RuntimeVersion { get; set; } = string.Empty;

    /// <summary>
    /// CPU model name (e.g., "Intel(R) Core(TM) i7-9700K CPU @ 3.60GHz").
    /// May be "unknown" if unavailable.
    /// </summary>
    [JsonPropertyName("cpu_model")]
    public string CpuModel { get; set; } = string.Empty;

    /// <summary>
    /// Number of logical CPU cores available.
    /// </summary>
    [JsonPropertyName("cpu_cores")]
    public int LogicalCores { get; set; }

    /// <summary>
    /// CPU base frequency in MHz. Null if unavailable.
    /// </summary>
    [JsonPropertyName("cpu_base_freq_mhz")]
    public double? CpuBaseFrequencyMhz { get; set; }

    /// <summary>
    /// CPU maximum frequency in MHz. Null if unavailable.
    /// </summary>
    [JsonPropertyName("cpu_max_freq_mhz")]
    public double? CpuMaxFrequencyMhz { get; set; }

    /// <summary>
    /// SIMD instruction sets available on this CPU.
    /// </summary>
    [JsonPropertyName("simd_capabilities")]
    public List<string> SimdCapabilities { get; set; } = new();

    /// <summary>
    /// Whether the runtime is using Server GC mode.
    /// </summary>
    [JsonPropertyName("server_gc")]
    public bool IsServerGC { get; set; }

    /// <summary>
    /// Current GC latency mode.
    /// </summary>
    [JsonPropertyName("gc_latency_mode")]
    public string GCLatencyMode { get; set; } = string.Empty;

    /// <summary>
    /// Git commit SHA of the codebase. Null if not in a git repository.
    /// </summary>
    [JsonPropertyName("git_commit")]
    public string? GitCommitSha { get; set; }

    /// <summary>
    /// Timestamp when this snapshot was captured.
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>
    /// Serializes this snapshot to a JSON string.
    /// </summary>
    /// <param name="indented">Whether to use indented formatting.</param>
    /// <returns>JSON representation of the snapshot.</returns>
    public string ToJson(bool indented = true)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = indented,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never
        };
        return JsonSerializer.Serialize(this, options);
    }

    /// <summary>
    /// Deserializes an environment snapshot from JSON.
    /// </summary>
    /// <param name="json">JSON string to deserialize.</param>
    /// <returns>Deserialized environment snapshot.</returns>
    public static EnvironmentSnapshot FromJson(string json)
    {
        return JsonSerializer.Deserialize<EnvironmentSnapshot>(json) 
               ?? throw new InvalidOperationException("Failed to deserialize EnvironmentSnapshot");
    }

    /// <summary>
    /// Returns a human-readable summary of the environment.
    /// </summary>
    public override string ToString()
    {
        return $"OS: {OperatingSystem}, Runtime: {RuntimeVersion}, CPU: {CpuModel} ({LogicalCores} cores)";
    }
}
