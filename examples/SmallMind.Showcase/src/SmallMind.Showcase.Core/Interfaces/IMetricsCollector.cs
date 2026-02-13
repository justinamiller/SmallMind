using SmallMind.Showcase.Core.Models;

namespace SmallMind.Showcase.Core.Interfaces;

/// <summary>
/// Collects and aggregates generation metrics.
/// </summary>
public interface IMetricsCollector
{
    /// <summary>
    /// Gets the current real-time metrics.
    /// </summary>
    GenerationMetrics GetCurrentMetrics();

    /// <summary>
    /// Gets rolling percentile statistics.
    /// </summary>
    MetricsPercentiles GetPercentiles();

    /// <summary>
    /// Notifies the start of a generation request.
    /// </summary>
    void OnRequestStart(int promptTokens, int contextWindowSize);

    /// <summary>
    /// Notifies when the first token is generated.
    /// </summary>
    void OnFirstToken();

    /// <summary>
    /// Notifies when a token is generated (during decode phase).
    /// </summary>
    void OnTokenGenerated();

    /// <summary>
    /// Notifies when the request completes.
    /// </summary>
    void OnRequestComplete(int generatedTokens);

    /// <summary>
    /// Records an error.
    /// </summary>
    void RecordError(string error);

    /// <summary>
    /// Records a warning.
    /// </summary>
    void RecordWarning(string warning);

    /// <summary>
    /// Resets current request metrics.
    /// </summary>
    void Reset();
}
