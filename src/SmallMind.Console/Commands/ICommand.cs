using System;
using System.Threading.Tasks;

namespace SmallMind.ConsoleApp.Commands
{
    /// <summary>
    /// Interface for CLI commands.
    /// </summary>
    internal interface ICommand
    {
        /// <summary>
        /// Command name (e.g., "quantize", "import-gguf").
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Brief description of what the command does.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Execute the command with the given arguments.
        /// </summary>
        /// <param name="args">Command-line arguments (after the command name).</param>
        /// <returns>Exit code (0 for success, non-zero for error).</returns>
        Task<int> ExecuteAsync(string[] args);

        /// <summary>
        /// Display usage information for this command.
        /// </summary>
        void ShowUsage();
    }
}
