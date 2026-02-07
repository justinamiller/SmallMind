namespace SmallMind.Perf;

public class Program
{
    public static int Main(string[] args)
    {
        try
        {
            var config = PerfConfig.Parse(args);
            var runner = new PerfRunner(config);
            
            Console.WriteLine("SmallMind.Perf - Deterministic Performance Microbenchmarks");
            Console.WriteLine("===========================================================");
            Console.WriteLine();
            
            runner.RunAll();
            
            return 0;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"Error: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
            Console.ResetColor();
            return 1;
        }
    }
}
