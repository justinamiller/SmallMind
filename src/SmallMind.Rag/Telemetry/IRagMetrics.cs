namespace SmallMind.Rag.Telemetry;

/// <summary>
/// Defines the contract for recording RAG metrics.
/// </summary>
internal interface IRagMetrics
{
    /// <summary>
    /// Increments the ingestion operation counter.
    /// </summary>
    /// <param name="delta">The amount to increment by.</param>
    void IncrementIngestionCount(int delta = 1);

    /// <summary>
    /// Increments the retrieval operation counter.
    /// </summary>
    /// <param name="delta">The amount to increment by.</param>
    void IncrementRetrievalCount(int delta = 1);

    /// <summary>
    /// Increments the error counter for a specific error type.
    /// </summary>
    /// <param name="errorType">The type of error.</param>
    /// <param name="delta">The amount to increment by.</param>
    void IncrementErrorCount(string errorType, int delta = 1);

    /// <summary>
    /// Records the duration of an ingestion operation.
    /// </summary>
    /// <param name="duration">The operation duration.</param>
    void RecordIngestionDuration(TimeSpan duration);

    /// <summary>
    /// Records the duration of a retrieval operation.
    /// </summary>
    /// <param name="duration">The operation duration.</param>
    void RecordRetrievalDuration(TimeSpan duration);

    /// <summary>
    /// Records the number of chunks processed.
    /// </summary>
    /// <param name="count">The chunk count.</param>
    void RecordChunkCount(int count);

    /// <summary>
    /// Records the number of documents processed.
    /// </summary>
    /// <param name="count">The document count.</param>
    void RecordDocumentCount(int count);
}
