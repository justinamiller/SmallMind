using System;
using System.Collections.Generic;

namespace SmallMind.Core.Utilities
{
    /// <summary>
    /// Canonical helper methods for GGUF metadata extraction.
    /// Pure, stateless functions for extracting and converting GGUF metadata values.
    /// Used across Engine and Runtime projects to avoid duplication.
    /// </summary>
    internal static class GgufMetadataHelpers
    {
        /// <summary>
        /// Extract an integer value from GGUF metadata with fallback to default.
        /// Handles various value types including JsonElement from deserialized metadata.
        /// </summary>
        /// <param name="metadata">The metadata dictionary</param>
        /// <param name="key">The key to look up</param>
        /// <param name="defaultValue">The default value if key is not found or conversion fails</param>
        /// <returns>The extracted integer value or the default</returns>
        internal static int ExtractMetadataInt(
            Dictionary<string, object>? metadata,
            string key,
            int defaultValue)
        {
            if (metadata != null && metadata.TryGetValue(key, out var value))
            {
                // Handle JsonElement from deserialized SMQ metadata
                if (value is System.Text.Json.JsonElement jsonElement)
                {
                    if (jsonElement.ValueKind == System.Text.Json.JsonValueKind.Number)
                    {
                        return jsonElement.GetInt32();
                    }
                    else if (jsonElement.ValueKind == System.Text.Json.JsonValueKind.String)
                    {
                        if (int.TryParse(jsonElement.GetString(), out int parsed))
                        {
                            return parsed;
                        }
                    }
                }
                
                // Try direct conversion for other types
                try
                {
                    return Convert.ToInt32(value);
                }
                catch
                {
                    // Conversion failed, return default
                    return defaultValue;
                }
            }
            return defaultValue;
        }
    }
}
