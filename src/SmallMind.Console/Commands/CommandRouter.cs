using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SmallMind.ConsoleApp.Commands
{
    /// <summary>
    /// Routes CLI commands to their implementations.
    /// </summary>
    public sealed class CommandRouter
    {
        private readonly Dictionary<string, ICommand> _commands;

        public CommandRouter()
        {
            _commands = new Dictionary<string, ICommand>(StringComparer.OrdinalIgnoreCase)
            {
                ["quantize"] = new QuantizeCommand(),
                ["import-gguf"] = new ImportGgufCommand(),
                ["inspect"] = new InspectCommand(),
                ["verify"] = new VerifyCommand(),
                ["model add"] = new ModelAddCommand(),
                ["model list"] = new ModelListCommand(),
                ["model verify"] = new ModelVerifyCommand(),
                ["model inspect"] = new ModelInspectCommand()
            };
        }

        /// <summary>
        /// Execute a command by name.
        /// </summary>
        /// <param name="commandName">Name of the command to execute.</param>
        /// <param name="args">Arguments to pass to the command.</param>
        /// <returns>Exit code (0 for success).</returns>
        public async Task<int> ExecuteAsync(string commandName, string[] args)
        {
            if (string.IsNullOrEmpty(commandName))
            {
                ShowHelp();
                return 1;
            }

            if (!_commands.TryGetValue(commandName, out var command))
            {
                System.Console.Error.WriteLine($"Unknown command: {commandName}");
                System.Console.Error.WriteLine();
                ShowHelp();
                return 1;
            }

            return await command.ExecuteAsync(args);
        }

        /// <summary>
        /// Show general help for all commands.
        /// </summary>
        public void ShowHelp()
        {
            System.Console.WriteLine("SmallMind Quantization CLI");
            System.Console.WriteLine();
            System.Console.WriteLine("Usage: smallmind <command> [arguments] [options]");
            System.Console.WriteLine();
            System.Console.WriteLine("Available commands:");
            
            int maxNameLength = _commands.Keys.Max(k => k.Length);
            
            foreach (var kvp in _commands.OrderBy(x => x.Key))
            {
                System.Console.WriteLine($"  {kvp.Key.PadRight(maxNameLength + 2)}{kvp.Value.Description}");
            }
            
            System.Console.WriteLine();
            System.Console.WriteLine("Run 'smallmind <command> --help' for command-specific help");
        }

        /// <summary>
        /// Check if a command exists.
        /// </summary>
        public bool HasCommand(string commandName)
        {
            return _commands.ContainsKey(commandName);
        }
    }
}
