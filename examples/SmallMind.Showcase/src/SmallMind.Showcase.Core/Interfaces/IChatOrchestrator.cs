using SmallMind.Showcase.Core.Models;

namespace SmallMind.Showcase.Core.Interfaces;

/// <summary>
/// Orchestrates chat interactions with the SmallMind engine.
/// </summary>
public interface IChatOrchestrator
{
    /// <summary>
    /// Loads a model and initializes the engine.
    /// </summary>
    Task LoadModelAsync(DiscoveredModel model, CancellationToken cancellationToken = default);

    /// <summary>
    /// Unloads the current model.
    /// </summary>
    Task UnloadModelAsync();

    /// <summary>
    /// Gets the currently loaded model.
    /// </summary>
    DiscoveredModel? CurrentModel { get; }

    /// <summary>
    /// Sends a chat message and streams the response.
    /// </summary>
    IAsyncEnumerable<string> SendMessageAsync(
        ChatSession session,
        string userMessage,
        GenerationConfig config,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets current generation metrics.
    /// </summary>
    GenerationMetrics GetCurrentMetrics();

    /// <summary>
    /// Checks if the engine is currently generating.
    /// </summary>
    bool IsGenerating { get; }
}
