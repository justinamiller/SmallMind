using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SmallMind.Core;
using SmallMind.Retrieval;
using SmallMind.Text;
using SmallMind.Workflows;

namespace SmallMind.Chat
{
    /// <summary>
    /// Orchestrates multi-turn RAG chat sessions with workflow integration.
    /// </summary>
    public class ChatOrchestrator : IChatCompletionService
    {
        private readonly ISessionStore _sessionStore;
        private readonly IRetrievalIndex? _retrievalIndex;
        private readonly TransformerModel _model;
        private readonly ITokenizer _tokenizer;
        private readonly int _blockSize;
        private readonly ILogger<ChatOrchestrator>? _logger;
        private readonly Sampling _sampling;

        /// <summary>
        /// Create a new chat orchestrator.
        /// </summary>
        /// <param name="sessionStore">Session storage.</param>
        /// <param name="model">Language model.</param>
        /// <param name="tokenizer">Tokenizer.</param>
        /// <param name="blockSize">Model block size.</param>
        /// <param name="retrievalIndex">Optional retrieval index for RAG.</param>
        /// <param name="logger">Optional logger.</param>
        public ChatOrchestrator(
            ISessionStore sessionStore,
            TransformerModel model,
            ITokenizer tokenizer,
            int blockSize,
            IRetrievalIndex? retrievalIndex = null,
            ILogger<ChatOrchestrator>? logger = null)
        {
            _sessionStore = sessionStore ?? throw new ArgumentNullException(nameof(sessionStore));
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _tokenizer = tokenizer ?? throw new ArgumentNullException(nameof(tokenizer));
            _blockSize = blockSize;
            _retrievalIndex = retrievalIndex;
            _logger = logger;
            _sampling = new Sampling(_model, _tokenizer, _blockSize);
        }

        /// <summary>
        /// Process a user message and generate a response.
        /// </summary>
        public async Task<ChatResponse> AskAsync(
            string sessionId,
            string message,
            ChatOptions options,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(sessionId))
                throw new ArgumentException("Session ID cannot be null or empty", nameof(sessionId));
            if (string.IsNullOrEmpty(message))
                throw new ArgumentException("Message cannot be null or empty", nameof(message));
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            _logger?.LogInformation("Processing message for session {SessionId}", sessionId);

            // Get or create session
            var session = await GetOrCreateSessionAsync(sessionId, cancellationToken);

            var response = new ChatResponse
            {
                SessionId = sessionId
            };

            try
            {
                // Store user message in state
                session.State.Set("current_user_message", message);

                string assistantMessage;
                List<RetrievedChunkWithCitation> retrievedChunks = new();

                // Perform RAG if enabled and index is available
                if (options.UseRag && _retrievalIndex != null)
                {
                    _logger?.LogDebug("Performing retrieval for query: {Query}", message);

                    var retrievalOptions = new RetrievalOptions
                    {
                        TopK = options.TopKRetrieval,
                        Deterministic = options.Deterministic,
                        IncludeSnippets = true,
                        MaxSnippetChars = 280
                    };

                    var retrievalResult = _retrievalIndex.Search(message, retrievalOptions, cancellationToken);
                    retrievedChunks = retrievalResult.Chunks;

                    _logger?.LogDebug("Retrieved {Count} chunks", retrievedChunks.Count);

                    // Build RAG prompt
                    var promptOptions = new RagPromptOptions
                    {
                        MaxContextChars = options.MaxContextChars,
                        MaxChunksToInclude = options.TopKRetrieval,
                        IncludeSourcesSection = options.ReturnCitations
                    };

                    var ragPrompt = RagPromptBuilder.Build(message, retrievedChunks, promptOptions);

                    // Generate response
                    assistantMessage = await GenerateResponseAsync(
                        ragPrompt,
                        options,
                        cancellationToken);

                    // Extract citations if requested
                    if (options.ReturnCitations && retrievedChunks.Count > 0)
                    {
                        response.Citations = ExtractCitations(retrievedChunks);
                    }
                }
                else
                {
                    // Non-RAG: build prompt from conversation history
                    var prompt = BuildConversationPrompt(session, message);

                    // Generate response
                    assistantMessage = await GenerateResponseAsync(
                        prompt,
                        options,
                        cancellationToken);
                }

                // Clean up the response (remove prompt if present)
                assistantMessage = CleanResponse(assistantMessage);

                response.Text = assistantMessage;

                // Add turn to session
                var turn = new ChatTurn
                {
                    UserMessage = message,
                    AssistantMessage = assistantMessage,
                    Timestamp = DateTime.UtcNow,
                    Citations = response.Citations
                };

                session.Turns.Add(turn);
                session.LastUpdatedAt = DateTime.UtcNow;

                // Save session
                await _sessionStore.UpsertAsync(session, cancellationToken);

                _logger?.LogInformation("Completed message for session {SessionId}", sessionId);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error processing message for session {SessionId}", sessionId);
                throw;
            }

            return response;
        }

        /// <summary>
        /// Get an existing session or create a new one.
        /// </summary>
        private async Task<ChatSession> GetOrCreateSessionAsync(
            string sessionId,
            CancellationToken cancellationToken)
        {
            var session = await _sessionStore.GetAsync(sessionId, cancellationToken);

            if (session == null)
            {
                session = new ChatSession
                {
                    SessionId = sessionId,
                    CreatedAt = DateTime.UtcNow,
                    LastUpdatedAt = DateTime.UtcNow
                };

                await _sessionStore.UpsertAsync(session, cancellationToken);
            }

            return session;
        }

        /// <summary>
        /// Build a conversation prompt from session history.
        /// </summary>
        private string BuildConversationPrompt(ChatSession session, string currentMessage)
        {
            var prompt = new System.Text.StringBuilder();

            // Include recent turns for context (last 3 turns)
            int startIndex = Math.Max(0, session.Turns.Count - 3);
            int count = session.Turns.Count - startIndex;

            if (count > 0)
            {
                prompt.AppendLine("=== CONVERSATION HISTORY ===");
                for (int i = startIndex; i < session.Turns.Count; i++)
                {
                    var turn = session.Turns[i];
                    prompt.AppendLine($"User: {turn.UserMessage}");
                    prompt.AppendLine($"Assistant: {turn.AssistantMessage}");
                    prompt.AppendLine();
                }
            }

            prompt.AppendLine("=== CURRENT MESSAGE ===");
            prompt.AppendLine($"User: {currentMessage}");
            prompt.AppendLine("Assistant:");

            return prompt.ToString();
        }

        /// <summary>
        /// Generate a response using the model.
        /// </summary>
        private async Task<string> GenerateResponseAsync(
            string prompt,
            ChatOptions options,
            CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                return _sampling.Generate(
                    prompt,
                    options.MaxTokens,
                    options.Temperature,
                    options.TopK,
                    seed: options.Deterministic ? options.Seed : null,
                    showPerf: false,
                    isPerfJsonMode: false);
            }, cancellationToken);
        }

        /// <summary>
        /// Extract citations from retrieved chunks.
        /// </summary>
        private List<string> ExtractCitations(List<RetrievedChunkWithCitation> chunks)
        {
            var citations = new List<string>();
            var seenSources = new HashSet<string>();

            foreach (var chunk in chunks)
            {
                var title = chunk.Citation.Title ?? "Unknown";
                var uri = chunk.Citation.SourceUri ?? "";

                var citation = string.IsNullOrEmpty(uri)
                    ? title
                    : $"{title} ({uri})";

                if (seenSources.Add(citation))
                {
                    citations.Add(citation);
                }
            }

            return citations;
        }

        /// <summary>
        /// Clean up generated response by removing prompt and extra content.
        /// </summary>
        private string CleanResponse(string response)
        {
            // Remove "Assistant:" prefix if present
            if (response.StartsWith("Assistant:", StringComparison.OrdinalIgnoreCase))
            {
                response = response.Substring("Assistant:".Length).TrimStart();
            }

            // Stop at next "User:" or "===" to avoid continuing into metadata
            int userIndex = response.IndexOf("\nUser:", StringComparison.OrdinalIgnoreCase);
            if (userIndex > 0)
            {
                response = response.Substring(0, userIndex);
            }

            int metaIndex = response.IndexOf("\n===");
            if (metaIndex > 0)
            {
                response = response.Substring(0, metaIndex);
            }

            return response.Trim();
        }
    }
}
