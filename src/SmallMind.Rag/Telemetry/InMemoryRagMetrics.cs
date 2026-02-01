using System.Text;

namespace SmallMind.Rag.Telemetry;

/// <summary>
/// In-memory implementation of <see cref="IRagMetrics"/> for tracking RAG operation metrics.
/// </summary>
public sealed class InMemoryRagMetrics : IRagMetrics
{
    private long _ingestionCount;
    private long _retrievalCount;
    private readonly Dictionary<string, long> _errorCounts;
    private readonly List<TimeSpan> _ingestionDurations;
    private readonly List<TimeSpan> _retrievalDurations;
    private long _totalChunks;
    private long _totalDocs;
    private readonly object _lock;

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryRagMetrics"/> class.
    /// </summary>
    public InMemoryRagMetrics()
    {
        _errorCounts = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        _ingestionDurations = new List<TimeSpan>();
        _retrievalDurations = new List<TimeSpan>();
        _lock = new object();
    }

    /// <summary>
    /// Increments the ingestion operation counter.
    /// </summary>
    /// <param name="delta">The amount to increment by.</param>
    public void IncrementIngestionCount(int delta = 1)
    {
        Interlocked.Add(ref _ingestionCount, delta);
    }

    /// <summary>
    /// Increments the retrieval operation counter.
    /// </summary>
    /// <param name="delta">The amount to increment by.</param>
    public void IncrementRetrievalCount(int delta = 1)
    {
        Interlocked.Add(ref _retrievalCount, delta);
    }

    /// <summary>
    /// Increments the error counter for a specific error type.
    /// </summary>
    /// <param name="errorType">The type of error.</param>
    /// <param name="delta">The amount to increment by.</param>
    public void IncrementErrorCount(string errorType, int delta = 1)
    {
        lock (_lock)
        {
            if (_errorCounts.TryGetValue(errorType, out long count))
            {
                _errorCounts[errorType] = count + delta;
            }
            else
            {
                _errorCounts[errorType] = delta;
            }
        }
    }

    /// <summary>
    /// Records the duration of an ingestion operation.
    /// </summary>
    /// <param name="duration">The operation duration.</param>
    public void RecordIngestionDuration(TimeSpan duration)
    {
        lock (_lock)
        {
            _ingestionDurations.Add(duration);
        }
    }

    /// <summary>
    /// Records the duration of a retrieval operation.
    /// </summary>
    /// <param name="duration">The operation duration.</param>
    public void RecordRetrievalDuration(TimeSpan duration)
    {
        lock (_lock)
        {
            _retrievalDurations.Add(duration);
        }
    }

    /// <summary>
    /// Records the number of chunks processed.
    /// </summary>
    /// <param name="count">The chunk count.</param>
    public void RecordChunkCount(int count)
    {
        Interlocked.Add(ref _totalChunks, count);
    }

    /// <summary>
    /// Records the number of documents processed.
    /// </summary>
    /// <param name="count">The document count.</param>
    public void RecordDocumentCount(int count)
    {
        Interlocked.Add(ref _totalDocs, count);
    }

    /// <summary>
    /// Resets all metrics to zero.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _ingestionCount = 0;
            _retrievalCount = 0;
            _totalChunks = 0;
            _totalDocs = 0;
            _errorCounts.Clear();
            _ingestionDurations.Clear();
            _retrievalDurations.Clear();
        }
    }

    /// <summary>
    /// Gets a formatted summary of all collected metrics.
    /// </summary>
    /// <returns>A string containing metric statistics.</returns>
    public string GetSummary()
    {
        lock (_lock)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== RAG Metrics Summary ===");
            sb.AppendLine($"Ingestions: {_ingestionCount}");
            sb.AppendLine($"Retrievals: {_retrievalCount}");
            sb.AppendLine($"Total Documents: {_totalDocs}");
            sb.AppendLine($"Total Chunks: {_totalChunks}");

            if (_ingestionDurations.Count > 0)
            {
                double avgIngestionMs = CalculateAverage(_ingestionDurations);
                sb.AppendLine($"Avg Ingestion Duration: {avgIngestionMs:F2}ms");
            }

            if (_retrievalDurations.Count > 0)
            {
                double avgRetrievalMs = CalculateAverage(_retrievalDurations);
                sb.AppendLine($"Avg Retrieval Duration: {avgRetrievalMs:F2}ms");
            }

            if (_errorCounts.Count > 0)
            {
                sb.AppendLine("Errors:");
                foreach (var kvp in _errorCounts)
                {
                    sb.AppendLine($"  {kvp.Key}: {kvp.Value}");
                }
            }

            return sb.ToString();
        }
    }

    private static double CalculateAverage(List<TimeSpan> durations)
    {
        double total = 0;
        for (int i = 0; i < durations.Count; i++)
        {
            total += durations[i].TotalMilliseconds;
        }
        return total / durations.Count;
    }
}
