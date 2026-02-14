using System.Text.Json;
using System.Text.Json.Serialization;
using SmallMind.Abstractions;

namespace SmallMind.Engine
{
    /// <summary>
    /// File-based session store with atomic writes and schema versioning.
    /// Uses safe temp-file-then-rename pattern for atomic writes.
    /// </summary>
    internal sealed class FileSessionStore : ISessionStore
    {
        private readonly string _storageDirectory;
        private readonly JsonSerializerOptions _jsonOptions;
        private const int CurrentSchemaVersion = 2;

        /// <summary>
        /// Initializes a new file session store.
        /// </summary>
        /// <param name="storageDirectory">Directory to store session files.</param>
        public FileSessionStore(string storageDirectory)
        {
            if (string.IsNullOrWhiteSpace(storageDirectory))
                throw new ArgumentException("Storage directory cannot be null or empty", nameof(storageDirectory));

            _storageDirectory = storageDirectory;
            Directory.CreateDirectory(_storageDirectory);

            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
        }

        public async Task<ChatSessionData?> GetAsync(string sessionId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
                throw new ArgumentException("Session ID cannot be null or empty", nameof(sessionId));

            var filePath = GetSessionFilePath(sessionId);
            if (!File.Exists(filePath))
                return null;

            try
            {
                var json = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
                var envelope = JsonSerializer.Deserialize<SessionEnvelope>(json, _jsonOptions);

                if (envelope == null)
                    return null;

                // Handle schema migration
                return envelope.Version switch
                {
                    1 => MigrateV1ToV2(envelope.Data),
                    2 => envelope.Data,
                    _ => throw new InvalidOperationException($"Unsupported schema version: {envelope.Version}")
                };
            }
            catch (JsonException)
            {
                // Corrupt file - return null
                return null;
            }
        }

        public async Task UpsertAsync(ChatSessionData session, CancellationToken cancellationToken = default)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));
            if (string.IsNullOrWhiteSpace(session.SessionId))
                throw new ArgumentException("Session ID cannot be null or empty");

            var filePath = GetSessionFilePath(session.SessionId);
            var envelope = new SessionEnvelope
            {
                Version = CurrentSchemaVersion,
                Data = session
            };

            var json = JsonSerializer.Serialize(envelope, _jsonOptions);

            // Atomic write: write to temp file, then rename
            var tempPath = filePath + ".tmp";
            try
            {
                await File.WriteAllTextAsync(tempPath, json, cancellationToken).ConfigureAwait(false);

                // Atomic rename (overwrites existing file on most platforms)
                File.Move(tempPath, filePath, overwrite: true);
            }
            catch
            {
                // Clean up temp file on error
                if (File.Exists(tempPath))
                {
                    try { File.Delete(tempPath); } catch (Exception) { /* Ignore cleanup failures */ }
                }
                throw;
            }
        }

        public Task DeleteAsync(string sessionId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
                throw new ArgumentException("Session ID cannot be null or empty", nameof(sessionId));

            var filePath = GetSessionFilePath(sessionId);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(string sessionId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
                throw new ArgumentException("Session ID cannot be null or empty", nameof(sessionId));

            var filePath = GetSessionFilePath(sessionId);
            return Task.FromResult(File.Exists(filePath));
        }

        private string GetSessionFilePath(string sessionId)
        {
            // Sanitize session ID for file system
            var safeSessionId = string.Join("_", sessionId.Split(Path.GetInvalidFileNameChars()));
            return Path.Combine(_storageDirectory, $"{safeSessionId}.json");
        }

        private static ChatSessionData MigrateV1ToV2(ChatSessionData v1Data)
        {
            // V1 to V2 migration logic
            // In this case, V2 just adds schema version tracking, so data is compatible
            return v1Data;
        }

        /// <summary>
        /// Session envelope with versioning.
        /// </summary>
        private sealed class SessionEnvelope
        {
            [JsonPropertyName("version")]
            public int Version { get; set; }

            [JsonPropertyName("data")]
            public ChatSessionData Data { get; set; } = new ChatSessionData();
        }
    }

    /// <summary>
    /// Enhanced chat session data with schema version.
    /// This is V2 of the schema.
    /// </summary>
    internal sealed class ChatSessionDataV2
    {
        /// <summary>
        /// Schema version for migration support.
        /// </summary>
        public int SchemaVersion { get; set; } = 2;

        /// <summary>
        /// Unique session identifier.
        /// </summary>
        public string SessionId { get; set; } = string.Empty;

        /// <summary>
        /// When the session was created.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// When the session was last updated.
        /// </summary>
        public DateTime LastUpdatedAt { get; set; }

        /// <summary>
        /// Ordered list of conversation turns.
        /// </summary>
        public List<ChatTurnData> Turns { get; set; } = new List<ChatTurnData>();

        /// <summary>
        /// Optional session metadata.
        /// </summary>
        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Model identifier used for this session.
        /// </summary>
        public string? ModelId { get; set; }

        /// <summary>
        /// Maximum context tokens for this session.
        /// </summary>
        public int? MaxContextTokens { get; set; }

        /// <summary>
        /// Current KV cache size in tokens.
        /// </summary>
        public int? KvCacheTokens { get; set; }
    }
}
