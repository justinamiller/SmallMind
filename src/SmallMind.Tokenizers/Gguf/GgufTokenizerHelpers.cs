namespace SmallMind.Tokenizers.Gguf
{
    /// <summary>
    /// Canonical helper methods for GGUF tokenizers.
    /// Pure, stateless functions shared across GGUF BPE and Token Table tokenizers.
    /// </summary>
    internal static class GgufTokenizerHelpers
    {
        /// <summary>
        /// Length of byte tokens in the format &lt;0xXX&gt;
        /// </summary>
        internal const int ByteTokenLength = 6;

        /// <summary>
        /// Checks if a token string represents a byte token and extracts the byte value.
        /// Byte tokens are in the format: &lt;0xXX&gt; where XX is a hex value.
        /// </summary>
        /// <param name="tokenStr">The token string to check</param>
        /// <param name="byteValue">The extracted byte value if the token is a byte token</param>
        /// <returns>True if the token is a byte token, false otherwise</returns>
        internal static bool IsByteToken(string tokenStr, out byte byteValue)
        {
            // Check if token is in byte format: <0xXX> where XX is hex
            if (tokenStr.Length == ByteTokenLength &&
                tokenStr.StartsWith("<0x") &&
                tokenStr.EndsWith(">"))
            {
                return byte.TryParse(
                    tokenStr.Substring(3, 2),
                    System.Globalization.NumberStyles.HexNumber,
                    null,
                    out byteValue);
            }

            byteValue = 0;
            return false;
        }
    }
}
