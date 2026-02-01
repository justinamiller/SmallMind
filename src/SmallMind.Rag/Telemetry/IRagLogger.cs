namespace SmallMind.Rag.Telemetry;

/// <summary>
/// Defines the contract for logging RAG operations.
/// </summary>
public interface IRagLogger
{
    /// <summary>
    /// Logs the completion of a document ingestion operation.
    /// </summary>
    /// <param name="traceId">The trace identifier for correlation.</param>
    /// <param name="docCount">The number of documents ingested.</param>
    /// <param name="chunkCount">The number of chunks created.</param>
    /// <param name="duration">The duration of the operation.</param>
    void LogIngestion(string traceId, int docCount, int chunkCount, TimeSpan duration);

    /// <summary>
    /// Logs a retrieval operation.
    /// </summary>
    /// <param name="traceId">The trace identifier for correlation.</param>
    /// <param name="query">The query text.</param>
    /// <param name="resultCount">The number of results returned.</param>
    /// <param name="duration">The duration of the operation.</param>
    void LogRetrieval(string traceId, string query, int resultCount, TimeSpan duration);

    /// <summary>
    /// Logs an error that occurred during an operation.
    /// </summary>
    /// <param name="traceId">The trace identifier for correlation.</param>
    /// <param name="operation">The operation that failed.</param>
    /// <param name="ex">The exception that occurred.</param>
    void LogError(string traceId, string operation, Exception ex);

    /// <summary>
    /// Logs a warning message.
    /// </summary>
    /// <param name="traceId">The trace identifier for correlation.</param>
    /// <param name="message">The warning message.</param>
    void LogWarning(string traceId, string message);

    /// <summary>
    /// Logs an informational message.
    /// </summary>
    /// <param name="traceId">The trace identifier for correlation.</param>
    /// <param name="message">The informational message.</param>
    void LogInfo(string traceId, string message);
}
