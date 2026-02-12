using SmallMind.Showcase.Core.Interfaces;
using SmallMind.Showcase.Core.Models;

namespace SmallMind.Showcase.Core.Services;

/// <summary>
/// Discovers local model files from configured folder paths.
/// </summary>
public sealed class ModelRegistry : IModelRegistry
{
    private readonly string _modelsPath;
    private readonly List<DiscoveredModel> _cachedModels = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    public ModelRegistry(string modelsPath)
    {
        _modelsPath = modelsPath ?? throw new ArgumentNullException(nameof(modelsPath));
    }

    public async Task<List<DiscoveredModel>> DiscoverModelsAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_cachedModels.Count > 0)
            {
                return new List<DiscoveredModel>(_cachedModels);
            }

            await RefreshInternalAsync(cancellationToken);
            return new List<DiscoveredModel>(_cachedModels);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<DiscoveredModel?> GetModelAsync(string modelId, CancellationToken cancellationToken = default)
    {
        var models = await DiscoverModelsAsync(cancellationToken);
        return models.FirstOrDefault(m => m.Id == modelId);
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            await RefreshInternalAsync(cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    private Task RefreshInternalAsync(CancellationToken cancellationToken)
    {
        _cachedModels.Clear();

        if (!Directory.Exists(_modelsPath))
        {
            return Task.CompletedTask;
        }

        // Discover .smq and .gguf files
        var patterns = new[] { "*.smq", "*.gguf" };
        var modelFiles = new List<FileInfo>();

        foreach (var pattern in patterns)
        {
            var files = Directory.GetFiles(_modelsPath, pattern, SearchOption.AllDirectories)
                .Select(f => new FileInfo(f));
            modelFiles.AddRange(files);
        }

        foreach (var file in modelFiles)
        {
            var model = new DiscoveredModel
            {
                Id = Guid.NewGuid().ToString(),
                Name = Path.GetFileNameWithoutExtension(file.Name),
                Path = file.FullName,
                SizeBytes = file.Length,
                LastModified = file.LastWriteTimeUtc,
                Architecture = InferArchitecture(file.Name),
                Quantization = InferQuantization(file.Name)
            };

            _cachedModels.Add(model);
        }

        return Task.CompletedTask;
    }

    private static string? InferArchitecture(string fileName)
    {
        var lower = fileName.ToLowerInvariant();
        if (lower.Contains("llama")) return "Llama";
        if (lower.Contains("mistral")) return "Mistral";
        if (lower.Contains("phi")) return "Phi";
        if (lower.Contains("gpt")) return "GPT";
        return null;
    }

    private static string? InferQuantization(string fileName)
    {
        var lower = fileName.ToLowerInvariant();
        if (lower.Contains("q4_0")) return "Q4_0";
        if (lower.Contains("q4_1")) return "Q4_1";
        if (lower.Contains("q5_0")) return "Q5_0";
        if (lower.Contains("q5_1")) return "Q5_1";
        if (lower.Contains("q8_0")) return "Q8_0";
        if (lower.Contains("f16")) return "F16";
        if (lower.Contains("f32")) return "F32";
        if (lower.Contains("q4")) return "Q4";
        if (lower.Contains("q5")) return "Q5";
        return null;
    }
}
