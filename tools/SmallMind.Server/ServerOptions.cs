namespace SmallMind.Server;

public sealed class ServerOptions
{
    public string? ModelId { get; set; }
    public string? ModelPath { get; set; }
    public string? CacheDir { get; set; }
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 8080;
    public int MaxConcurrentRequests { get; set; } = 4;
    public int MaxQueueDepth { get; set; } = 32;
    public int RequestTimeoutMs { get; set; } = 300000;
    public int MaxContextTokens { get; set; } = 4096;
    public int DefaultMaxTokens { get; set; } = 100;
    public float DefaultTemperature { get; set; } = 0.8f;
    public float DefaultTopP { get; set; } = 0.95f;
    public int DefaultTopK { get; set; } = 40;
}
