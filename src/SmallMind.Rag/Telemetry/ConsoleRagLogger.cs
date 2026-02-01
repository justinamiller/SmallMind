namespace SmallMind.Rag.Telemetry;

/// <summary>
/// Simple console-based implementation of <see cref="IRagLogger"/>.
/// </summary>
public sealed class ConsoleRagLogger : IRagLogger
{
    /// <summary>
    /// Logs the completion of a document ingestion operation.
    /// </summary>
    /// <param name="traceId">The trace identifier for correlation.</param>
    /// <param name="docCount">The number of documents ingested.</param>
    /// <param name="chunkCount">The number of chunks created.</param>
    /// <param name="duration">The duration of the operation.</param>
    public void LogIngestion(string traceId, int docCount, int chunkCount, TimeSpan duration)
    {
        Console.WriteLine($"[{traceId}] Ingestion complete: {docCount} docs, {chunkCount} chunks in {duration.TotalSeconds:F2}s");
    }

    /// <summary>
    /// Logs a retrieval operation.
    /// </summary>
    /// <param name="traceId">The trace identifier for correlation.</param>
    /// <param name="query">The query text.</param>
    /// <param name="resultCount">The number of results returned.</param>
    /// <param name="duration">The duration of the operation.</param>
    public void LogRetrieval(string traceId, string query, int resultCount, TimeSpan duration)
    {
        Console.WriteLine($"[{traceId}] Retrieval: '{query}' returned {resultCount} results in {duration.TotalMilliseconds:F2}ms");
    }

    /// <summary>
    /// Logs an error that occurred during an operation.
    /// </summary>
    /// <param name="traceId">The trace identifier for correlation.</param>
    /// <param name="operation">The operation that failed.</param>
    /// <param name="ex">The exception that occurred.</param>
    public void LogError(string traceId, string operation, Exception ex)
    {
        Console.WriteLine($"[{traceId}] ERROR: {operation} - {ex.Message}");
    }

    /// <summary>
    /// Logs a warning message.
    /// </summary>
    /// <param name="traceId">The trace identifier for correlation.</param>
    /// <param name="message">The warning message.</param>
    public void LogWarning(string traceId, string message)
    {
        Console.WriteLine($"[{traceId}] WARN: {message}");
    }

    /// <summary>
    /// Logs an informational message.
    /// </summary>
    /// <param name="traceId">The trace identifier for correlation.</param>
    /// <param name="message">The informational message.</param>
    public void LogInfo(string traceId, string message)
    {
        Console.WriteLine($"[{traceId}] INFO: {message}");
    }
}
