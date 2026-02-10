using System.Text.Json;
using SmallMind.Showcase.Core.Interfaces;
using SmallMind.Showcase.Core.Models;

namespace SmallMind.Showcase.Core.Services;

/// <summary>
/// JSON file-based storage for chat sessions.
/// </summary>
public sealed class JsonChatSessionStore : IChatSessionStore
{
    private readonly string _dataPath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public JsonChatSessionStore(string dataPath)
    {
        _dataPath = dataPath ?? throw new ArgumentNullException(nameof(dataPath));
        Directory.CreateDirectory(_dataPath);
    }

    public async Task<List<ChatSession>> GetAllSessionsAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var sessions = new List<ChatSession>();
            var files = Directory.GetFiles(_dataPath, "session_*.json");

            foreach (var file in files)
            {
                try
                {
                    var json = await File.ReadAllTextAsync(file, cancellationToken);
                    var session = JsonSerializer.Deserialize<ChatSession>(json, _jsonOptions);
                    if (session != null)
                    {
                        sessions.Add(session);
                    }
                }
                catch
                {
                    // Skip corrupted files
                }
            }

            return sessions.OrderByDescending(s => s.UpdatedAt).ToList();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<ChatSession?> GetSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var filePath = GetSessionFilePath(sessionId);
            if (!File.Exists(filePath))
            {
                return null;
            }

            var json = await File.ReadAllTextAsync(filePath, cancellationToken);
            return JsonSerializer.Deserialize<ChatSession>(json, _jsonOptions);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<ChatSession> CreateSessionAsync(ChatSession session, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var filePath = GetSessionFilePath(session.Id);
            var json = JsonSerializer.Serialize(session, _jsonOptions);
            await File.WriteAllTextAsync(filePath, json, cancellationToken);
            return session;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task UpdateSessionAsync(ChatSession session, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            session.UpdatedAt = DateTime.UtcNow;
            var filePath = GetSessionFilePath(session.Id);
            var json = JsonSerializer.Serialize(session, _jsonOptions);
            await File.WriteAllTextAsync(filePath, json, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task DeleteSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var filePath = GetSessionFilePath(sessionId);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    private string GetSessionFilePath(string sessionId)
    {
        return Path.Combine(_dataPath, $"session_{sessionId}.json");
    }
}
