using System.Security.Cryptography;

namespace SmallMind.ModelRegistry
{
    /// <summary>
    /// Utilities for file hashing and verification.
    /// </summary>
    internal static class FileHashUtility
    {
        /// <summary>
        /// Computes the SHA256 hash of a file.
        /// </summary>
        /// <param name="filePath">Path to the file.</param>
        /// <returns>Hexadecimal string representation of the SHA256 hash.</returns>
        public static string ComputeSha256(string filePath)
        {
            using var stream = File.OpenRead(filePath);
            using var sha256 = SHA256.Create();
            byte[] hashBytes = sha256.ComputeHash(stream);
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>
        /// Verifies that a file matches the expected SHA256 hash.
        /// </summary>
        /// <param name="filePath">Path to the file.</param>
        /// <param name="expectedHash">Expected SHA256 hash (case-insensitive).</param>
        /// <returns>True if the hash matches; otherwise, false.</returns>
        public static bool VerifySha256(string filePath, string expectedHash)
        {
            string actualHash = ComputeSha256(filePath);
            return string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase);
        }
    }
}
