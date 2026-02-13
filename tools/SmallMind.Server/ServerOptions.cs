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
    
    // New production hardening limits
    /// <summary>
    /// Maximum number of tokens allowed in a single completion request.
    /// Prevents excessive resource usage. Default: 2048.
    /// </summary>
    public int MaxCompletionTokens { get; set; } = 2048;
    
    /// <summary>
    /// Maximum total prompt tokens (context) allowed in a request.
    /// Prevents memory exhaustion. Default: 8192.
    /// </summary>
    public int MaxPromptTokens { get; set; } = 8192;
    
    /// <summary>
    /// Timeout per generated token in milliseconds.
    /// Prevents hung inference. Default: 5000ms (5 seconds per token).
    /// </summary>
    public int PerTokenTimeoutMs { get; set; } = 5000;
    
    /// <summary>
    /// Whether to enforce strict validation of all limits.
    /// When true, requests exceeding limits are rejected immediately.
    /// Default: true.
    /// </summary>
    public bool StrictLimits { get; set; } = true;

    /// <summary>
    /// Maximum request body size in bytes.
    /// Prevents oversized payloads. Default: 1 MB.
    /// </summary>
    public long MaxRequestBodySizeBytes { get; set; } = 1_048_576;

    /// <summary>
    /// Whether to enable console logging for request/response details.
    /// Default: false (null logger — no console spam).
    /// </summary>
    public bool EnableConsoleLogging { get; set; }
}
