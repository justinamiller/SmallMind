using System;
using System.Collections.Generic;
using System.Linq;

namespace SmallMind.Benchmarks.Options
{
    /// <summary>
    /// Simple command-line argument parser without external dependencies.
    /// </summary>
    internal sealed class CommandLineParser
    {
        private readonly Dictionary<string, string> _options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _positionalArgs = new List<string>();

        public CommandLineParser(string[] args)
        {
            if (args == null || args.Length == 0)
                return;

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];

                if (arg.StartsWith("--"))
                {
                    // Long option: --option or --option=value or --option value
                    string optionName = arg.Substring(2);
                    string? optionValue = null;

                    int equalsIndex = optionName.IndexOf('=');
                    if (equalsIndex >= 0)
                    {
                        // --option=value
                        optionValue = optionName.Substring(equalsIndex + 1);
                        optionName = optionName.Substring(0, equalsIndex);
                    }
                    else if (i + 1 < args.Length && !args[i + 1].StartsWith("--") && !args[i + 1].StartsWith("-"))
                    {
                        // --option value
                        optionValue = args[i + 1];
                        i++;
                    }

                    _options[optionName] = optionValue ?? "true";
                }
                else if (arg.StartsWith("-") && arg.Length > 1)
                {
                    // Short option: -o or -o value
                    string optionName = arg.Substring(1);
                    string? optionValue = null;

                    if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                    {
                        optionValue = args[i + 1];
                        i++;
                    }

                    _options[optionName] = optionValue ?? "true";
                }
                else
                {
                    // Positional argument
                    _positionalArgs.Add(arg);
                }
            }
        }

        public bool HasOption(string name)
        {
            return _options.ContainsKey(name);
        }

        public string? GetOption(string name, string? defaultValue = null)
        {
            return _options.TryGetValue(name, out var value) ? value : defaultValue;
        }

        public int GetOptionInt(string name, int defaultValue)
        {
            string? value = GetOption(name);
            return value != null && int.TryParse(value, out int result) ? result : defaultValue;
        }

        public bool GetOptionBool(string name, bool defaultValue = false)
        {
            if (!_options.TryGetValue(name, out var value))
                return defaultValue;

            if (value == "true" || value == "1" || value == "yes")
                return true;

            if (value == "false" || value == "0" || value == "no")
                return false;

            return defaultValue;
        }

        public string[] GetOptionArray(string name, char separator = ',')
        {
            string? value = GetOption(name);
            if (string.IsNullOrWhiteSpace(value))
                return Array.Empty<string>();

            return value.Split(separator, StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => s.Trim())
                        .ToArray();
        }

        public int[] GetOptionIntArray(string name, char separator = ',')
        {
            string[] values = GetOptionArray(name, separator);
            var results = new List<int>();

            foreach (string value in values)
            {
                if (int.TryParse(value, out int result))
                {
                    results.Add(result);
                }
            }

            return results.ToArray();
        }

        public List<string> GetPositionalArgs()
        {
            return new List<string>(_positionalArgs);
        }

        public string? GetPositionalArg(int index)
        {
            return index >= 0 && index < _positionalArgs.Count ? _positionalArgs[index] : null;
        }

        public int PositionalArgCount => _positionalArgs.Count;
    }
}
