using System;
using System.Diagnostics;
using SmallMind.Transformers;

namespace SmallMind.ProfileModelCreation
{
    /// <summary>
    /// Simple profiler to measure model creation time and identify bottlenecks.
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== SmallMind Model Creation Profiling ===\n");
            
            // Profile different model sizes
            ProfileModelCreation("Tiny", () => 
                TransformerModelBuilder.Create().UseTinyConfig(50).Build());
            
            ProfileModelCreation("Small", () => 
                TransformerModelBuilder.Create().UseSmallConfig(100).Build());
            
            ProfileModelCreation("Medium", () => 
                TransformerModelBuilder.Create().UseMediumConfig(100).Build());
            
            Console.WriteLine("\n=== Profiling Complete ===");
        }
        
        static void ProfileModelCreation(string modelName, Func<TransformerModel> createModel)
        {
            Console.WriteLine($"--- {modelName} Model Creation ---");
            
            // Warmup
            var warmupModel = createModel();
            
            // Profile with 5 iterations
            const int iterations = 5;
            var times = new double[iterations];
            
            for (int i = 0; i < iterations; i++)
            {
                // Force GC before each run for consistent measurements
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                
                var sw = Stopwatch.StartNew();
                var model = createModel();
                sw.Stop();
                
                times[i] = sw.Elapsed.TotalMilliseconds;
            }
            
            // Calculate statistics
            Array.Sort(times);
            double median = times[iterations / 2];
            double min = times[0];
            double max = times[iterations - 1];
            double avg = 0;
            foreach (var t in times) avg += t;
            avg /= iterations;
            
            Console.WriteLine($"  Min:    {min:F2} ms");
            Console.WriteLine($"  Median: {median:F2} ms");
            Console.WriteLine($"  Avg:    {avg:F2} ms");
            Console.WriteLine($"  Max:    {max:F2} ms");
            Console.WriteLine();
        }
    }
}
