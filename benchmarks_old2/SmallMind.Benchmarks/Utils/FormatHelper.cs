namespace SmallMind.Benchmarks.Utils
{
    /// <summary>
    /// Formatting utilities for benchmark output.
    /// </summary>
    internal static class FormatHelper
    {
        /// <summary>
        /// Format bytes to human-readable string (KB, MB, GB).
        /// </summary>
        public static string FormatBytes(long bytes)
        {
            if (bytes < 0)
                return "0 B";

            const long KB = 1024;
            const long MB = KB * 1024;
            const long GB = MB * 1024;

            if (bytes >= GB)
                return $"{bytes / (double)GB:F2} GB";
            if (bytes >= MB)
                return $"{bytes / (double)MB:F2} MB";
            if (bytes >= KB)
                return $"{bytes / (double)KB:F2} KB";

            return $"{bytes} B";
        }

        /// <summary>
        /// Format duration in milliseconds to human-readable string.
        /// </summary>
        public static string FormatDuration(double milliseconds)
        {
            if (milliseconds < 1)
                return $"{milliseconds * 1000:F2} μs";
            if (milliseconds < 1000)
                return $"{milliseconds:F2} ms";

            double seconds = milliseconds / 1000.0;
            if (seconds < 60)
                return $"{seconds:F2} s";

            double minutes = seconds / 60.0;
            if (minutes < 60)
                return $"{minutes:F2} min";

            double hours = minutes / 60.0;
            return $"{hours:F2} hr";
        }

        /// <summary>
        /// Format throughput (tokens per second).
        /// </summary>
        public static string FormatThroughput(double tokensPerSecond)
        {
            if (tokensPerSecond < 0)
                return "0.00 tok/s";

            return $"{tokensPerSecond:F2} tok/s";
        }

        /// <summary>
        /// Format GFLOPS.
        /// </summary>
        public static string FormatGFlops(double gflops)
        {
            if (gflops < 0)
                return "0.00 GFLOPS";

            if (gflops >= 1000)
                return $"{gflops / 1000.0:F2} TFLOPS";

            return $"{gflops:F2} GFLOPS";
        }

        /// <summary>
        /// Format percentage.
        /// </summary>
        public static string FormatPercentage(double value)
        {
            return $"{value:F2}%";
        }

        /// <summary>
        /// Format a number with thousands separator.
        /// </summary>
        public static string FormatNumber(long number)
        {
            return number.ToString("N0");
        }

        /// <summary>
        /// Format a floating-point number with specified decimal places.
        /// </summary>
        public static string FormatDecimal(double value, int decimalPlaces = 2)
        {
            return value.ToString($"F{decimalPlaces}");
        }

        /// <summary>
        /// Truncate string to max length with ellipsis.
        /// </summary>
        public static string Truncate(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
                return text ?? string.Empty;

            return text.Substring(0, maxLength - 3) + "...";
        }

        /// <summary>
        /// Pad string to align in columns.
        /// </summary>
        public static string PadRight(string text, int width)
        {
            return (text ?? string.Empty).PadRight(width);
        }

        /// <summary>
        /// Pad string to align in columns (left-aligned numbers).
        /// </summary>
        public static string PadLeft(string text, int width)
        {
            return (text ?? string.Empty).PadLeft(width);
        }
    }
}
