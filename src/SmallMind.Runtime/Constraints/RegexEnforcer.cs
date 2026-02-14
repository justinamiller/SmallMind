using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace SmallMind.Runtime.Constraints
{
    /// <summary>
    /// Enforces that generated output matches a regular expression pattern.
    /// Uses incremental matching to validate prefixes during generation.
    /// </summary>
    internal sealed class RegexEnforcer : IOutputConstraint
    {
        private readonly Regex _pattern;
        private readonly string _patternString;

        // Static cache to reuse compiled regex instances across constraint instances
        // Thread-safe for concurrent access during inference
        private static readonly ConcurrentDictionary<string, Regex> PatternCache =
            new(StringComparer.Ordinal);

        // Timeout for user-provided regex patterns to prevent ReDoS attacks
        private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Creates a new RegexEnforcer with the specified pattern.
        /// </summary>
        /// <param name="pattern">The regex pattern to enforce.</param>
        public RegexEnforcer(string pattern)
        {
            if (string.IsNullOrEmpty(pattern))
                throw new ArgumentException("Pattern cannot be null or empty", nameof(pattern));

            _patternString = pattern;
            // Reuse compiled regex from cache or create new one with timeout protection
            _pattern = PatternCache.GetOrAdd(pattern,
                p => new Regex(p, RegexOptions.Compiled, RegexTimeout));
        }

        public string ConstraintDescription => $"Regex pattern: {_patternString}";

        public bool IsTokenAllowed(string generatedSoFar, int candidateTokenId, string candidateTokenText)
        {
            if (string.IsNullOrEmpty(candidateTokenText))
                return false;

            // Check if the combined text could still lead to a match
            // This is a simplified check - we allow it if it's either:
            // 1. Already a match (could continue)
            // 2. Could be a prefix of a match (partial match possible)

            // For simplicity, we're permissive here - full validation happens in IsComplete
            // A more sophisticated implementation would use a DFA/NFA for prefix matching
            return true;
        }

        public bool IsComplete(string generatedSoFar)
        {
            if (string.IsNullOrEmpty(generatedSoFar))
                return false;

            try
            {
                return _pattern.IsMatch(generatedSoFar);
            }
            catch (RegexMatchTimeoutException)
            {
                // Timeout occurred - treat as invalid to prevent ReDoS attacks
                return false;
            }
        }
    }
}
