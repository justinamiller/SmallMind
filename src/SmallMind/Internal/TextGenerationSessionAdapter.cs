using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using SmallMind.Abstractions;

namespace SmallMind.Internal
{
    /// <summary>
    /// Adapter for text generation session.
    /// NOT thread-safe - callers must synchronize access.
    /// </summary>
    internal sealed class TextGenerationSessionAdapter : ITextGenerationSession
    {
        private readonly Abstractions.ISmallMindEngine _internalEngine;
        private readonly Abstractions.IModelHandle _model;
        private readonly TextGenerationOptions _sessionOptions;
        private readonly SmallMindOptions _engineOptions;
        private readonly ISmallMindDiagnosticsSink? _diagnosticsSink;
        private readonly string _sessionId;
        private bool _disposed;

        public TextGenerationSessionAdapter(
            Abstractions.ISmallMindEngine internalEngine,
            Abstractions.IModelHandle model,
            TextGenerationOptions sessionOptions,
            SmallMindOptions engineOptions,
            ISmallMindDiagnosticsSink? diagnosticsSink,
            string sessionId)
        {
            _internalEngine = internalEngine;
            _model = model;
            _sessionOptions = sessionOptions;
            _engineOptions = engineOptions;
            _diagnosticsSink = diagnosticsSink;
            _sessionId = sessionId;
        }

        public GenerationResult Generate(TextGenerationRequest request, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            var requestId = Guid.NewGuid();
            var stopwatch = Stopwatch.StartNew();

            EmitDiagnostic(new SmallMindDiagnosticEvent
            {
                EventType = DiagnosticEventType.RequestStarted,
                Timestamp = DateTimeOffset.UtcNow,
                RequestId = requestId,
                SessionId = _sessionId
            });

            try
            {
                // Build internal generation request
                var internalRequest = MapToInternalRequest(request);

                // Apply timeout if configured
                using var timeoutCts = CreateTimeoutCancellationTokenSource();
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken,
                    timeoutCts?.Token ?? CancellationToken.None);

                // Execute generation
                Abstractions.GenerationResult internalResult;
                try
                {
                    // Use Task.Run to avoid SynchronizationContext deadlocks in ASP.NET/UI contexts
                    // Since generation is CPU-bound, this is safe and prevents deadlocks
                    internalResult = Task.Run(async () =>
                        await _internalEngine.GenerateAsync(_model, internalRequest, linkedCts.Token)
                            .ConfigureAwait(false)
                    ).GetAwaiter().GetResult();
                }
                catch (OperationCanceledException) when (timeoutCts?.IsCancellationRequested == true)
                {
                    EmitDiagnostic(new SmallMindDiagnosticEvent
                    {
                        EventType = DiagnosticEventType.RequestFailed,
                        Timestamp = DateTimeOffset.UtcNow,
                        RequestId = requestId,
                        SessionId = _sessionId,
                        DurationMs = stopwatch.Elapsed.TotalMilliseconds,
                        ErrorMessage = "Timeout"
                    });
                    throw new RequestCancelledException($"Request timeout after {_engineOptions.RequestTimeoutMs}ms");
                }
                catch (OperationCanceledException)
                {
                    EmitDiagnostic(new SmallMindDiagnosticEvent
                    {
                        EventType = DiagnosticEventType.RequestCancelled,
                        Timestamp = DateTimeOffset.UtcNow,
                        RequestId = requestId,
                        SessionId = _sessionId,
                        DurationMs = stopwatch.Elapsed.TotalMilliseconds
                    });
                    throw new RequestCancelledException("User cancelled request");
                }
                catch (Abstractions.ContextLimitExceededException ex)
                {
                    EmitDiagnostic(new SmallMindDiagnosticEvent
                    {
                        EventType = DiagnosticEventType.RequestFailed,
                        Timestamp = DateTimeOffset.UtcNow,
                        RequestId = requestId,
                        SessionId = _sessionId,
                        DurationMs = stopwatch.Elapsed.TotalMilliseconds,
                        ErrorMessage = ex.Message
                    });
                    throw new ContextOverflowException(ex.TotalTokens, ex.ContextLimit);
                }
                catch (Abstractions.SmallMindException ex)
                {
                    EmitDiagnostic(new SmallMindDiagnosticEvent
                    {
                        EventType = DiagnosticEventType.RequestFailed,
                        Timestamp = DateTimeOffset.UtcNow,
                        RequestId = requestId,
                        SessionId = _sessionId,
                        DurationMs = stopwatch.Elapsed.TotalMilliseconds,
                        ErrorMessage = ex.Message
                    });
                    throw new InferenceFailedException($"Generation failed: {ex.Message}", ex);
                }
                catch (Exception ex)
                {
                    EmitDiagnostic(new SmallMindDiagnosticEvent
                    {
                        EventType = DiagnosticEventType.RequestFailed,
                        Timestamp = DateTimeOffset.UtcNow,
                        RequestId = requestId,
                        SessionId = _sessionId,
                        DurationMs = stopwatch.Elapsed.TotalMilliseconds,
                        ErrorMessage = ex.Message
                    });
                    throw new InferenceFailedException($"Unexpected error during generation: {ex.Message}", ex);
                }

                stopwatch.Stop();

                // Map result
                var result = MapToPublicResult(internalResult, stopwatch.Elapsed.TotalMilliseconds);

                EmitDiagnostic(new SmallMindDiagnosticEvent
                {
                    EventType = DiagnosticEventType.RequestCompleted,
                    Timestamp = DateTimeOffset.UtcNow,
                    RequestId = requestId,
                    SessionId = _sessionId,
                    DurationMs = stopwatch.Elapsed.TotalMilliseconds,
                    CompletionTokens = result.Usage.CompletionTokens
                });

                return result;
            }
            catch (SmallMindException)
            {
                throw; // Already wrapped
            }
            catch (Exception ex)
            {
                EmitDiagnostic(new SmallMindDiagnosticEvent
                {
                    EventType = DiagnosticEventType.RequestFailed,
                    Timestamp = DateTimeOffset.UtcNow,
                    RequestId = requestId,
                    SessionId = _sessionId,
                    DurationMs = stopwatch.Elapsed.TotalMilliseconds,
                    ErrorMessage = ex.Message
                });
                throw new InternalErrorException($"Unexpected error: {ex.Message}", ex);
            }
        }

        public async IAsyncEnumerable<TokenResult> GenerateStreaming(
            TextGenerationRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            var requestId = Guid.NewGuid();
            var stopwatch = Stopwatch.StartNew();

            EmitDiagnostic(new SmallMindDiagnosticEvent
            {
                EventType = DiagnosticEventType.RequestStarted,
                Timestamp = DateTimeOffset.UtcNow,
                RequestId = requestId,
                SessionId = _sessionId
            });

            // Build internal generation request
            var internalRequest = MapToInternalRequest(request);

            // Apply timeout if configured
            using var timeoutCts = CreateTimeoutCancellationTokenSource();
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                timeoutCts?.Token ?? CancellationToken.None);

            IAsyncEnumerable<Abstractions.TokenEvent> stream;
            try
            {
                stream = _internalEngine.GenerateStreamingAsync(_model, internalRequest, linkedCts.Token);
            }
            catch (Abstractions.ContextLimitExceededException ex)
            {
                EmitDiagnostic(new SmallMindDiagnosticEvent
                {
                    EventType = DiagnosticEventType.RequestFailed,
                    Timestamp = DateTimeOffset.UtcNow,
                    RequestId = requestId,
                    SessionId = _sessionId,
                    ErrorMessage = ex.Message
                });
                throw new ContextOverflowException(ex.TotalTokens, ex.ContextLimit);
            }
            catch (Exception ex)
            {
                EmitDiagnostic(new SmallMindDiagnosticEvent
                {
                    EventType = DiagnosticEventType.RequestFailed,
                    Timestamp = DateTimeOffset.UtcNow,
                    RequestId = requestId,
                    SessionId = _sessionId,
                    ErrorMessage = ex.Message
                });
                throw new InferenceFailedException($"Failed to start streaming: {ex.Message}", ex);
            }

            // Use helper to avoid yield in try-catch
            await foreach (var tokenResult in GenerateStreamingImpl(
                stream, requestId, stopwatch, linkedCts.Token, timeoutCts))
            {
                yield return tokenResult;
            }

            stopwatch.Stop();

            EmitDiagnostic(new SmallMindDiagnosticEvent
            {
                EventType = DiagnosticEventType.RequestCompleted,
                Timestamp = DateTimeOffset.UtcNow,
                RequestId = requestId,
                SessionId = _sessionId,
                DurationMs = stopwatch.Elapsed.TotalMilliseconds
            });
        }

        private async IAsyncEnumerable<TokenResult> GenerateStreamingImpl(
            IAsyncEnumerable<Abstractions.TokenEvent> stream,
            Guid requestId,
            Stopwatch stopwatch,
            [EnumeratorCancellation] CancellationToken cancellationToken,
            CancellationTokenSource? timeoutCts)
        {
            double? ttft = null;
            int tokenCount = 0;

            await foreach (var token in stream.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                if (token.Kind == Abstractions.TokenEventKind.Token)
                {
                    tokenCount++;

                    // Capture time to first token
                    if (!ttft.HasValue)
                    {
                        ttft = stopwatch.Elapsed.TotalMilliseconds;
                    }

                    var tokenResult = new TokenResult
                    {
                        TokenText = token.Text.ToString(),
                        TokenId = token.TokenId,
                        IsSpecial = false, // Not exposed by internal API
                        Usage = new UsagePartial
                        {
                            PromptTokens = 0, // Not available during streaming
                            CompletionTokensSoFar = tokenCount
                        },
                        Timings = new GenerationTimingsPartial
                        {
                            TimeToFirstTokenMs = ttft,
                            ElapsedMs = stopwatch.Elapsed.TotalMilliseconds
                        }
                    };

                    EmitDiagnostic(new SmallMindDiagnosticEvent
                    {
                        EventType = DiagnosticEventType.TokenEmitted,
                        Timestamp = DateTimeOffset.UtcNow,
                        RequestId = requestId,
                        SessionId = _sessionId,
                        CompletionTokens = tokenCount
                    });

                    yield return tokenResult;
                }
                else if (token.Kind == Abstractions.TokenEventKind.Error)
                {
                    EmitDiagnostic(new SmallMindDiagnosticEvent
                    {
                        EventType = DiagnosticEventType.RequestFailed,
                        Timestamp = DateTimeOffset.UtcNow,
                        RequestId = requestId,
                        SessionId = _sessionId,
                        DurationMs = stopwatch.Elapsed.TotalMilliseconds,
                        ErrorMessage = token.Error ?? "Unknown error"
                    });
                    throw new InferenceFailedException(token.Error ?? "Unknown error during streaming");
                }
            }
        }

        private Abstractions.GenerationRequest MapToInternalRequest(TextGenerationRequest request)
        {
            var maxTokens = request.MaxOutputTokensOverride ?? _sessionOptions.MaxOutputTokens;
            var stopSequences = request.StopSequencesOverride.Count > 0
                ? request.StopSequencesOverride.ToArray()
                : _sessionOptions.StopSequences.ToArray();

            return new Abstractions.GenerationRequest
            {
                Prompt = request.Prompt.ToString(),
                Options = new Abstractions.GenerationOptions
                {
                    MaxNewTokens = maxTokens,
                    MaxContextTokens = _engineOptions.MaxContextTokens,
                    TimeoutMs = _engineOptions.RequestTimeoutMs ?? 0,
                    Mode = request.Seed.HasValue
                        ? Abstractions.GenerationMode.Deterministic
                        : Abstractions.GenerationMode.Exploratory,
                    Seed = (uint)(request.Seed ?? 42),
                    Temperature = _sessionOptions.Temperature,
                    TopK = _sessionOptions.TopK,
                    TopP = _sessionOptions.TopP,
                    Stop = stopSequences.Length > 0 ? stopSequences : null
                }
            };
        }

        private GenerationResult MapToPublicResult(Abstractions.GenerationResult internalResult, double totalMs)
        {
            var finishReason = internalResult.StopReason switch
            {
                "completed" => FinishReason.Completed,
                var r when r?.StartsWith("budget_exceeded") == true => FinishReason.Length,
                _ => FinishReason.Completed
            };

            return new GenerationResult
            {
                Text = internalResult.Text,
                Usage = new Usage
                {
                    PromptTokens = 0, // Not exposed by current internal API
                    CompletionTokens = internalResult.GeneratedTokens
                },
                Timings = new GenerationTimings(0, totalMs, internalResult.GeneratedTokens),
                FinishReason = finishReason,
                Warnings = null
            };
        }

        private CancellationTokenSource? CreateTimeoutCancellationTokenSource()
        {
            if (_engineOptions.RequestTimeoutMs.HasValue && _engineOptions.RequestTimeoutMs.Value > 0)
            {
                return new CancellationTokenSource(_engineOptions.RequestTimeoutMs.Value);
            }
            return null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EmitDiagnostic(in SmallMindDiagnosticEvent evt)
        {
            try
            {
                _diagnosticsSink?.OnEvent(evt);
            }
            catch
            {
                // Diagnostics must never throw
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(TextGenerationSessionAdapter));
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            EmitDiagnostic(new SmallMindDiagnosticEvent
            {
                EventType = DiagnosticEventType.SessionDisposed,
                Timestamp = DateTimeOffset.UtcNow,
                RequestId = Guid.NewGuid(),
                SessionId = _sessionId
            });
        }
    }
}
