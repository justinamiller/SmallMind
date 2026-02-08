using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SmallMind.Abstractions;
using SmallMind.Runtime;
using SmallMind.Runtime.Cache;
using SmallMind.Tokenizers;
using PublicGenerationOptions = SmallMind.Abstractions.GenerationOptions;

namespace SmallMind.Engine
{
    /// <summary>
    /// Internal implementation of IChatSession.
    /// Maintains conversation context and KV cache across multiple turns.
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
        private int _turnCount;
        private int _cachedTokenCount; // Actual cached token count from KV cache
        private int[]? _lastPromptTokenIds; // Last tokenized prompt for delta calculation
        private bool _disposed;

        public ChatSession(
            ModelHandle modelHandle,
            ChatSessionOptions options,
            SmallMindOptions engineOptions)
        {
            _modelHandle = modelHandle ?? throw new ArgumentNullException(nameof(modelHandle));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _engineOptions = engineOptions ?? throw new ArgumentNullException(nameof(engineOptions));
            _tokenizer = modelHandle.Tokenizer ?? throw new InvalidOperationException("Model handle does not have a tokenizer");
            _conversationHistory = new List<ChatMessage>();
            _sessionId = options.SessionId ?? Guid.NewGuid().ToString("N");
            _createdAt = DateTimeOffset.UtcNow;

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

        public async ValueTask<GenerationResult> SendAsync(
            ChatMessage message,
            PublicGenerationOptions options,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            // Add user message to history
            _conversationHistory.Add(message);

            // Build full conversation prompt with truncation
            var prompt = BuildConversationPrompt(options.MaxNewTokens);

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
                    }
                }
            }

            // Generate response
            var session = _modelHandle.CreateInferenceSession(options, _engineOptions);
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
                        cancellationToken: cancellationToken);
                }
                else
                {
                    // Full prefill
                    response = await session.GenerateAsync(
                        prompt: prompt,
                        metrics: null,
                        cancellationToken: cancellationToken);
                }

                // Add assistant response to history
                _conversationHistory.Add(new ChatMessage
                {
                    Role = ChatRole.Assistant,
                    Content = response
                });

                _turnCount++;

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
                    StopReason = "completed"
                };
            }
            finally
            {
                session.Dispose();
            }
        }

        public async IAsyncEnumerable<TokenEvent> SendStreamingAsync(
            ChatMessage message,
            PublicGenerationOptions options,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            // Add user message to history
            _conversationHistory.Add(message);

            // Build full conversation prompt with truncation
            var prompt = BuildConversationPrompt(options.MaxNewTokens);

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
                    }
                }
            }

            // Generate streaming response
            var session = _modelHandle.CreateInferenceSession(options, _engineOptions);
            try
            {
                var responseBuilder = new StringBuilder();
                int tokenCount = 0;

                // Emit started event
                yield return new TokenEvent(
                    kind: TokenEventKind.Started,
                    text: ReadOnlyMemory<char>.Empty,
                    tokenId: -1,
                    generatedTokens: 0,
                    isFinal: false);

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

                // Update cache tracking
                if (_options.EnableKvCache && kvCache != null)
                {
                    _lastPromptTokenIds = promptTokenIds;
                    _cachedTokenCount = promptTokenIds.Length;
                    _kvCacheStore.Touch(sessionId);
                }

                // Emit completed event
                yield return new TokenEvent(
                    kind: TokenEventKind.Completed,
                    text: ReadOnlyMemory<char>.Empty,
                    tokenId: -1,
                    generatedTokens: tokenCount,
                    isFinal: true);
            }
            finally
            {
                session.Dispose();
            }
        }

        public void Reset()
        {
            ThrowIfDisposed();

            _conversationHistory.Clear();
            _turnCount = 0;
            _cachedTokenCount = 0;
            _lastPromptTokenIds = null;

            // Remove from KV cache store
            if (_options.EnableKvCache)
            {
                SessionId sessionId = new SessionId(_sessionId);
                _kvCacheStore.Remove(sessionId);
            }
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
