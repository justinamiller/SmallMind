using SmallMind.Showcase.Core.Models;

namespace SmallMind.Showcase.Core.Interfaces;

/// <summary>
/// Discovers and manages local model files.
/// </summary>
public interface IModelRegistry
{
    /// <summary>
    /// Discovers all models in the configured folder(s).
    /// </summary>
    Task<List<DiscoveredModel>> DiscoverModelsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific model by ID.
    /// </summary>
    Task<DiscoveredModel?> GetModelAsync(string modelId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Refreshes the model registry.
    /// </summary>
    Task RefreshAsync(CancellationToken cancellationToken = default);
}
