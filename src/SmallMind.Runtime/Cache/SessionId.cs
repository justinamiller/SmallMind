using System;

namespace SmallMind.Runtime.Cache
{
    /// <summary>
    /// Represents a unique session identifier for KV cache management.
    /// Immutable value type for safe dictionary keys.
    /// </summary>
    internal readonly struct SessionId : IEquatable<SessionId>
    {
        private readonly string _value;

        /// <summary>
        /// Creates a new session ID from a string value.
        /// </summary>
        public SessionId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Session ID cannot be null or whitespace", nameof(value));
            
            _value = value;
        }

        /// <summary>
        /// Creates a new random session ID.
        /// </summary>
        public static SessionId NewId() => new SessionId(Guid.NewGuid().ToString("N"));

        /// <summary>
        /// Gets the string value of this session ID.
        /// </summary>
        public string Value => _value ?? string.Empty;

        public bool Equals(SessionId other) => _value == other._value;

        public override bool Equals(object? obj) => obj is SessionId other && Equals(other);

        public override int GetHashCode() => _value?.GetHashCode() ?? 0;

        public override string ToString() => _value ?? string.Empty;

        public static bool operator ==(SessionId left, SessionId right) => left.Equals(right);

        public static bool operator !=(SessionId left, SessionId right) => !left.Equals(right);

        public static implicit operator string(SessionId sessionId) => sessionId.Value;

        public static implicit operator SessionId(string value) => new SessionId(value);
    }
}
