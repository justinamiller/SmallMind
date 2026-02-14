using System.Runtime.CompilerServices;

namespace SmallMind.Internal
{
    /// <summary>
    /// Internal adapter that bridges the stable public API to the existing SmallMind engine.
    /// This is NOT part of the public contract and may change between versions.
    /// </summary>
    internal sealed class SmallMindEngineAdapter : ISmallMindEngine
    {
        private readonly SmallMindOptions _publicOptions;
        private readonly Abstractions.ISmallMindEngine _internalEngine;
        private readonly Abstractions.IModelHandle _model;
        private readonly ISmallMindDiagnosticsSink? _diagnosticsSink;
        private bool _disposed;
        private readonly Guid _engineId;

        public SmallMindEngineAdapter(SmallMindOptions publicOptions)
        {
            _engineId = Guid.NewGuid();
            _publicOptions = publicOptions ?? throw new ArgumentNullException(nameof(publicOptions));
            _diagnosticsSink = publicOptions.DiagnosticsSink;

            // Validate options
            ValidateOptions(publicOptions);

            try
            {
                // Map public options to internal options
                var internalOptions = MapToInternalOptions(publicOptions);

                // Create internal engine
                _internalEngine = Engine.SmallMind.Create(internalOptions);

                EmitDiagnostic(new SmallMindDiagnosticEvent
                {
                    EventType = DiagnosticEventType.EngineCreated,
                    Timestamp = DateTimeOffset.UtcNow,
                    RequestId = _engineId,
                    Metadata = $"MaxContextTokens={publicOptions.MaxContextTokens}"
                });

                // Load model
                _model = LoadModelSync(publicOptions);

                EmitDiagnostic(new SmallMindDiagnosticEvent
                {
                    EventType = DiagnosticEventType.ModelLoaded,
                    Timestamp = DateTimeOffset.UtcNow,
                    RequestId = _engineId,
                    Metadata = $"Path={publicOptions.ModelPath}"
                });
            }
            catch (Abstractions.UnsupportedModelException ex)
            {
                EmitDiagnostic(new SmallMindDiagnosticEvent
                {
                    EventType = DiagnosticEventType.ModelLoadFailed,
                    Timestamp = DateTimeOffset.UtcNow,
                    RequestId = _engineId,
                    ErrorMessage = ex.Message
                });
                throw new UnsupportedModelFormatException(ex.Extension, ex.Message);
            }
            catch (Abstractions.SmallMindException ex)
            {
                EmitDiagnostic(new SmallMindDiagnosticEvent
                {
                    EventType = DiagnosticEventType.ModelLoadFailed,
                    Timestamp = DateTimeOffset.UtcNow,
                    RequestId = _engineId,
                    ErrorMessage = ex.Message
                });
                throw new ModelLoadFailedException(publicOptions.ModelPath, ex.Message, ex);
            }
            catch (Exception ex)
            {
                EmitDiagnostic(new SmallMindDiagnosticEvent
                {
                    EventType = DiagnosticEventType.ModelLoadFailed,
                    Timestamp = DateTimeOffset.UtcNow,
                    RequestId = _engineId,
                    ErrorMessage = ex.Message
                });
                throw new ModelLoadFailedException(publicOptions.ModelPath, ex.Message, ex);
            }
        }

        public EngineCapabilities GetCapabilities()
        {
            ThrowIfDisposed();

            var internalCaps = _internalEngine.Capabilities;
            var modelInfo = _model.Info;

            return new EngineCapabilities
            {
                SupportsStreaming = internalCaps.SupportsStreaming,
                SupportsEmbeddings = false, // Not yet implemented in current engine
                SupportsKvCache = internalCaps.SupportsKvCache,
                SupportsBatching = internalCaps.SupportsBatching,
                MaxContextTokens = _publicOptions.MaxContextTokens,
                ModelFormat = System.IO.Path.GetExtension(_publicOptions.ModelPath).TrimStart('.'),
                Quantization = modelInfo.QuantizationSchemes.Length > 0
                    ? string.Join(", ", modelInfo.QuantizationSchemes)
                    : "FP32",
                TokenizerId = null // Not exposed by current model info
            };
        }

        public ITextGenerationSession CreateTextGenerationSession(TextGenerationOptions options)
        {
            ThrowIfDisposed();

            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            // Validate options
            ValidateTextGenerationOptions(options);

            var sessionId = Guid.NewGuid().ToString();

            EmitDiagnostic(new SmallMindDiagnosticEvent
            {
                EventType = DiagnosticEventType.SessionCreated,
                Timestamp = DateTimeOffset.UtcNow,
                RequestId = Guid.NewGuid(),
                SessionId = sessionId,
                Metadata = $"MaxOutputTokens={options.MaxOutputTokens}"
            });

            return new TextGenerationSessionAdapter(
                _internalEngine,
                _model,
                options,
                _publicOptions,
                _diagnosticsSink,
                sessionId);
        }

        public IEmbeddingSession CreateEmbeddingSession(EmbeddingOptions options)
        {
            ThrowIfDisposed();

            // Embeddings not yet supported by the current engine
            throw new InternalErrorException("Embeddings are not yet supported. Check SupportsEmbeddings capability before calling.");
        }

        public IChatClient CreateChatClient(ChatClientOptions options)
        {
            ThrowIfDisposed();

            if (options == null)
                throw new ArgumentNullException(nameof(options));

            var sessionId = options.SessionId ?? Guid.NewGuid().ToString();

            EmitDiagnostic(new SmallMindDiagnosticEvent
            {
                EventType = DiagnosticEventType.SessionCreated,
                Timestamp = DateTimeOffset.UtcNow,
                RequestId = Guid.NewGuid(),
                SessionId = sessionId,
                Metadata = $"ChatClient,EnableKvCache={options.EnableKvCache}"
            });

            // Create session options
            var sessionOptions = new Abstractions.SessionOptions
            {
                SessionId = sessionId,
                EnableKvCache = options.EnableKvCache,
                MaxKvCacheTokens = options.MaxKvCacheTokens
            };

            // Create internal chat session
            var chatSession = _internalEngine.CreateChatSession(_model, sessionOptions);

            // Wrap in public API
            return new ChatClient(chatSession, options.DefaultTelemetry);
        }

        private static void ValidateOptions(SmallMindOptions options)
        {
            if (string.IsNullOrWhiteSpace(options.ModelPath))
            {
                throw new InvalidOptionsException(nameof(options.ModelPath), "Model path cannot be empty.");
            }

            if (!System.IO.File.Exists(options.ModelPath))
            {
                throw new InvalidOptionsException(nameof(options.ModelPath), $"Model file not found: {options.ModelPath}");
            }

            if (options.MaxContextTokens <= 0)
            {
                throw new InvalidOptionsException(nameof(options.MaxContextTokens), "Must be greater than 0.");
            }

            if (options.MaxBatchSize <= 0)
            {
                throw new InvalidOptionsException(nameof(options.MaxBatchSize), "Must be greater than 0.");
            }

            if (options.ThreadCount.HasValue && options.ThreadCount.Value < 0)
            {
                throw new InvalidOptionsException(nameof(options.ThreadCount), "Cannot be negative.");
            }

            if (options.RequestTimeoutMs.HasValue && options.RequestTimeoutMs.Value < 0)
            {
                throw new InvalidOptionsException(nameof(options.RequestTimeoutMs), "Cannot be negative.");
            }
        }

        private static void ValidateTextGenerationOptions(TextGenerationOptions options)
        {
            if (options.Temperature < 0.0f || options.Temperature > 2.0f)
            {
                throw new InvalidOptionsException(nameof(options.Temperature), "Must be between 0.0 and 2.0.");
            }

            if (options.TopP < 0.0f || options.TopP > 1.0f)
            {
                throw new InvalidOptionsException(nameof(options.TopP), "Must be between 0.0 and 1.0.");
            }

            if (options.TopK < 0)
            {
                throw new InvalidOptionsException(nameof(options.TopK), "Cannot be negative.");
            }

            if (options.MaxOutputTokens <= 0)
            {
                throw new InvalidOptionsException(nameof(options.MaxOutputTokens), "Must be greater than 0.");
            }
        }

        private static Abstractions.EngineOptions MapToInternalOptions(SmallMindOptions publicOptions)
        {
            return new Abstractions.EngineOptions
            {
                DefaultThreads = publicOptions.ThreadCount ?? 0,
                EnableKvCache = publicOptions.EnableKvCache,
                EnableBatching = publicOptions.MaxBatchSize > 1,
                EnableRag = false, // RAG not exposed in public API yet
                EnableDeterministicMode = false
            };
        }

        private Abstractions.IModelHandle LoadModelSync(SmallMindOptions publicOptions)
        {
            var request = new Abstractions.ModelLoadRequest
            {
                Path = publicOptions.ModelPath,
                AllowGgufImport = publicOptions.AllowGgufImport,
                ImportCacheDirectory = publicOptions.GgufCacheDirectory,
                Threads = publicOptions.ThreadCount ?? 0,
                MaxMemoryBytes = null
            };

            // Load model synchronously
            // Since model loading is CPU-bound (no actual I/O awaits in the async path),
            // we use Task.Run to avoid SynchronizationContext deadlocks in ASP.NET/UI contexts
            return Task.Run(async () =>
                await _internalEngine.LoadModelAsync(request, CancellationToken.None)
                    .ConfigureAwait(false)
            ).GetAwaiter().GetResult();
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
                throw new ObjectDisposedException(nameof(SmallMindEngineAdapter));
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _model?.Dispose();
            _internalEngine?.Dispose();
        }
    }
}
