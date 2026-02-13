using System.Diagnostics;
using SmallMind.Abstractions;

namespace SmallMind.Engine;

/// <summary>
/// Internal helper for enforcing generation budgets consistently across all generation APIs.
/// Centralizes budget enforcement logic (MaxNewTokens, MaxContextTokens, TimeoutMs).
/// </summary>
internal sealed class BudgetEnforcer : IDisposable
{
    private readonly GenerationOptions _options;
    private readonly Stopwatch _stopwatch;
    private readonly CancellationTokenSource? _timeoutCts;
    private readonly CancellationTokenSource _combinedCts;
    private bool _disposed;

    /// <summary>
    /// Gets the combined cancellation token that includes both user cancellation and timeout.
    /// </summary>
    public CancellationToken CombinedToken => _combinedCts.Token;

    /// <summary>
    /// Gets the number of generated tokens so far.
    /// </summary>
    public int GeneratedTokens { get; private set; }

    /// <summary>
    /// Gets whether the budget has been exceeded.
    /// </summary>
    public bool BudgetExceeded { get; private set; }

    /// <summary>
    /// Gets the reason the budget was exceeded (null if not exceeded).
    /// </summary>
    public string? ExceededReason { get; private set; }

    public BudgetEnforcer(GenerationOptions options, CancellationToken userToken)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _stopwatch = Stopwatch.StartNew();

        // Validate and create timeout cancellation token if timeout is specified
        if (_options.TimeoutMs > 0)
        {
            // Validate timeout is not excessively large (prevent overflow)
            if (_options.TimeoutMs > int.MaxValue / 2)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(options.TimeoutMs),
                    _options.TimeoutMs,
                    $"TimeoutMs is too large ({_options.TimeoutMs}ms). Maximum allowed: {int.MaxValue / 2}ms.");
            }

            _timeoutCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(_options.TimeoutMs));
        }

        // Combine user token and timeout token
        if (_timeoutCts != null)
        {
            _combinedCts = CancellationTokenSource.CreateLinkedTokenSource(userToken, _timeoutCts.Token);
        }
        else
        {
            _combinedCts = CancellationTokenSource.CreateLinkedTokenSource(userToken);
        }
    }

    /// <summary>
    /// Validates context length before generation starts.
    /// </summary>
    public void ValidateContextLength(int promptTokens)
    {
        if (_options.MaxContextTokens > 0 && promptTokens > _options.MaxContextTokens)
        {
            throw new ContextLimitExceededException(
                message: $"Context limit exceeded: {promptTokens} tokens exceeds maximum of {_options.MaxContextTokens} tokens",
                totalTokens: promptTokens,
                contextLimit: _options.MaxContextTokens);
        }
    }

    /// <summary>
    /// Checks if the next token should be generated (budget not exceeded).
    /// Call this before generating each token.
    /// </summary>
    /// <returns>True if generation should continue, false if budget exceeded.</returns>
    public bool ShouldContinue()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(BudgetEnforcer));
        }

        if (BudgetExceeded)
        {
            return false;
        }

        // Check token budget
        if (GeneratedTokens >= _options.MaxNewTokens)
        {
            BudgetExceeded = true;
            ExceededReason = "MaxNewTokens";
            return false;
        }

        // Check timeout
        if (_options.TimeoutMs > 0)
        {
            var elapsedMs = _stopwatch.ElapsedMilliseconds;
            if (elapsedMs >= _options.TimeoutMs)
            {
                BudgetExceeded = true;
                ExceededReason = "TimeoutMs";
                return false;
            }
        }

        // Check cancellation
        if (_combinedCts.Token.IsCancellationRequested)
        {
            BudgetExceeded = true;
            ExceededReason = "Cancellation";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Increments the generated token count.
    /// Call this after successfully generating a token.
    /// </summary>
    public void IncrementTokenCount()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(BudgetEnforcer));
        }

        GeneratedTokens++;
    }

    /// <summary>
    /// Throws BudgetExceededException if budget has been exceeded.
    /// Use this to fail fast when budget is exceeded in non-streaming scenarios.
    /// </summary>
    public void ThrowIfExceeded()
    {
        if (!BudgetExceeded)
        {
            return;
        }

        if (ExceededReason == "MaxNewTokens")
        {
            throw new BudgetExceededException(
                "MaxNewTokens",
                GeneratedTokens,
                _options.MaxNewTokens);
        }

        if (ExceededReason == "TimeoutMs")
        {
            var elapsedMs = _stopwatch.ElapsedMilliseconds;
            throw new BudgetExceededException(
                "TimeoutMs",
                elapsedMs,
                _options.TimeoutMs);
        }

        if (ExceededReason == "Cancellation")
        {
            // Don't throw for cancellation - caller should handle via CancellationToken
            return;
        }

        // Unknown reason
        throw new BudgetExceededException(
            "Unknown",
            GeneratedTokens,
            _options.MaxNewTokens);
    }

    /// <summary>
    /// Gets the remaining token budget.
    /// </summary>
    public int RemainingTokens => Math.Max(0, _options.MaxNewTokens - GeneratedTokens);

    /// <summary>
    /// Gets the elapsed generation time in milliseconds.
    /// </summary>
    public long ElapsedMilliseconds => _stopwatch.ElapsedMilliseconds;

    /// <summary>
    /// Gets the remaining time budget in milliseconds (or null if no timeout).
    /// </summary>
    public long? RemainingMilliseconds
    {
        get
        {
            if (_options.TimeoutMs <= 0)
            {
                return null;
            }

            var remaining = _options.TimeoutMs - _stopwatch.ElapsedMilliseconds;
            return Math.Max(0, remaining);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _stopwatch.Stop();
        _timeoutCts?.Dispose();
        _combinedCts.Dispose();
    }
}
