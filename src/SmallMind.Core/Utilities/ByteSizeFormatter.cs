using System;

namespace SmallMind.Core.Utilities
{
    /// <summary>
    /// Provides formatting utilities for converting byte sizes to human-readable strings.
    /// </summary>
    public static class ByteSizeFormatter
    {
        /// <summary>
        /// Formats a byte count into a human-readable string with appropriate unit suffix.
        /// </summary>
        /// <param name="bytes">The number of bytes to format.</param>
        /// <returns>A formatted string with the appropriate size unit (B, KB, MB, GB, TB).</returns>
        /// <example>
        /// <code>
        /// ByteSizeFormatter.FormatBytes(512);           // "512 B"
        /// ByteSizeFormatter.FormatBytes(2048);          // "2.00 KB"
        /// ByteSizeFormatter.FormatBytes(1073741824);    // "1.00 GB"
        /// </code>
        /// </example>
        public static string FormatBytes(long bytes)
        {
            if (bytes >= 1_099_511_627_776L) // 1 TB
                return $"{bytes / 1_099_511_627_776.0:F2} TB";
            else if (bytes >= 1_073_741_824L) // 1 GB
                return $"{bytes / 1_073_741_824.0:F2} GB";
            else if (bytes >= 1_048_576L) // 1 MB
                return $"{bytes / 1_048_576.0:F2} MB";
            else if (bytes >= 1024L) // 1 KB
                return $"{bytes / 1024.0:F2} KB";
            else
                return $"{bytes} B";
        }

        /// <summary>
        /// Formats a byte count (as a double) into a human-readable string with appropriate unit suffix.
        /// </summary>
        /// <param name="bytes">The number of bytes to format (allows fractional bytes for calculations).</param>
        /// <returns>A formatted string with the appropriate size unit (B, KB, MB, GB, TB).</returns>
        public static string FormatBytes(double bytes)
        {
            // Convert to long and use the primary implementation
            return FormatBytes((long)bytes);
        }
    }
}
