namespace SmallMind.Rag.Telemetry;

/// <summary>
/// Provides thread-safe context for trace ID propagation across async operations.
/// </summary>
internal static class RagContext
{
    private static readonly AsyncLocal<string?> _traceId = new AsyncLocal<string?>();

    /// <summary>
    /// Gets or sets the current trace identifier for the async context.
    /// </summary>
    public static string? TraceId
    {
        get => _traceId.Value;
        set => _traceId.Value = value;
    }

    /// <summary>
    /// Generates a new GUID-based trace identifier.
    /// </summary>
    /// <returns>A unique trace identifier.</returns>
    public static string GenerateTraceId()
    {
        return Guid.NewGuid().ToString("N");
    }

    /// <summary>
    /// Begins a new trace scope with the specified trace ID.
    /// </summary>
    /// <param name="traceId">The trace ID to use, or null to generate a new one.</param>
    /// <returns>An <see cref="IDisposable"/> that restores the previous trace ID when disposed.</returns>
    public static IDisposable BeginScope(string? traceId = null)
    {
        return new TraceScope(traceId ?? GenerateTraceId());
    }

    private sealed class TraceScope : IDisposable
    {
        private readonly string? _previousTraceId;

        public TraceScope(string traceId)
        {
            _previousTraceId = TraceId;
            TraceId = traceId;
        }

        public void Dispose()
        {
            TraceId = _previousTraceId;
        }
    }
}
