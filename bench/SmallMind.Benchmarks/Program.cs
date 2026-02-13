using System;
using System.Threading.Tasks;
using SmallMind.Benchmarks.Commands;

namespace SmallMind.Benchmarks
{
    internal sealed class Program
    {
        private static async Task<int> Main(string[] args)
        {
            try
            {
                if (args.Length == 0 || args[0] == "--help" || args[0] == "-h")
                {
                    ShowHelp();
                    return 0;
                }

                string command = args[0].ToLowerInvariant();
                string[] commandArgs = new string[args.Length - 1];
                Array.Copy(args, 1, commandArgs, 0, commandArgs.Length);

                switch (command)
                {
                    case "run":
                        return await new RunCommand().ExecuteAsync(commandArgs);
                    
                    case "download":
                        return await new DownloadCommand().ExecuteAsync(commandArgs);
                    
                    case "merge":
                        return await new MergeCommand().ExecuteAsync(commandArgs);
                    
                    default:
                        Console.Error.WriteLine($"Unknown command: {command}");
                        Console.Error.WriteLine();
                        ShowHelp();
                        return 1;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Fatal error: {ex.Message}");
                if (Environment.GetEnvironmentVariable("SMALLMIND_BENCH_VERBOSE") == "1")
                {
                    Console.Error.WriteLine(ex.StackTrace);
                }
                return 1;
            }
        }

        private static void ShowHelp()
        {
            Console.WriteLine("SmallMind Benchmarks - Performance testing tool for SmallMind LLM");
            Console.WriteLine();
            Console.WriteLine("Usage: SmallMind.Benchmarks <command> [options]");
            Console.WriteLine();
            Console.WriteLine("Commands:");
            Console.WriteLine("  run        Run benchmarks with specified configuration");
            Console.WriteLine("  download   Download models from manifest");
            Console.WriteLine("  merge      Merge multiple result files into summary table");
            Console.WriteLine();
            Console.WriteLine("Run 'SmallMind.Benchmarks <command> --help' for command-specific help");
            Console.WriteLine();
            Console.WriteLine("Environment Variables:");
            Console.WriteLine("  SMALLMIND_BENCH_VERBOSE=1     Show verbose error messages");
            Console.WriteLine("  SMALLMIND_BENCH_MODEL_CACHE   Override model cache directory");
            Console.WriteLine("  SMALLMIND_BENCH_SKIP_CHECKSUM Override checksum verification");
        }
    }
}
