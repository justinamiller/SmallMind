using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SmallMind.Benchmarks;

/// <summary>
/// Environment metadata for benchmark reports.
/// </summary>
public sealed class EnvironmentMetadata
{
    public string OsDescription { get; set; } = string.Empty;
    public string OsVersion { get; set; } = string.Empty;
    public string ProcessArchitecture { get; set; } = string.Empty;
    public int ProcessorCount { get; set; }
    public string DotNetVersion { get; set; } = string.Empty;
    public string RuntimeDescription { get; set; } = string.Empty;
    public string BuildConfiguration { get; set; } = string.Empty;
    public string RepoCommitHash { get; set; } = string.Empty;
    public string EngineVersion { get; set; } = string.Empty;
    public int EngineThreads { get; set; }
    public int EngineContextLength { get; set; }
    public string EngineQuantization { get; set; } = string.Empty;
    public string EngineTokenizer { get; set; } = string.Empty;
    
    public static EnvironmentMetadata Collect(BenchmarkConfig config)
    {
        var metadata = new EnvironmentMetadata
        {
            OsDescription = RuntimeInformation.OSDescription,
            OsVersion = Environment.OSVersion.ToString(),
            ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
            ProcessorCount = Environment.ProcessorCount,
            DotNetVersion = Environment.Version.ToString(),
            RuntimeDescription = RuntimeInformation.FrameworkDescription,
            EngineThreads = config.Threads > 0 ? config.Threads : Environment.ProcessorCount
        };
        
        // Build configuration
        #if DEBUG
        metadata.BuildConfiguration = "Debug";
        #else
        metadata.BuildConfiguration = "Release";
        #endif
        
        // Try to get git commit hash
        metadata.RepoCommitHash = GetGitCommitHash();
        
        return metadata;
    }
    
    private static string GetGitCommitHash()
    {
        // Try environment variable first (CI environments)
        string? envHash = Environment.GetEnvironmentVariable("GITHUB_SHA");
        if (!string.IsNullOrEmpty(envHash))
        {
            return envHash;
        }
        
        // Try to read from .git/HEAD
        try
        {
            string gitHeadPath = Path.Combine(Directory.GetCurrentDirectory(), ".git", "HEAD");
            if (File.Exists(gitHeadPath))
            {
                string headContent = File.ReadAllText(gitHeadPath).Trim();
                if (headContent.StartsWith("ref: "))
                {
                    // It's a reference, read the actual commit
                    string refPath = headContent.Substring(5);
                    string commitPath = Path.Combine(Directory.GetCurrentDirectory(), ".git", refPath);
                    if (File.Exists(commitPath))
                    {
                        return File.ReadAllText(commitPath).Trim();
                    }
                }
                else
                {
                    // It's a commit hash
                    return headContent;
                }
            }
        }
        catch
        {
            // Best effort
        }
        
        return "unknown";
    }
}
