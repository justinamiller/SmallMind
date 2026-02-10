using System.Runtime.CompilerServices;
using SmallMind;
using SmallMind.Abstractions;
using SmallMind.Showcase.Core.Interfaces;
using SmallMind.Showcase.Core.Models;

namespace SmallMind.Showcase.Core.Services;

/// <summary>
/// Orchestrates chat interactions with SmallMind engine.
/// </summary>
public sealed class ChatOrchestrator : IChatOrchestrator, IDisposable
{
    private readonly IMetricsCollector _metricsCollector;
    private ISmallMindEngine? _engine;
    private DiscoveredModel? _currentModel;
    private bool _isGenerating;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public ChatOrchestrator(IMetricsCollector metricsCollector)
    {
        _metricsCollector = metricsCollector ?? throw new ArgumentNullException(nameof(metricsCollector));
    }

    public DiscoveredModel? CurrentModel => _currentModel;
    public bool IsGenerating => _isGenerating;

    public async Task LoadModelAsync(DiscoveredModel model, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            // Unload existing model if any
            if (_engine != null)
            {
                _engine.Dispose();
                _engine = null;
                _currentModel = null;
            }

            // Create SmallMind engine options
            var options = new SmallMindOptions
            {
                ModelPath = model.Path,
                EnableKvCache = true,
                AllowGgufImport = model.Path.EndsWith(".gguf", StringComparison.OrdinalIgnoreCase),
                MaxContextTokens = 2048
            };

            // Create engine
            _engine = await Task.Run(() => SmallMindFactory.Create(options), cancellationToken);
            _currentModel = model;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task UnloadModelAsync()
    {
        await _lock.WaitAsync();
        try
        {
            if (_engine != null)
            {
                _engine.Dispose();
                _engine = null;
                _currentModel = null;
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async IAsyncEnumerable<string> SendMessageAsync(
        ChatSession session,
        string userMessage,
        GenerationConfig config,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (_engine == null)
        {
            throw new InvalidOperationException("No model loaded. Please load a model first.");
        }

        if (_isGenerating)
        {
            throw new InvalidOperationException("A generation is already in progress.");
        }

        _isGenerating = true;
        _metricsCollector.Reset();

        // Build messages for SmallMind - use simple prompt formatting for now
        var promptBuilder = new System.Text.StringBuilder();

        // Add system messages
        foreach (var msg in session.Messages.Where(m => m.Role == ChatMessageRole.System))
        {
            promptBuilder.AppendLine($"System: {msg.Content}");
        }

        // Add conversation history
        foreach (var msg in session.Messages.Where(m => m.Role != ChatMessageRole.System))
        {
            var role = msg.Role == ChatMessageRole.User ? "User" : "Assistant";
            promptBuilder.AppendLine($"{role}: {msg.Content}");
        }

        // Add current user message
        promptBuilder.AppendLine($"User: {userMessage}");
        promptBuilder.Append("Assistant:");

        var prompt = promptBuilder.ToString();

        // Estimate prompt tokens (rough estimate based on characters)
        int estimatedPromptTokens = prompt.Length / 4;
        _metricsCollector.OnRequestStart(estimatedPromptTokens, 2048);

        // Create generation options
        var genOptions = new TextGenerationOptions
        {
            MaxOutputTokens = config.MaxTokens,
            Temperature = config.Temperature,
            TopP = config.TopP,
            TopK = config.TopK,
            StopSequences = config.StopSequences
        };

        // Use the text generation session for streaming
        var textSession = _engine.CreateTextGenerationSession(genOptions);

        bool firstToken = true;
        int tokenCount = 0;
        var generatedText = new System.Text.StringBuilder();

        // Stream tokens - using a helper method to avoid try/catch in async iterator
        await foreach (var result in GenerateStreamingInternalAsync(
            textSession, 
            prompt, 
            config.Seed, 
            cancellationToken))
        {
            if (firstToken)
            {
                _metricsCollector.OnFirstToken();
                firstToken = false;
            }

            _metricsCollector.OnTokenGenerated();
            tokenCount++;
            generatedText.Append(result);

            yield return result;
        }

        _metricsCollector.OnRequestComplete(tokenCount);
        _isGenerating = false;
    }

    private async IAsyncEnumerable<string> GenerateStreamingInternalAsync(
        ITextGenerationSession session,
        string prompt,
        int? seed,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var request = new TextGenerationRequest
        {
            Prompt = prompt.AsMemory(),
            Seed = seed
        };

        await foreach (var tokenResult in session.GenerateStreaming(request, cancellationToken))
        {
            yield return tokenResult.TokenText;
        }
    }

    public GenerationMetrics GetCurrentMetrics()
    {
        return _metricsCollector.GetCurrentMetrics();
    }

    public void Dispose()
    {
        _engine?.Dispose();
        _lock.Dispose();
    }
}
