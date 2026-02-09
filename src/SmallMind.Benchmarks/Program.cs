using System;

namespace SmallMind.Benchmarks
{
    internal class Program
    {
        public static int Main(string[] args)
        {
            try
            {
                var config = new BenchmarkConfig
                {
                    WarmupIterations = 5,
                    MeasuredIterations = 20,
                    Seed = 42
                };

                var runner = new BenchmarkRunner(config);
                runner.RunAll();

                return 0;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine($"Benchmark failed: {ex.Message}");
                Console.Error.WriteLine(ex.StackTrace);
                Console.ResetColor();
                return 1;
            }
        }
    }
}
