namespace SmallMind.Showcase.Core.Models;

/// <summary>
/// Information about a discoverable local model file.
/// </summary>
public sealed class DiscoveredModel
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Path { get; init; }
    public long SizeBytes { get; init; }
    public string? Architecture { get; init; }
    public string? Quantization { get; init; }
    public string? Description { get; init; }
    public DateTime LastModified { get; init; }

    public string SizeFormatted => FormatBytes(SizeBytes);

    private static string FormatBytes(long bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int suffixIndex = 0;
        double size = bytes;

        while (size >= 1024 && suffixIndex < suffixes.Length - 1)
        {
            size /= 1024;
            suffixIndex++;
        }

        return $"{size:F2} {suffixes[suffixIndex]}";
    }
}
