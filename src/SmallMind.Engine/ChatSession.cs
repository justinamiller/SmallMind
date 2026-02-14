using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using SmallMind.Abstractions;
using SmallMind.Rag;
using SmallMind.Rag.Pipeline;
using SmallMind.Rag.Prompting;
using SmallMind.Runtime;
using SmallMind.Runtime.Cache;
using SmallMind.Tokenizers;
using PublicGenerationOptions = SmallMind.Abstractions.GenerationOptions;
using RagRetrievalOptions = SmallMind.Rag.RagOptions.RetrievalOptions;

namespace SmallMind.Engine
{
    /// <summary>
    /// Context budget information for a chat session.
    /// </summary>
    internal readonly struct ContextBudget
    {
        /// <summary>Maximum context tokens (model.BlockSize).</summary>
        public readonly int MaxContextTokens;

        /// <summary>Current history tokens (from tokenizer).</summary>
        public readonly int CurrentHistoryTokens;

        /// <summary>Reserved tokens for generation (maxNewTokens).</summary>
        public readonly int ReservedForGeneration;

        /// <summary>Available tokens for prompt (MaxContext - CurrentHistory - Reserved).</summary>
        public readonly int AvailableTokens;

        /// <summary>Number of conversation turns.</summary>
        public readonly int TurnCount;

        /// <summary>True if overflow would trigger truncation.</summary>
        public readonly bool WouldTruncate;

        public ContextBudget(int maxContext, int currentHistory, int reserved, int turnCount)
        {
            MaxContextTokens = maxContext;
            CurrentHistoryTokens = currentHistory;
            ReservedForGeneration = reserved;
            AvailableTokens = maxContext - currentHistory - reserved;
            TurnCount = turnCount;
            WouldTruncate = AvailableTokens < 0;
        }
    }

    /// <summary>
    /// Internal implementation of IChatSession.
    /// Maintains conversation context and KV cache across multiple turns.
    /// NOT thread-safe: Sessions must not be accessed concurrently.
    /// </summary>
    internal sealed class ChatSession : IChatSession
    {
        private readonly ModelHandle _modelHandle;
        private readonly ChatSessionOptions _options;
        private readonly SmallMindOptions _engineOptions;
        private readonly ITokenizer _tokenizer;
        private readonly List<ChatMessage> _conversationHistory;
        private readonly string _sessionId;
        private readonly DateTimeOffset _createdAt;
        private readonly ChatTemplateType _templateType;
        private readonly IKvCacheStore _kvCacheStore;
        private readonly ModelShape _modelShape;
        private readonly RagPipeline? _ragPipeline;
        private int _turnCount;
        private int _cachedTokenCount; // Actual cached token count from KV cache
        private int[]? _lastPromptTokenIds; // Last tokenized prompt for delta calculation
        private bool _lastTurnWasTruncated; // Track if last turn required truncation
        private bool _disposed;

        // Thread-safety guard: Sessions are NOT thread-safe and must not be used concurrently
        private int _inUse = 0; // 0 = not in use, 1 = in use

        // Persistent InferenceSession for KV cache reuse (Phase 2.1)
        private InferenceSession? _persistentInferenceSession;
        private int _persistentSessionPosition = 0;

        // Diagnostic counters
        private int _truncatedTurns;
        private int _kvCacheHits;
        private int _kvCacheMisses;
        private readonly int _nanRecoveries;
        private int _degenerateOutputRecoveries;
        private long _totalTokensGenerated;
        private long _totalTokensFromCache;
        private readonly Stopwatch _totalInferenceTimer = new Stopwatch();

        public ChatSession(
            ModelHandle modelHandle,
            ChatSessionOptions options,
            SmallMindOptions engineOptions,
            RagPipeline? ragPipeline = null)
        {
            _modelHandle = modelHandle ?? throw new ArgumentNullException(nameof(modelHandle));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _engineOptions = engineOptions ?? throw new ArgumentNullException(nameof(engineOptions));
            _tokenizer = modelHandle.Tokenizer ?? throw new InvalidOperationException("Model handle does not have a tokenizer");
            _conversationHistory = new List<ChatMessage>();
            _sessionId = options.SessionId ?? Guid.NewGuid().ToString("N");
            _createdAt = DateTimeOffset.UtcNow;
            _ragPipeline = ragPipeline;

            // Detect or use configured chat template
            _templateType = options.ChatTemplateType == ChatTemplateType.Auto
                ? ChatTemplates.DetectTemplate(modelHandle.Info.Name, metadata: null)
                : options.ChatTemplateType;

            // Initialize KV cache store (from options or create default)
            if (options.KvCacheStore != null)
            {
                _kvCacheStore = options.KvCacheStore;
            }
            else
            {
                // Create default LRU cache store with reasonable limits
                var cacheOptions = new KvCacheOptions
                {
                    Enabled = options.EnableKvCache,
                    MaxTokensPerSession = options.MaxKvCacheTokens ?? modelHandle.Model.BlockSize,
                    MaxSessions = 100,
                    MaxBytesTotal = 512L * 1024 * 1024 // 512MB default
                };
                _kvCacheStore = new LruKvCacheStore(cacheOptions);
            }

            // Compute model shape for KV cache validation
            _modelShape = new ModelShape(
                layers: modelHandle.Model.NumLayers,
                heads: modelHandle.Model.NumHeads,
                headDim: modelHandle.Model.EmbedDim / modelHandle.Model.NumHeads
            );
        }

        public SessionInfo Info => new SessionInfo(
            sessionId: _sessionId,
            createdAt: _createdAt,
            turnCount: _turnCount,
            kvCacheTokens: _cachedTokenCount); // Actual cached token count (0 for Phase 1)

        public ValueTask AddSystemAsync(string systemPrompt, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (string.IsNullOrWhiteSpace(systemPrompt))
            {
                throw new ArgumentException("System prompt cannot be empty", nameof(systemPrompt));
            }

            _conversationHistory.Add(new ChatMessage
            {
                Role = ChatRole.System,
                Content = systemPrompt
            });

            return ValueTask.CompletedTask;
        }

        /// <summary>
        /// Acquires the session for use. Throws if already in use by another thread.
        /// </summary>
        private void AcquireSession()
        {
            if (Interlocked.CompareExchange(ref _inUse, 1, 0) != 0)
            {
                throw new InvalidOperationException(
                    $"ChatSession '{_sessionId}' is already in use. " +
                    "Sessions are not thread-safe and must not be accessed concurrently. " +
                    "Create separate sessions for concurrent users.");
            }
        }

        /// <summary>
        /// Releases the session after use.
        /// </summary>
        private void ReleaseSession()
        {
            Interlocked.Exchange(ref _inUse, 0);
        }

        public async ValueTask<GenerationResult> SendAsync(
            ChatMessage message,
            PublicGenerationOptions options,
            CancellationToken cancellationToken = default)
        {
            AcquireSession();
            try
            {
                ThrowIfDisposed();

                if (message == null)
                {
                    throw new ArgumentNullException(nameof(message));
                }

                _totalInferenceTimer.Start();

                // Add user message to history
                _conversationHistory.Add(message);

                // RAG integration
                List<Citation>? citations = null;
                string prompt;
                List<string>? warnings = null;

                if (_options.EnableRag && _ragPipeline != null && message.Role == ChatRole.User)
                {
                    // Retrieve relevant chunks
                    var topK = _options.RagOptions?.TopK ?? 5;
                    var chunks = _ragPipeline.Retrieve(message.Content, userContext: null, topK);

                    // Build RAG prompt
                    // Using manual loop with dictionary lookup for deduplication (performance optimization, avoiding LINQ overhead)
                    // Pre-allocate with smaller capacity since duplicates are expected and will be filtered out
                    var chunkStore = new Dictionary<string, Chunk>(Math.Max(4, topK / 2));
                    foreach (var chunk in chunks)
                    {
                        if (!chunkStore.TryGetValue(chunk.ChunkId, out _))
                        {
                            chunkStore[chunk.ChunkId] = new Chunk
                            {
                                ChunkId = chunk.ChunkId,
                                DocId = chunk.DocId,
                                Text = chunk.Excerpt,
                                SourceUri = chunk.DocId,
                                Title = chunk.DocId
                            };
                        }
                    }

                    var composer = new PromptComposer(new RagRetrievalOptions { TopK = topK });
                    var ragPrompt = composer.ComposePrompt(message.Content, chunks, chunkStore);

                    // Build conversation context with RAG prompt
                    var tempHistory = new List<ChatMessage>(_conversationHistory);
                    tempHistory[tempHistory.Count - 1] = new ChatMessage
                    {
                        Role = ChatRole.User,
                        Content = ragPrompt
                    };
                    prompt = BuildPromptFromMessages(tempHistory);

                    // Extract citations (avoid LINQ for better performance)
                    if (_options.RagOptions?.IncludeCitations ?? true)
                    {
                        citations = new List<Citation>(chunks.Count);
                        for (int i = 0; i < chunks.Count; i++)
                        {
                            var c = chunks[i];
                            citations.Add(new Citation
                            {
                                Source = c.DocId,
                                Title = null, // RetrievedChunk doesn't include title metadata; would need pipeline API enhancement
                                Snippet = c.Excerpt,
                                RelevanceScore = c.Score
                            });
                        }
                    }
                }
                else
                {
                    // Apply overflow strategy and build conversation prompt
                    prompt = ApplyOverflowStrategyAndBuildPrompt(options.MaxNewTokens, ref warnings);
                }

                // Tokenize current prompt
                var promptTokenIds = _tokenizer.Encode(prompt).ToArray();

                // Calculate KV cache delta and determine prefill strategy
                SessionId sessionId = new SessionId(_sessionId);
                KvCacheEntry? kvCache = null;
                int startPosition = 0;
                int maxTokens = _options.MaxKvCacheTokens ?? _modelHandle.Model.BlockSize;

                if (_options.EnableKvCache)
                {
                    // Get or create KV cache entry
                    kvCache = _kvCacheStore.GetOrCreate(sessionId, _modelShape, maxTokens);

                    // Calculate longest common prefix (LCP) with last prompt
                    if (_lastPromptTokenIds != null && kvCache.CurrentTokenCount > 0)
                    {
                        int lcp = CalculateLongestCommonPrefix(_lastPromptTokenIds, promptTokenIds);

                        // If LCP matches cached count, we can use incremental prefill
                        if (lcp == _cachedTokenCount && lcp == kvCache.CurrentTokenCount)
                        {
                            startPosition = lcp;
                            _kvCacheHits++;
                            _totalTokensFromCache += lcp;
                        }
                        else
                        {
                            // Cache mismatch or evicted - reset and do full prefill
                            kvCache.Reset();
                            startPosition = 0;
                            _cachedTokenCount = 0;
                            _kvCacheMisses++;

                            // Invalidate persistent session (Phase 2.1)
                            if (_persistentInferenceSession != null)
                            {
                                _persistentInferenceSession.Dispose();
                                _persistentInferenceSession = null;
                                _persistentSessionPosition = 0;
                            }
                        }
                    }
                    else
                    {
                        _kvCacheMisses++;
                    }
                }

                // OOM protection: estimate memory for KV cache (maxTokens * embedDim * 4 bytes per float)
                long estimatedMemoryBytes = (long)options.MaxNewTokens * _modelHandle.Model.EmbedDim * 4;
                var gcInfo = GC.GetGCMemoryInfo();
                if (gcInfo.TotalAvailableMemoryBytes > 0)
                {
                    double usageRatio = (double)estimatedMemoryBytes / gcInfo.TotalAvailableMemoryBytes;
                    if (usageRatio > 0.9)
                    {
                        throw new Abstractions.InsufficientMemoryException(estimatedMemoryBytes, gcInfo.TotalAvailableMemoryBytes);
                    }
                }

                // Timeout support
                CancellationTokenSource? timeoutCts = null;
                CancellationToken effectiveToken = cancellationToken;
                if (options.TimeoutMs > 0)
                {
                    timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    timeoutCts.CancelAfter(options.TimeoutMs);
                    effectiveToken = timeoutCts.Token;
                }

                // Generate response
                InferenceSession session;

                if (_persistentInferenceSession == null)
                {
                    // First turn: create new session
                    session = _modelHandle.CreateInferenceSession(options, _engineOptions);
                    _persistentInferenceSession = session;
                    _persistentSessionPosition = 0;
                }
                else
                {
                    // Subsequent turns: reuse existing session
                    session = _persistentInferenceSession;
                }

                try
                {
                    string response;
                    if (startPosition > 0 && kvCache != null)
                    {
                        // Incremental prefill: only process new tokens
                        var deltaTokenIds = new int[promptTokenIds.Length - startPosition];
                        Array.Copy(promptTokenIds, startPosition, deltaTokenIds, 0, deltaTokenIds.Length);
                        var deltaPrompt = _tokenizer.Decode(new List<int>(deltaTokenIds));

                        response = await session.GenerateAsync(
                            prompt: deltaPrompt,
                            metrics: null,
                            cancellationToken: effectiveToken);
                    }
                    else
                    {
                        // Full prefill
                        response = await session.GenerateAsync(
                            prompt: prompt,
                            metrics: null,
                            cancellationToken: effectiveToken);
                    }

                    // Degenerate output detection
                    var (cleanedResponse, stopReason) = DetectAndCleanDegenerateOutput(response, options);
                    if (stopReason != "completed")
                    {
                        _degenerateOutputRecoveries++;
                        response = cleanedResponse;
                    }

                    // Add assistant response to history
                    _conversationHistory.Add(new ChatMessage
                    {
                        Role = ChatRole.Assistant,
                        Content = response
                    });

                    _turnCount++;
                    _totalTokensGenerated += options.MaxNewTokens; // Approximate
                    _totalInferenceTimer.Stop();

                    // Update cache tracking
                    if (_options.EnableKvCache && kvCache != null)
                    {
                        _lastPromptTokenIds = promptTokenIds;
                        _cachedTokenCount = promptTokenIds.Length;
                        _kvCacheStore.Touch(sessionId);
                    }

                    return new GenerationResult
                    {
                        Text = response,
                        GeneratedTokens = options.MaxNewTokens, // Approximate
                        StoppedByBudget = false,
                        StopReason = stopReason,
                        Citations = citations,
                        Warnings = warnings
                    };
                }
                catch (OperationCanceledException) when (options.TimeoutMs > 0)
                {
                    _totalInferenceTimer.Stop();
                    // Timeout - return partial result if available
                    return new GenerationResult
                    {
                        Text = string.Empty,
                        GeneratedTokens = 0,
                        StoppedByBudget = true,
                        StopReason = "timeout",
                        Citations = citations,
                        Warnings = warnings
                    };
                }
                finally
                {
                    timeoutCts?.Dispose();
                    // Don't dispose persistent session - it will be reused across turns
                }
            }
            finally
            {
                ReleaseSession();
            }
        }

        public async IAsyncEnumerable<TokenEvent> SendStreamingAsync(
            ChatMessage message,
            PublicGenerationOptions options,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            AcquireSession();
            try
            {
                ThrowIfDisposed();

                if (message == null)
                {
                    throw new ArgumentNullException(nameof(message));
                }

                _totalInferenceTimer.Start();

                // Add user message to history
                _conversationHistory.Add(message);

                // RAG integration
                List<Citation>? citations = null;
                string prompt;
                List<string>? warnings = null;

                if (_options.EnableRag && _ragPipeline != null && message.Role == ChatRole.User)
                {
                    // Retrieve relevant chunks
                    var topK = _options.RagOptions?.TopK ?? 5;
                    var chunks = _ragPipeline.Retrieve(message.Content, userContext: null, topK);

                    // Build RAG prompt
                    // Using manual loop with dictionary lookup for deduplication (performance optimization, avoiding LINQ overhead)
                    var chunkStore = new Dictionary<string, Chunk>(topK);
                    foreach (var chunk in chunks)
                    {
                        if (!chunkStore.TryGetValue(chunk.ChunkId, out _))
                        {
                            chunkStore[chunk.ChunkId] = new Chunk
                            {
                                ChunkId = chunk.ChunkId,
                                DocId = chunk.DocId,
                                Text = chunk.Excerpt,
                                SourceUri = chunk.DocId,
                                Title = chunk.DocId
                            };
                        }
                    }

                    var composer = new PromptComposer(new RagRetrievalOptions { TopK = topK });
                    var ragPrompt = composer.ComposePrompt(message.Content, chunks, chunkStore);

                    // Build conversation context with RAG prompt
                    var tempHistory = new List<ChatMessage>(_conversationHistory);
                    tempHistory[tempHistory.Count - 1] = new ChatMessage
                    {
                        Role = ChatRole.User,
                        Content = ragPrompt
                    };
                    prompt = BuildPromptFromMessages(tempHistory);

                    // Extract citations (avoid LINQ for better performance)
                    if (_options.RagOptions?.IncludeCitations ?? true)
                    {
                        citations = new List<Citation>(chunks.Count);
                        for (int i = 0; i < chunks.Count; i++)
                        {
                            var c = chunks[i];
                            citations.Add(new Citation
                            {
                                Source = c.DocId,
                                Title = null, // RetrievedChunk doesn't include title metadata; would need pipeline API enhancement
                                Snippet = c.Excerpt,
                                RelevanceScore = c.Score
                            });
                        }
                    }
                }
                else
                {
                    // Apply overflow strategy and build conversation prompt
                    prompt = ApplyOverflowStrategyAndBuildPrompt(options.MaxNewTokens, ref warnings);
                }

                // Tokenize current prompt
                var promptTokenIds = _tokenizer.Encode(prompt).ToArray();

                // Calculate KV cache delta and determine prefill strategy
                SessionId sessionId = new SessionId(_sessionId);
                KvCacheEntry? kvCache = null;
                int startPosition = 0;
                int maxTokens = _options.MaxKvCacheTokens ?? _modelHandle.Model.BlockSize;

                if (_options.EnableKvCache)
                {
                    // Get or create KV cache entry
                    kvCache = _kvCacheStore.GetOrCreate(sessionId, _modelShape, maxTokens);

                    // Calculate longest common prefix (LCP) with last prompt
                    if (_lastPromptTokenIds != null && kvCache.CurrentTokenCount > 0)
                    {
                        int lcp = CalculateLongestCommonPrefix(_lastPromptTokenIds, promptTokenIds);

                        // If LCP matches cached count, we can use incremental prefill
                        if (lcp == _cachedTokenCount && lcp == kvCache.CurrentTokenCount)
                        {
                            startPosition = lcp;
                        }
                        else
                        {
                            // Cache mismatch or evicted - reset and do full prefill
                            kvCache.Reset();
                            startPosition = 0;
                            _cachedTokenCount = 0;

                            // Invalidate persistent session (Phase 2.1)
                            if (_persistentInferenceSession != null)
                            {
                                _persistentInferenceSession.Dispose();
                                _persistentInferenceSession = null;
                                _persistentSessionPosition = 0;
                            }
                        }
                    }
                }

                // Generate streaming response
                var session = _modelHandle.CreateInferenceSession(options, _engineOptions);
                try
                {
                    var responseBuilder = new StringBuilder();
                    int tokenCount = 0;

                    // Emit started event (with warnings if truncation occurred)
                    // Note: Using error field for informational warnings about truncation
                    yield return new TokenEvent(
                        kind: TokenEventKind.Started,
                        text: ReadOnlyMemory<char>.Empty,
                        tokenId: -1,
                        generatedTokens: 0,
                        isFinal: false,
                        error: warnings != null ? string.Join("; ", warnings) : null);

                    bool isLast = false;
                    IAsyncEnumerable<GeneratedToken> tokenStream;

                    if (startPosition > 0 && kvCache != null)
                    {
                        // Incremental prefill: only process new tokens
                        var deltaTokenIds = new int[promptTokenIds.Length - startPosition];
                        Array.Copy(promptTokenIds, startPosition, deltaTokenIds, 0, deltaTokenIds.Length);
                        var deltaPrompt = _tokenizer.Decode(new List<int>(deltaTokenIds));

                        tokenStream = session.GenerateStreamAsync(
                            prompt: deltaPrompt,
                            metrics: null,
                            cancellationToken: cancellationToken);
                    }
                    else
                    {
                        // Full prefill
                        tokenStream = session.GenerateStreamAsync(
                            prompt: prompt,
                            metrics: null,
                            cancellationToken: cancellationToken);
                    }

                    await foreach (var token in tokenStream)
                    {
                        tokenCount++;
                        responseBuilder.Append(token.Text);
                        isLast = (tokenCount >= options.MaxNewTokens);

                        yield return new TokenEvent(
                            kind: TokenEventKind.Token,
                            text: token.Text.AsMemory(),
                            tokenId: token.TokenId,
                            generatedTokens: tokenCount,
                            isFinal: isLast);

                        if (isLast)
                        {
                            break;
                        }
                    }

                    // Add complete response to history
                    var response = responseBuilder.ToString();
                    _conversationHistory.Add(new ChatMessage
                    {
                        Role = ChatRole.Assistant,
                        Content = response
                    });

                    _turnCount++;
                    _totalTokensGenerated += tokenCount;
                    _totalInferenceTimer.Stop();

                    // Update cache tracking
                    if (_options.EnableKvCache && kvCache != null)
                    {
                        _lastPromptTokenIds = promptTokenIds;
                        _cachedTokenCount = promptTokenIds.Length;
                        _kvCacheStore.Touch(sessionId);
                    }

                    // Emit completed event with citations if RAG was used
                    // NOTE: Using the error field to pass citation metadata for backward compatibility
                    // with existing TokenEvent struct. In future, consider adding dedicated Metadata field.
                    string? citationMetadata = null;
                    if (citations != null && citations.Count > 0)
                    {
                        // Simplified: just include source count
                        citationMetadata = $"Citations: {citations.Count} sources";
                    }

                    yield return new TokenEvent(
                        kind: TokenEventKind.Completed,
                        text: ReadOnlyMemory<char>.Empty,
                        tokenId: -1,
                        generatedTokens: tokenCount,
                        isFinal: true,
                        error: citationMetadata);
                }
                finally
                {
                    session.Dispose();
                }
            }
            finally
            {
                ReleaseSession();
            }
        }

        public void Reset()
        {
            ThrowIfDisposed();

            _conversationHistory.Clear();
            _turnCount = 0;
            _cachedTokenCount = 0;
            _lastPromptTokenIds = null;
            _lastTurnWasTruncated = false;

            // Dispose persistent session (Phase 2.1)
            if (_persistentInferenceSession != null)
            {
                _persistentInferenceSession.Dispose();
                _persistentInferenceSession = null;
                _persistentSessionPosition = 0;
            }

            // Remove from KV cache store
            if (_options.EnableKvCache)
            {
                SessionId sessionId = new SessionId(_sessionId);
                _kvCacheStore.Remove(sessionId);
            }
        }

        /// <summary>
        /// Gets diagnostic metrics for this chat session.
        /// </summary>
        public ChatSessionDiagnostics GetDiagnostics()
        {
            ThrowIfDisposed();

            double avgTokensPerSecond = 0;
            if (_totalInferenceTimer.Elapsed.TotalSeconds > 0)
            {
                avgTokensPerSecond = _totalTokensGenerated / _totalInferenceTimer.Elapsed.TotalSeconds;
            }

            return new ChatSessionDiagnostics
            {
                TotalTurns = _turnCount,
                TruncatedTurns = _truncatedTurns,
                KvCacheHits = _kvCacheHits,
                KvCacheMisses = _kvCacheMisses,
                NaNRecoveries = _nanRecoveries, // NaN detection not yet implemented in attention layers
                DegenerateOutputRecoveries = _degenerateOutputRecoveries,
                TotalTokensGenerated = _totalTokensGenerated,
                TotalTokensFromCache = _totalTokensFromCache,
                AverageTokensPerSecond = avgTokensPerSecond,
                TotalInferenceTime = _totalInferenceTimer.Elapsed
            };
        }

        /// <summary>
        /// Detects and cleans degenerate output (repetition, prompt leakage).
        /// Returns cleaned text and stop reason.
        /// </summary>
        private (string cleanedText, string stopReason) DetectAndCleanDegenerateOutput(string text, PublicGenerationOptions options)
        {
            if (string.IsNullOrEmpty(text))
                return (text, "completed");

            // Tokenize to check for repetition
            var tokens = _tokenizer.Encode(text).ToArray();

            // Check for degenerate repetition (last 20 tokens identical)
            if (tokens.Length >= 40)
            {
                bool isRepetitive = true;
                int checkLength = Math.Min(20, tokens.Length / 2);
                for (int i = 0; i < checkLength; i++)
                {
                    if (tokens[tokens.Length - checkLength + i] != tokens[tokens.Length - 2 * checkLength + i])
                    {
                        isRepetitive = false;
                        break;
                    }
                }

                if (isRepetitive)
                {
                    // Truncate to remove repetition
                    int truncateAt = tokens.Length - checkLength;
                    var cleanedTokens = tokens.Take(truncateAt).ToList();
                    return (_tokenizer.Decode(cleanedTokens), "degenerate_repetition");
                }
            }

            // Check for prompt leakage
            if (text.Contains("User:") || text.Contains("System:") || text.Contains("USER:") || text.Contains("SYSTEM:"))
            {
                // Find boundary and truncate
                int userIdx = text.IndexOf("User:", StringComparison.OrdinalIgnoreCase);
                int systemIdx = text.IndexOf("System:", StringComparison.OrdinalIgnoreCase);
                int truncateIdx = -1;

                if (userIdx >= 0 && systemIdx >= 0)
                    truncateIdx = Math.Min(userIdx, systemIdx);
                else if (userIdx >= 0)
                    truncateIdx = userIdx;
                else if (systemIdx >= 0)
                    truncateIdx = systemIdx;

                if (truncateIdx > 0)
                {
                    return (text.Substring(0, truncateIdx).TrimEnd(), "prompt_leakage");
                }
            }

            return (text, "completed");
        }

        /// <summary>
        /// Gets the current context budget for this session.
        /// </summary>
        public ContextBudget GetContextBudget()
        {
            ThrowIfDisposed();

            // Build current prompt to measure token count
            var prompt = BuildConversationPrompt(0); // 0 = no generation reserved
            var tokens = _tokenizer.Encode(prompt).Count;

            return new ContextBudget(
                maxContext: _modelHandle.Model.BlockSize,
                currentHistory: tokens,
                reserved: 0,
                turnCount: _turnCount
            );
        }

        /// <summary>
        /// Manually trims conversation history to a maximum number of turns.
        /// Preserves system messages, removes oldest user/assistant pairs.
        /// </summary>
        public void TrimHistory(int maxTurns)
        {
            ThrowIfDisposed();

            if (maxTurns < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxTurns), "maxTurns must be non-negative");
            }

            SeparateSystemAndConversationMessages(out var systemMessages, out var conversationTurns);

            // If we have more turns than maxTurns, remove oldest
            if (conversationTurns.Count > maxTurns)
            {
                int toRemove = conversationTurns.Count - maxTurns;
                conversationTurns.RemoveRange(0, toRemove);

                // Rebuild history
                _conversationHistory.Clear();
                _conversationHistory.AddRange(systemMessages);
                _conversationHistory.AddRange(conversationTurns);

                // Invalidate KV cache since we modified history
                InvalidateCache();
            }
        }

        /// <summary>
        /// Applies the configured context overflow strategy and builds the conversation prompt.
        /// Returns the prompt and any warnings generated.
        /// </summary>
        private string ApplyOverflowStrategyAndBuildPrompt(int maxNewTokens, ref List<string>? warnings)
        {
            int maxAllowedPromptTokens = _modelHandle.Model.BlockSize - maxNewTokens;

            switch (_options.ContextOverflowStrategy)
            {
                case ContextOverflowStrategy.TruncateOldest:
                    return ApplyTruncateOldestStrategy(maxAllowedPromptTokens, ref warnings);

                case ContextOverflowStrategy.SlidingWindow:
                    return ApplySlidingWindowStrategy(maxAllowedPromptTokens, ref warnings);

                case ContextOverflowStrategy.Error:
                    return ApplyErrorStrategy(maxAllowedPromptTokens);

                default:
                    return ApplyTruncateOldestStrategy(maxAllowedPromptTokens, ref warnings);
            }
        }

        /// <summary>
        /// TruncateOldest strategy: Remove oldest non-system turns one at a time until prompt fits.
        /// </summary>
        private string ApplyTruncateOldestStrategy(int maxAllowedPromptTokens, ref List<string>? warnings)
        {
            SeparateSystemAndConversationMessages(out var systemMessages, out var conversationTurns);

            // Try full conversation first
            var workingHistory = new List<ChatMessage>(_conversationHistory);
            string prompt = BuildPromptFromMessages(workingHistory);
            int tokenCount = _tokenizer.Encode(prompt).Count;

            int removedCount = 0;

            // Remove oldest turns until we fit (keep at least 1 turn - the current user message)
            while (tokenCount > maxAllowedPromptTokens && conversationTurns.Count > 1)
            {
                conversationTurns.RemoveAt(0);
                removedCount++;

                // Rebuild and recount
                workingHistory.Clear();
                workingHistory.AddRange(systemMessages);
                workingHistory.AddRange(conversationTurns);
                prompt = BuildPromptFromMessages(workingHistory);
                tokenCount = _tokenizer.Encode(prompt).Count;
            }

            // If still doesn't fit with only system + current message, throw exception
            if (tokenCount > maxAllowedPromptTokens)
            {
                ThrowContextLimitExceeded(systemMessages, tokenCount, maxAllowedPromptTokens);
            }

            // Handle truncation if needed
            if (removedCount > 0)
            {
                RecordTruncationForOldestStrategy(removedCount, ref warnings);
            }
            else
            {
                _lastTurnWasTruncated = false;
            }

            return prompt;
        }

        /// <summary>
        /// SlidingWindow strategy: Keep system + last N turns that fit using binary search.
        /// </summary>
        private string ApplySlidingWindowStrategy(int maxAllowedPromptTokens, ref List<string>? warnings)
        {
            SeparateSystemAndConversationMessages(out var systemMessages, out var conversationTurns);

            // Binary search for optimal number of recent turns to keep
            int left = 1; // Must keep at least current message
            int right = conversationTurns.Count;
            int bestN = 1;
            string bestPrompt = "";

            while (left <= right)
            {
                int mid = (left + right) / 2;

                // Build prompt with last 'mid' turns
                var candidateHistory = new List<ChatMessage>();
                candidateHistory.AddRange(systemMessages);
                candidateHistory.AddRange(conversationTurns.Skip(conversationTurns.Count - mid));

                string candidatePrompt = BuildPromptFromMessages(candidateHistory);
                int tokenCount = _tokenizer.Encode(candidatePrompt).Count;

                if (tokenCount <= maxAllowedPromptTokens)
                {
                    // This fits, try to include more
                    bestN = mid;
                    bestPrompt = candidatePrompt;
                    left = mid + 1;
                }
                else
                {
                    // Too many tokens, try fewer turns
                    right = mid - 1;
                }
            }

            // Check if we had to truncate
            int removedCount = conversationTurns.Count - bestN;

            if (removedCount > 0)
            {
                RecordTruncationForSlidingWindow(removedCount, ref warnings);
            }
            else
            {
                _lastTurnWasTruncated = false;
            }

            // Verify the best prompt still fits (edge case: even 1 turn is too large)
            if (bestN == 0 || string.IsNullOrEmpty(bestPrompt))
            {
                bestPrompt = ValidateMinimalPromptFits(systemMessages, conversationTurns, maxAllowedPromptTokens);
            }

            return bestPrompt;
        }

        /// <summary>
        /// Validates that even a minimal prompt (system + current message) fits within limits.
        /// </summary>
        private string ValidateMinimalPromptFits(
            List<ChatMessage> systemMessages,
            List<ChatMessage> conversationTurns,
            int maxAllowedPromptTokens)
        {
            var minimalHistory = new List<ChatMessage>();
            minimalHistory.AddRange(systemMessages);
            if (conversationTurns.Count > 0)
            {
                minimalHistory.Add(conversationTurns[conversationTurns.Count - 1]);
            }
            
            string prompt = BuildPromptFromMessages(minimalHistory);
            int tokenCount = _tokenizer.Encode(prompt).Count;
            
            if (tokenCount > maxAllowedPromptTokens)
            {
                ThrowContextLimitExceeded(systemMessages, tokenCount, maxAllowedPromptTokens);
            }

            return prompt;
        }

        /// <summary>
        /// Error strategy: Throw exception immediately if context is exceeded.
        /// </summary>
        private string ApplyErrorStrategy(int maxAllowedPromptTokens)
        {
            string prompt = BuildPromptFromMessages(_conversationHistory);
            int totalTokens = _tokenizer.Encode(prompt).Count;

            if (totalTokens > maxAllowedPromptTokens)
            {
                // Build diagnostic breakdown
                var breakdown = new StringBuilder();
                breakdown.AppendLine($"Conversation ({totalTokens} tokens) exceeds context window ({_modelHandle.Model.BlockSize} tokens). Per-turn breakdown:");

                foreach (var msg in _conversationHistory)
                {
                    var msgList = new List<ChatMessage> { msg };
                    var msgPrompt = BuildPromptFromMessages(msgList);
                    int msgTokens = _tokenizer.Encode(msgPrompt).Count;
                    breakdown.AppendLine($"  - {msg.Role}: {msgTokens} tokens");
                }

                throw new ContextLimitExceededException(
                    message: breakdown.ToString().TrimEnd(),
                    totalTokens: totalTokens,
                    contextLimit: _modelHandle.Model.BlockSize
                );
            }

            _lastTurnWasTruncated = false;
            return prompt;
        }

        /// <summary>
        /// Builds a prompt from a list of messages (helper for overflow strategies).
        /// </summary>
        private string BuildPromptFromMessages(List<ChatMessage> messages)
        {
            if (_templateType == ChatTemplateType.None)
            {
                return BuildPlainPromptFromMessages(messages);
            }
            else
            {
                return BuildTemplatePromptFromMessages(messages);
            }
        }

        /// <summary>
        /// Appends assistant prefix to StringBuilder if needed (last message is not assistant).
        /// </summary>
        private void AppendAssistantPrefixIfNeeded(StringBuilder sb, List<ChatMessage> messages)
        {
            if (messages.Count == 0 || messages[messages.Count - 1].Role != ChatRole.Assistant)
            {
                sb.Append(_templateType == ChatTemplateType.None ? "Assistant:" : GetAssistantPrefix());
            }
        }

        /// <summary>
        /// Build plain text prompt from messages.
        /// </summary>
        private string BuildPlainPromptFromMessages(List<ChatMessage> messages)
        {
            var sb = new StringBuilder();

            foreach (var msg in messages)
            {
                if (msg.Role == ChatRole.System)
                {
                    sb.AppendLine($"System: {msg.Content}");
                }
                else
                {
                    string rolePrefix = msg.Role == ChatRole.User ? "User: " : "Assistant: ";
                    sb.AppendLine($"{rolePrefix}{msg.Content}");
                }
            }

            AppendAssistantPrefixIfNeeded(sb, messages);
            return sb.ToString();
        }

        /// <summary>
        /// Build templated prompt from messages.
        /// </summary>
        private string BuildTemplatePromptFromMessages(List<ChatMessage> messages)
        {
            var sb = new StringBuilder();

            foreach (var msg in messages)
            {
                sb.Append(FormatMessageForTemplate(msg));
            }

            AppendAssistantPrefixIfNeeded(sb, messages);
            return sb.ToString();
        }

        /// <summary>
        /// Builds the conversation prompt with intelligent truncation.
        /// Applies chat template formatting and ensures the prompt fits within context limits.
        /// </summary>
        private string BuildConversationPrompt(int maxNewTokens)
        {
            int contextLimit = _modelHandle.Model.BlockSize - maxNewTokens;

            // For templates that don't do individual message formatting,
            // we need to build the full conversation differently
            if (_templateType == ChatTemplateType.None)
            {
                return BuildPlainPromptWithTruncation(contextLimit);
            }

            return BuildTemplatePromptWithTruncation(contextLimit);
        }

        /// <summary>
        /// Build plain text prompt (no template) with truncation.
        /// </summary>
        private string BuildPlainPromptWithTruncation(int contextLimit)
        {
            var sb = new StringBuilder();

            // First, separate system messages from conversation turns
            var systemMessages = new List<ChatMessage>();
            var conversationTurns = new List<ChatMessage>();

            foreach (var msg in _conversationHistory)
            {
                if (msg.Role == ChatRole.System)
                {
                    systemMessages.Add(msg);
                }
                else
                {
                    conversationTurns.Add(msg);
                }
            }

            // Build system prompt (always included)
            foreach (var msg in systemMessages)
            {
                sb.AppendLine($"System: {msg.Content}");
            }

            string systemPrompt = sb.ToString();
            int systemTokens = _tokenizer.Encode(systemPrompt).Count;

            // Build conversation history with truncation
            var turnStrings = new List<string>();
            foreach (var msg in conversationTurns)
            {
                string rolePrefix = msg.Role == ChatRole.User ? "User: " : "Assistant: ";
                turnStrings.Add($"{rolePrefix}{msg.Content}");
            }

            // Add turns from most recent, working backwards until we hit the limit
            var includedTurns = new List<string>();
            int currentTokens = systemTokens;

            // Calculate assistant prefix tokens once
            int assistantPrefixTokens = _tokenizer.Encode("Assistant:").Count;

            for (int i = turnStrings.Count - 1; i >= 0; i--)
            {
                string turn = turnStrings[i];
                int turnTokens = _tokenizer.Encode(turn + "\n").Count;

                if (currentTokens + turnTokens + assistantPrefixTokens > contextLimit)
                {
                    break;
                }

                includedTurns.Insert(0, turn);
                currentTokens += turnTokens;
            }

            // Build final prompt
            sb.Clear();
            sb.Append(systemPrompt);
            foreach (var turn in includedTurns)
            {
                sb.AppendLine(turn);
            }
            sb.Append("Assistant:");

            return sb.ToString();
        }

        /// <summary>
        /// Build templated prompt with truncation.
        /// </summary>
        private string BuildTemplatePromptWithTruncation(int contextLimit)
        {
            // Separate system messages from conversation turns
            var systemMessages = new List<ChatMessage>();
            var conversationTurns = new List<ChatMessage>();

            foreach (var msg in _conversationHistory)
            {
                if (msg.Role == ChatRole.System)
                {
                    systemMessages.Add(msg);
                }
                else
                {
                    conversationTurns.Add(msg);
                }
            }

            // Build and measure system messages (always included)
            var sb = new StringBuilder();
            foreach (var msg in systemMessages)
            {
                sb.Append(FormatMessageForTemplate(msg));
            }

            string systemPrompt = sb.ToString();
            int systemTokens = _tokenizer.Encode(systemPrompt).Count;

            // Build conversation turns with truncation
            var includedTurns = new List<string>();
            int currentTokens = systemTokens;

            // Process turns from most recent backwards
            for (int i = conversationTurns.Count - 1; i >= 0; i--)
            {
                var msg = conversationTurns[i];
                string formattedTurn = FormatMessageForTemplate(msg);
                int turnTokens = _tokenizer.Encode(formattedTurn).Count;

                // Reserve space for assistant prefix (varies by template)
                string assistantPrefix = GetAssistantPrefix();
                int prefixTokens = _tokenizer.Encode(assistantPrefix).Count;

                if (currentTokens + turnTokens + prefixTokens > contextLimit)
                {
                    break;
                }

                includedTurns.Insert(0, formattedTurn);
                currentTokens += turnTokens;
            }

            // Build final prompt
            sb.Clear();
            sb.Append(systemPrompt);
            foreach (var turn in includedTurns)
            {
                sb.Append(turn);
            }
            sb.Append(GetAssistantPrefix());

            return sb.ToString();
        }

        /// <summary>
        /// Format a single message according to the template.
        /// </summary>
        private string FormatMessageForTemplate(ChatMessage msg)
        {
            switch (_templateType)
            {
                case ChatTemplateType.ChatML:
                    return FormatChatMLMessage(msg);
                case ChatTemplateType.Llama2:
                    return FormatLlama2Message(msg);
                case ChatTemplateType.Llama3:
                    return FormatLlama3Message(msg);
                case ChatTemplateType.Mistral:
                    return FormatMistralMessage(msg);
                case ChatTemplateType.Phi:
                    return FormatPhiMessage(msg);
                default:
                    return msg.Content;
            }
        }

        private string FormatChatMLMessage(ChatMessage msg)
        {
            string role = msg.Role switch
            {
                ChatRole.System => "system",
                ChatRole.User => "user",
                ChatRole.Assistant => "assistant",
                _ => "user"
            };
            return $"<|im_start|>{role}\n{msg.Content}<|im_end|>\n";
        }

        private string FormatLlama2Message(ChatMessage msg)
        {
            if (msg.Role == ChatRole.System)
            {
                return $"<<SYS>>\n{msg.Content}\n<</SYS>>\n\n";
            }
            else if (msg.Role == ChatRole.User)
            {
                return $"[INST] {msg.Content} [/INST] ";
            }
            else // Assistant
            {
                return msg.Content + " ";
            }
        }

        private string FormatLlama3Message(ChatMessage msg)
        {
            string role = msg.Role switch
            {
                ChatRole.System => "system",
                ChatRole.User => "user",
                ChatRole.Assistant => "assistant",
                _ => "user"
            };
            return $"<|start_header_id|>{role}<|end_header_id|>\n\n{msg.Content}<|eot_id|>";
        }

        private string FormatMistralMessage(ChatMessage msg)
        {
            if (msg.Role == ChatRole.System)
            {
                return $"{msg.Content}\n\n";
            }
            else if (msg.Role == ChatRole.User)
            {
                return $"[INST] {msg.Content} [/INST]";
            }
            else // Assistant
            {
                return msg.Content + " ";
            }
        }

        private string FormatPhiMessage(ChatMessage msg)
        {
            string role = msg.Role switch
            {
                ChatRole.System => "System",
                ChatRole.User => "User",
                ChatRole.Assistant => "Assistant",
                _ => "User"
            };
            return $"{role}: {msg.Content}\n";
        }

        /// <summary>
        /// Get the assistant prefix for the current template.
        /// </summary>
        private string GetAssistantPrefix()
        {
            return _templateType switch
            {
                ChatTemplateType.ChatML => "<|im_start|>assistant\n",
                ChatTemplateType.Llama2 => "",
                ChatTemplateType.Llama3 => "<|start_header_id|>assistant<|end_header_id|>\n\n",
                ChatTemplateType.Mistral => "",
                ChatTemplateType.Phi => "Assistant:",
                _ => "Assistant:"
            };
        }

        /// <summary>
        /// Calculates the longest common prefix between two token arrays.
        /// Used for delta calculation in incremental prefill.
        /// </summary>
        private int CalculateLongestCommonPrefix(int[] previous, int[] current)
        {
            int minLength = Math.Min(previous.Length, current.Length);
            int lcp = 0;

            for (int i = 0; i < minLength; i++)
            {
                if (previous[i] != current[i])
                {
                    break;
                }
                lcp++;
            }

            return lcp;
        }

        // ============================================================================
        // LEVEL 3 CHAT API - ChatRequest/ChatResponse support
        // ============================================================================

        /// <summary>
        /// Sends a chat request with Level 3 features (messages, tools, format, telemetry).
        /// </summary>
        public async ValueTask<ChatResponse> SendAsync(
            ChatRequest request,
            IChatTelemetry? telemetry = null,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (request == null)
                throw new ArgumentNullException(nameof(request));
            if (request.Messages == null || request.Messages.Count == 0)
                throw new ArgumentException("Request must contain at least one message", nameof(request));

            telemetry ??= IChatTelemetry.Default;

            var sw = Stopwatch.StartNew();
            telemetry.OnRequestStart(_sessionId, request.Messages.Count);

            // Apply context policy if configured
            var messages = request.Messages;
            if (request.ContextPolicy != null)
            {
                var tokenCounter = new TokenizerAdapter(_tokenizer);
                int originalTokens = CountTokensInMessages(messages, tokenCounter);
                int maxTokens = request.Options?.MaxContextTokens ?? _modelHandle.Model.BlockSize;

                messages = request.ContextPolicy.Apply(messages, maxTokens, tokenCounter);

                int finalTokens = CountTokensInMessages(messages, tokenCounter);
                telemetry.OnContextPolicyApplied(
                    _sessionId,
                    request.ContextPolicy.GetType().Name,
                    originalTokens,
                    finalTokens);
            }

            // Convert Level 3 messages to legacy format for existing logic
            var legacyMessages = ConvertToLegacyMessages(messages);

            // Build prompt from messages
            var prompt = BuildPromptFromMessages(legacyMessages);

            // Convert options
            var options = request.Options ?? new PublicGenerationOptions();

            // Use existing generation logic
            var promptTokenIds = _tokenizer.Encode(prompt).ToArray();

            // Get KV cache if enabled
            SessionId sessionId = new SessionId(_sessionId);
            KvCacheEntry? kvCache = null;
            int startPosition = 0;

            if (_options.EnableKvCache)
            {
                int maxTokens = _options.MaxKvCacheTokens ?? _modelHandle.Model.BlockSize;
                kvCache = _kvCacheStore.GetOrCreate(sessionId, _modelShape, maxTokens);

                bool cacheHit = false;
                if (_lastPromptTokenIds != null && kvCache.CurrentTokenCount > 0)
                {
                    int lcp = CalculateLongestCommonPrefix(_lastPromptTokenIds, promptTokenIds);
                    if (lcp == _cachedTokenCount && lcp == kvCache.CurrentTokenCount)
                    {
                        startPosition = lcp;
                        cacheHit = true;
                        _kvCacheHits++;
                    }
                    else
                    {
                        kvCache.Reset();
                        _kvCacheMisses++;
                    }
                }
                else
                {
                    _kvCacheMisses++;
                }

                telemetry.OnKvCacheAccess(_sessionId, cacheHit, startPosition);
            }

            // Create or reuse inference session
            if (_persistentInferenceSession == null)
            {
                _persistentInferenceSession = _modelHandle.CreateInferenceSession(options, _engineOptions);
            }

            double? ttftMs = null;
            int completionTokens = 0;

            try
            {
                // Generate response
                string responseText = await _persistentInferenceSession.GenerateAsync(
                    prompt: prompt,
                    metrics: null,
                    cancellationToken: cancellationToken);

                ttftMs = sw.Elapsed.TotalMilliseconds;
                telemetry.OnFirstToken(_sessionId, ttftMs.Value);

                // Clean degenerate output
                var (cleanedResponse, stopReason) = DetectAndCleanDegenerateOutput(responseText, options);

                // Estimate completion tokens
                completionTokens = _tokenizer.Encode(cleanedResponse).Count;

                // Update cache tracking
                if (_options.EnableKvCache && kvCache != null)
                {
                    _lastPromptTokenIds = promptTokenIds;
                    _cachedTokenCount = promptTokenIds.Length;
                    _kvCacheStore.Touch(sessionId);
                }

                _turnCount++;
                sw.Stop();

                // Build response
                var usage = new UsageStats
                {
                    PromptTokens = promptTokenIds.Length,
                    CompletionTokens = completionTokens,
                    TimeToFirstTokenMs = ttftMs.Value,
                    TokensPerSecond = completionTokens / (sw.Elapsed.TotalSeconds > 0 ? sw.Elapsed.TotalSeconds : 1)
                };

                telemetry.OnRequestComplete(_sessionId, usage);

                return new ChatResponse
                {
                    Message = new ChatMessageV3
                    {
                        Role = ChatRole.Assistant,
                        Content = cleanedResponse
                    },
                    FinishReason = stopReason,
                    Usage = usage,
                    Citations = null, // Citations not available in streaming mode (SendAsync context only)
                    Warnings = null
                };
            }
            catch (Exception ex)
            {
                throw new SmallMindException($"Chat request failed: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Converts Level 3 messages to legacy ChatMessage format.
        /// </summary>
        private List<ChatMessage> ConvertToLegacyMessages(IReadOnlyList<ChatMessageV3> messages)
        {
            var legacy = new List<ChatMessage>();
            foreach (var msg in messages)
            {
                // Skip tool messages for now (not supported in legacy format)
                if (msg.Role == ChatRole.Tool)
                    continue;

                legacy.Add(new ChatMessage
                {
                    Role = msg.Role,
                    Content = msg.Content
                });
            }
            return legacy;
        }

        /// <summary>
        /// Counts total tokens in a list of messages.
        /// </summary>
        private int CountTokensInMessages(IReadOnlyList<ChatMessageV3> messages, ITokenCounter tokenizer)
        {
            int total = 0;
            foreach (var msg in messages)
            {
                total += tokenizer.CountTokens(msg.Content);
            }
            return total;
        }

        /// <summary>
        /// Separates system messages from conversation turns.
        /// </summary>
        private void SeparateSystemAndConversationMessages(
            out List<ChatMessage> systemMessages, 
            out List<ChatMessage> conversationTurns)
        {
            systemMessages = new List<ChatMessage>();
            conversationTurns = new List<ChatMessage>();

            foreach (var msg in _conversationHistory)
            {
                if (msg.Role == ChatRole.System)
                {
                    systemMessages.Add(msg);
                }
                else
                {
                    conversationTurns.Add(msg);
                }
            }
        }

        /// <summary>
        /// Invalidates KV cache and persistent session.
        /// </summary>
        private void InvalidateCache()
        {
            if (_options.EnableKvCache)
            {
                SessionId sessionId = new SessionId(_sessionId);
                _kvCacheStore.Remove(sessionId);
                _cachedTokenCount = 0;
                _lastPromptTokenIds = null;

                if (_persistentInferenceSession != null)
                {
                    _persistentInferenceSession.Dispose();
                    _persistentInferenceSession = null;
                    _persistentSessionPosition = 0;
                }
            }
        }

        /// <summary>
        /// Invalidates KV cache if there is cached content.
        /// </summary>
        private void InvalidateCacheIfPopulated()
        {
            if (_options.EnableKvCache && _cachedTokenCount > 0)
            {
                SessionId sessionId = new SessionId(_sessionId);
                _kvCacheStore.Remove(sessionId);
                _cachedTokenCount = 0;
                _lastPromptTokenIds = null;

                if (_persistentInferenceSession != null)
                {
                    _persistentInferenceSession.Dispose();
                    _persistentInferenceSession = null;
                    _persistentSessionPosition = 0;
                }
            }
        }

        /// <summary>
        /// Records truncation warning and invalidates cache for TruncateOldest strategy.
        /// </summary>
        private void RecordTruncationForOldestStrategy(int removedCount, ref List<string>? warnings)
        {
            _lastTurnWasTruncated = true;
            _truncatedTurns++; // Track for diagnostics

            if (warnings == null)
            {
                warnings = new List<string>();
            }
            warnings.Add($"Context truncated: removed {removedCount} oldest turns to fit within {_modelHandle.Model.BlockSize} token limit");

            InvalidateCacheIfPopulated();
        }

        /// <summary>
        /// Records truncation warning and invalidates cache for SlidingWindow strategy.
        /// </summary>
        private void RecordTruncationForSlidingWindow(int removedCount, ref List<string>? warnings)
        {
            _lastTurnWasTruncated = true;

            if (warnings == null)
            {
                warnings = new List<string>();
            }
            warnings.Add($"Context truncated: removed {removedCount} oldest turns to fit within {_modelHandle.Model.BlockSize} token limit");

            InvalidateCacheIfPopulated();
        }

        /// <summary>
        /// Throws ContextLimitExceededException when system + current message exceeds limit.
        /// </summary>
        private void ThrowContextLimitExceeded(
            List<ChatMessage> systemMessages,
            int tokenCount,
            int maxAllowedPromptTokens)
        {
            var systemPrompt = BuildPromptFromMessages(systemMessages);
            int systemTokens = systemMessages.Count > 0 ? _tokenizer.Encode(systemPrompt).Count : 0;
            int messageTokens = tokenCount - systemTokens;

            throw new ContextLimitExceededException(
                message: $"System prompt ({systemTokens} tokens) + current message ({messageTokens} tokens) exceeds context window ({_modelHandle.Model.BlockSize} tokens). Reduce your system prompt or message length.",
                totalTokens: tokenCount,
                contextLimit: _modelHandle.Model.BlockSize,
                systemTokens: systemTokens,
                messageTokens: messageTokens
            );
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(ChatSession));
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            // Dispose persistent session (Phase 2.1)
            if (_persistentInferenceSession != null)
            {
                _persistentInferenceSession.Dispose();
                _persistentInferenceSession = null;
            }

            // Remove from KV cache store on disposal
            if (_options.EnableKvCache)
            {
                SessionId sessionId = new SessionId(_sessionId);
                _kvCacheStore.Remove(sessionId);
            }

            _disposed = true;
        }
    }
}
