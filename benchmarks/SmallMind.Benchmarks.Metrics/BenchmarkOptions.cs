namespace SmallMind.Benchmarks
{
    internal class BenchmarkOptions
    {
        public bool ShowHelp { get; set; }
        public string BenchmarkType { get; set; } = "kernel";
        public int WarmupIterations { get; set; } = 5;
        public int MeasuredIterations { get; set; } = 20;
        public int Seed { get; set; } = 42;
        public string OutputDir { get; set; } = ".";
        public List<string> Formats { get; set; } = new List<string> { "markdown" };
    }
}
