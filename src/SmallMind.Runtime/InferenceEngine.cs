using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SmallMind.Core.Core;
using SmallMind.Core.Exceptions;
using SmallMind.Tokenizers;
using SmallMind.Transformers;

namespace SmallMind.Runtime
{
    /// <summary>
    /// Thread-safe inference engine facade for concurrent session management.
    /// Provides bounded concurrency and session pooling for production deployments.
    /// Model weights are immutable and shared safely across all sessions.
    /// </summary>
    public sealed class InferenceEngine : IDisposable
    {
        private readonly TransformerModel _model;
        private readonly ITokenizer _tokenizer;
        private readonly int _blockSize;
        private readonly int _maxConcurrentSessions;
        private readonly SemaphoreSlim _concurrencySemaphore;
        private readonly object _lock = new object();
        
        // Track active sessions for diagnostics
        private readonly ConcurrentDictionary<string, InferenceSession> _activeSessions;
        private bool _disposed;
        
        /// <summary>
        /// Gets the number of currently active sessions.
        /// </summary>
        public int ActiveSessionCount => _activeSessions.Count;
        
        /// <summary>
        /// Gets the maximum allowed concurrent sessions.
        /// </summary>
        public int MaxConcurrentSessions => _maxConcurrentSessions;
        
        /// <summary>
        /// Creates a new inference engine with bounded concurrency.
        /// </summary>
        /// <param name="model">Transformer model (weights are shared, read-only across all sessions)</param>
        /// <param name="tokenizer">Tokenizer (read-only)</param>
        /// <param name="blockSize">Model block size for context window</param>
        /// <param name="maxConcurrentSessions">Maximum allowed concurrent sessions (0 for unlimited)</param>
        public InferenceEngine(
            TransformerModel model,
            ITokenizer tokenizer,
            int blockSize,
            int maxConcurrentSessions = 0)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _tokenizer = tokenizer ?? throw new ArgumentNullException(nameof(tokenizer));
            _blockSize = blockSize;
            _maxConcurrentSessions = maxConcurrentSessions;
            
            // Create semaphore for concurrency control if limit is set
            _concurrencySemaphore = maxConcurrentSessions > 0
                ? new SemaphoreSlim(maxConcurrentSessions, maxConcurrentSessions)
                : new SemaphoreSlim(int.MaxValue, int.MaxValue); // Effectively unlimited
            
            _activeSessions = new ConcurrentDictionary<string, InferenceSession>();
            
            // Ensure model is in eval mode (thread-safe)
            _model.Eval();
        }
        
        /// <summary>
        /// Generate text from a prompt (non-streaming).
        /// Automatically manages concurrency and session lifecycle.
        /// </summary>
        /// <param name="prompt">Input text prompt</param>
        /// <param name="options">Inference options (cloned internally)</param>
        /// <param name="metrics">Optional performance metrics collector</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Generated text including the original prompt</returns>
        public async ValueTask<string> GenerateAsync(
            string prompt,
            ProductionInferenceOptions options,
            PerformanceMetrics? metrics = null,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            
            // Wait for available slot (respects cancellation)
            await _concurrencySemaphore.WaitAsync(cancellationToken);
            
            try
            {
                // Create session
                using var session = CreateSession(options);
                
                // Generate
                return await session.GenerateAsync(prompt, metrics, cancellationToken);
            }
            finally
            {
                _concurrencySemaphore.Release();
            }
        }
        
        /// <summary>
        /// Generate text as a stream of tokens.
        /// Automatically manages concurrency and session lifecycle.
        /// </summary>
        /// <param name="prompt">Input text prompt</param>
        /// <param name="options">Inference options (cloned internally)</param>
        /// <param name="metrics">Optional performance metrics collector</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Async enumerable of generated tokens</returns>
        public async IAsyncEnumerable<GeneratedToken> GenerateStreamAsync(
            string prompt,
            ProductionInferenceOptions options,
            PerformanceMetrics? metrics = null,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            
            // Wait for available slot (respects cancellation)
            await _concurrencySemaphore.WaitAsync(cancellationToken);
            
            InferenceSession? session = null;
            try
            {
                // Create session
                session = CreateSession(options);
                
                // Stream tokens
                await foreach (var token in session.GenerateStreamAsync(prompt, metrics, cancellationToken))
                {
                    yield return token;
                }
            }
            finally
            {
                // Clean up session
                if (session != null)
                {
                    _activeSessions.TryRemove(session.SessionId, out _);
                    session.Dispose();
                }
                
                _concurrencySemaphore.Release();
            }
        }
        
        /// <summary>
        /// Create a managed session for manual control.
        /// Caller is responsible for disposing the session.
        /// Session will count against concurrency limit until disposed.
        /// </summary>
        /// <param name="options">Inference options</param>
        /// <param name="sessionId">Optional session ID</param>
        /// <returns>Inference session</returns>
        public InferenceSession CreateManagedSession(
            ProductionInferenceOptions options,
            string? sessionId = null)
        {
            ThrowIfDisposed();
            
            // Check concurrency limit before creating
            if (_maxConcurrentSessions > 0 && _activeSessions.Count >= _maxConcurrentSessions)
            {
                throw new ResourceLimitException(
                    "MaxConcurrentSessions",
                    _maxConcurrentSessions,
                    _activeSessions.Count + 1,
                    "Close existing sessions before creating new ones.");
            }
            
            var session = CreateSession(options, sessionId);
            return session;
        }
        
        /// <summary>
        /// Get current engine statistics.
        /// </summary>
        public EngineStatistics GetStatistics()
        {
            ThrowIfDisposed();
            
            return new EngineStatistics(
                activeSessions: _activeSessions.Count,
                maxConcurrentSessions: _maxConcurrentSessions,
                availableSlots: _maxConcurrentSessions > 0 
                    ? Math.Max(0, _maxConcurrentSessions - _activeSessions.Count)
                    : int.MaxValue);
        }
        
        private InferenceSession CreateSession(ProductionInferenceOptions options, string? sessionId = null)
        {
            var session = new InferenceSession(_model, _tokenizer, options, _blockSize, sessionId);
            
            // Track session
            if (!_activeSessions.TryAdd(session.SessionId, session))
            {
                // Very unlikely race condition
                session.Dispose();
                throw new InvalidOperationException($"Session ID collision: {session.SessionId}");
            }
            
            return session;
        }
        
        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(InferenceEngine));
            }
        }
        
        public void Dispose()
        {
            if (!_disposed)
            {
                // Dispose all active sessions
                foreach (var session in _activeSessions.Values)
                {
                    session?.Dispose();
                }
                _activeSessions.Clear();
                
                _concurrencySemaphore?.Dispose();
                _disposed = true;
            }
        }
    }
    
    /// <summary>
    /// Statistics about the inference engine state.
    /// </summary>
    public readonly struct EngineStatistics : IEquatable<EngineStatistics>
    {
        /// <summary>
        /// Number of currently active sessions.
        /// </summary>
        public readonly int ActiveSessions;
        
        /// <summary>
        /// Maximum allowed concurrent sessions (0 = unlimited).
        /// </summary>
        public readonly int MaxConcurrentSessions;
        
        /// <summary>
        /// Number of available session slots.
        /// </summary>
        public readonly int AvailableSlots;

        /// <summary>
        /// Initializes a new instance of the EngineStatistics struct.
        /// </summary>
        public EngineStatistics(int activeSessions, int maxConcurrentSessions, int availableSlots)
        {
            ActiveSessions = activeSessions;
            MaxConcurrentSessions = maxConcurrentSessions;
            AvailableSlots = availableSlots;
        }

        public bool Equals(EngineStatistics other) =>
            ActiveSessions == other.ActiveSessions &&
            MaxConcurrentSessions == other.MaxConcurrentSessions &&
            AvailableSlots == other.AvailableSlots;

        public override bool Equals(object? obj) =>
            obj is EngineStatistics other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(ActiveSessions, MaxConcurrentSessions, AvailableSlots);

        public static bool operator ==(EngineStatistics left, EngineStatistics right) => left.Equals(right);
        public static bool operator !=(EngineStatistics left, EngineStatistics right) => !left.Equals(right);
    }
}
