using SmallMind.Abstractions;
using System.Diagnostics;

namespace SmallMind.Benchmarks;

/// <summary>
/// Adapter for SmallMind engine that enables precise measurement.
/// </summary>
public sealed class EngineAdapter : IDisposable
{
    private readonly ISmallMindEngine _engine;
    private IModelHandle? _model;
    private bool _disposed;
    
    public EngineAdapter()
    {
        _engine = Engine.SmallMind.Create(new SmallMindOptions
        {
            EnableKvCache = false, // Disable for benchmarking consistency
            EnableRag = false
        });
    }
    
    public async Task<IModelHandle> LoadModelAsync(string modelPath, int threads, CancellationToken ct)
    {
        var request = new ModelLoadRequest
        {
            Path = modelPath,
            AllowGgufImport = true,
            Threads = threads
        };
        
        _model = await _engine.LoadModelAsync(request, ct);
        return _model;
    }
    
    /// <summary>
    /// Generate tokens with TTFT measurement capability.
    /// </summary>
    public async Task<GenerationMeasurement> GenerateAsync(
        string prompt,
        int maxNewTokens,
        double temperature,
        int topK,
        double topP,
        uint seed,
        CancellationToken ct)
    {
        if (_model == null)
            throw new InvalidOperationException("Model not loaded");
            
        var request = new GenerationRequest
        {
            Prompt = prompt,
            Options = new GenerationOptions
            {
                MaxNewTokens = maxNewTokens,
                Temperature = temperature,
                TopK = topK,
                TopP = topP,
                Mode = GenerationMode.Deterministic,
                Seed = seed
            }
        };
        
        var measurement = new GenerationMeasurement();
        measurement.StartTimestamp = TimingUtils.GetTimestamp();
        
        int tokenCount = 0;
        var textBuilder = new System.Text.StringBuilder();
        
        await foreach (var tokenEvent in _engine.GenerateStreamingAsync(_model, request, ct))
        {
            if (tokenEvent.Kind == TokenEventKind.Token)
            {
                tokenCount++;
                
                // Record TTFT (first token)
                if (tokenCount == 1)
                {
                    measurement.FirstTokenTimestamp = TimingUtils.GetTimestamp();
                }
                
                // Accumulate text
                textBuilder.Append(tokenEvent.Text);
            }
            else if (tokenEvent.Kind == TokenEventKind.Error)
            {
                throw new Exception($"Generation error: {tokenEvent.Error}");
            }
            else if (tokenEvent.Kind == TokenEventKind.Completed)
            {
                break;
            }
        }
        
        measurement.EndTimestamp = TimingUtils.GetTimestamp();
        measurement.TokenCount = tokenCount;
        measurement.GeneratedText = textBuilder.ToString();
        
        return measurement;
    }
    
    public void Dispose()
    {
        if (_disposed)
            return;
            
        _model?.Dispose();
        _engine.Dispose();
        _disposed = true;
    }
}

/// <summary>
/// Measurement result for a single generation.
/// </summary>
public sealed class GenerationMeasurement
{
    public long StartTimestamp { get; set; }
    public long FirstTokenTimestamp { get; set; }
    public long EndTimestamp { get; set; }
    public int TokenCount { get; set; }
    public string GeneratedText { get; set; } = string.Empty;
    
    /// <summary>
    /// Time to first token in milliseconds.
    /// </summary>
    public double TtftMs
    {
        get
        {
            if (FirstTokenTimestamp == 0)
                return 0;
            return TimingUtils.TicksToMilliseconds(FirstTokenTimestamp - StartTimestamp);
        }
    }
    
    /// <summary>
    /// End-to-end latency in milliseconds.
    /// </summary>
    public double LatencyMs
    {
        get
        {
            return TimingUtils.TicksToMilliseconds(EndTimestamp - StartTimestamp);
        }
    }
    
    /// <summary>
    /// Overall tokens per second (including TTFT).
    /// </summary>
    public double OverallTokensPerSec
    {
        get
        {
            double seconds = TimingUtils.TicksToSecondsDouble(EndTimestamp - StartTimestamp);
            if (seconds <= 0)
                return 0;
            return TokenCount / seconds;
        }
    }
    
    /// <summary>
    /// Steady-state tokens per second (after first token).
    /// </summary>
    public double SteadyTokensPerSec
    {
        get
        {
            if (FirstTokenTimestamp == 0 || TokenCount <= 1)
                return 0;
                
            double seconds = TimingUtils.TicksToSecondsDouble(EndTimestamp - FirstTokenTimestamp);
            if (seconds <= 0)
                return 0;
                
            return (TokenCount - 1) / seconds;
        }
    }
}
