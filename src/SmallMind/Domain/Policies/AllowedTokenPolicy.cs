using System.Collections.Generic;

namespace SmallMind.Domain.Policies
{
    /// <summary>
    /// Defines allowed tokens for domain-bounded generation.
    /// Supports both allowlist and blocklist approaches.
    /// </summary>
    public class AllowedTokenPolicy
    {
        /// <summary>
        /// Gets or sets the list of allowed token IDs.
        /// If null, all tokens are allowed (subject to blocklist).
        /// </summary>
        public IReadOnlyList<int>? AllowedTokenIds { get; set; }

        /// <summary>
        /// Gets or sets the list of allowed character patterns.
        /// For character-level tokenizers, defines allowed characters.
        /// If null, all characters are allowed (subject to blocklist).
        /// </summary>
        public string? AllowedCharacters { get; set; }

        /// <summary>
        /// Gets or sets the list of blocked token IDs.
        /// These tokens will never be generated.
        /// </summary>
        public IReadOnlyList<int>? BlockedTokenIds { get; set; }

        /// <summary>
        /// Gets or sets the list of blocked characters.
        /// For character-level tokenizers, defines disallowed characters.
        /// </summary>
        public string? BlockedCharacters { get; set; }

        /// <summary>
        /// Creates a default AllowedTokenPolicy with no restrictions.
        /// </summary>
        /// <returns>A default policy allowing all tokens.</returns>
        public static AllowedTokenPolicy Default() => new AllowedTokenPolicy();

        /// <summary>
        /// Creates an AllowedTokenPolicy that only allows specific characters.
        /// </summary>
        /// <param name="allowedChars">The characters to allow.</param>
        /// <returns>A policy allowing only the specified characters.</returns>
        public static AllowedTokenPolicy AllowCharacters(string allowedChars)
        {
            return new AllowedTokenPolicy
            {
                AllowedCharacters = allowedChars
            };
        }

        /// <summary>
        /// Creates an AllowedTokenPolicy that blocks specific characters.
        /// </summary>
        /// <param name="blockedChars">The characters to block.</param>
        /// <returns>A policy blocking the specified characters.</returns>
        public static AllowedTokenPolicy BlockCharacters(string blockedChars)
        {
            return new AllowedTokenPolicy
            {
                BlockedCharacters = blockedChars
            };
        }
    }
}
