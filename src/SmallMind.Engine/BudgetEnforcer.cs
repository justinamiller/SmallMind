using System;
using System.Diagnostics;
using System.Threading;
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

        // Create timeout cancellation token if timeout is specified
        if (_options.TimeoutMs.HasValue && _options.TimeoutMs.Value > 0)
        {
            _timeoutCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(_options.TimeoutMs.Value));
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
        if (_options.MaxContextTokens.HasValue && promptTokens > _options.MaxContextTokens.Value)
        {
            throw new ContextLimitExceededException(
                promptTokens,
                _options.MaxContextTokens.Value,
                $"Prompt length ({promptTokens} tokens) exceeds maximum context length ({_options.MaxContextTokens.Value} tokens).{Environment.NewLine}" +
                $"Remediation: Reduce prompt length or increase MaxContextTokens (if model supports larger context).");
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
        if (_options.TimeoutMs.HasValue)
        {
            var elapsedMs = _stopwatch.ElapsedMilliseconds;
            if (elapsedMs >= _options.TimeoutMs.Value)
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
                GeneratedTokens,
                _options.MaxNewTokens,
                $"Generation stopped: MaxNewTokens limit ({_options.MaxNewTokens}) reached.{Environment.NewLine}" +
                $"Generated {GeneratedTokens} tokens.{Environment.NewLine}" +
                $"Remediation: Increase MaxNewTokens if more output is needed.");
        }

        if (ExceededReason == "TimeoutMs")
        {
            var elapsedMs = _stopwatch.ElapsedMilliseconds;
            throw new BudgetExceededException(
                (int)elapsedMs,
                _options.TimeoutMs!.Value,
                $"Generation stopped: TimeoutMs limit ({_options.TimeoutMs.Value}ms) reached.{Environment.NewLine}" +
                $"Elapsed time: {elapsedMs}ms, Generated {GeneratedTokens} tokens.{Environment.NewLine}" +
                $"Remediation: Increase TimeoutMs or optimize generation speed.");
        }

        if (ExceededReason == "Cancellation")
        {
            // Don't throw for cancellation - caller should handle via CancellationToken
            return;
        }

        // Unknown reason
        throw new BudgetExceededException(
            GeneratedTokens,
            _options.MaxNewTokens,
            $"Generation stopped: Budget exceeded ({ExceededReason ?? "unknown reason"}).{Environment.NewLine}" +
            $"Generated {GeneratedTokens} tokens.");
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
            if (!_options.TimeoutMs.HasValue)
            {
                return null;
            }

            var remaining = _options.TimeoutMs.Value - _stopwatch.ElapsedMilliseconds;
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
