using SmallMind.Benchmarks;
using System.Diagnostics;

Console.WriteLine("SmallMind Benchmarking Harness");
Console.WriteLine("================================");
Console.WriteLine();

try
{
    // Parse arguments
    var config = CliParser.Parse(args);
    
    // Child run mode (for cold start)
    if (config.ChildRun)
    {
        await RunChildProcessAsync(config);
        return 0;
    }
    
    // Normal benchmark run
    await RunBenchmarksAsync(config);
    return 0;
}
catch (ArgumentException ex)
{
    Console.WriteLine($"Error: {ex.Message}");
    Console.WriteLine();
    Console.WriteLine("Use --help for usage information.");
    return 1;
}
catch (Exception ex)
{
    Console.WriteLine($"Fatal error: {ex.Message}");
    Console.WriteLine(ex.StackTrace);
    return 1;
}

static async Task RunBenchmarksAsync(BenchmarkConfig config)
{
    Console.WriteLine($"Model: {config.ModelPath}");
    Console.WriteLine($"Scenario: {config.Scenario}");
    Console.WriteLine($"Iterations: {config.Iterations} (warmup: {config.Warmup})");
    Console.WriteLine();
    
    // Create output directory
    Directory.CreateDirectory(config.OutputDir);
    
    // Collect environment metadata
    var environment = EnvironmentMetadata.Collect(config);
    
    // Create report
    var report = new BenchmarkReport
    {
        SchemaVersion = 1,
        Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
        TimestampUtc = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + " UTC",
        Environment = environment,
        RunConfig = config
    };
    
    // Start runtime counters listener
    using var counters = new RuntimeCountersListener();
    
    // Initialize engine and load model
    Console.WriteLine("Loading model...");
    using var engine = new EngineAdapter();
    using var cts = new CancellationTokenSource();
    
    // Handle Ctrl+C
    Console.CancelKeyPress += (s, e) =>
    {
        Console.WriteLine("\nCancelling...");
        cts.Cancel();
        e.Cancel = true;
    };
    
    try
    {
        await engine.LoadModelAsync(config.ModelPath, config.Threads, cts.Token);
        Console.WriteLine("Model loaded successfully.");
        Console.WriteLine();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Failed to load model: {ex.Message}");
        return;
    }
    
    // Create scenario runner
    var runner = new ScenarioRunner(config, engine, counters);
    
    // Determine which scenarios to run
    var scenariosToRun = GetScenariosToRun(config.Scenario);
    
    // Run scenarios
    foreach (var scenario in scenariosToRun)
    {
        try
        {
            ScenarioResult? result = null;
            
            switch (scenario)
            {
                case "ttft":
                    result = await runner.RunTtftAsync(cts.Token);
                    break;
                    
                case "tokens_per_sec":
                    result = await runner.RunTokensPerSecAsync(cts.Token);
                    break;
                    
                case "latency":
                    result = await runner.RunLatencyAsync(cts.Token);
                    break;
                    
                case "memory":
                    result = await runner.RunMemoryAsync(cts.Token);
                    break;
                    
                case "gc":
                    result = await runner.RunGcAsync(cts.Token);
                    break;
                    
                case "concurrency":
                    // Run for each concurrency level
                    foreach (var concurrency in config.Concurrency)
                    {
                        var concResult = await runner.RunConcurrencyAsync(concurrency, cts.Token);
                        report.Scenarios.Add(concResult);
                    }
                    continue;
                    
                case "env":
                    // Environment metadata already collected
                    continue;
                    
                default:
                    Console.WriteLine($"Unknown scenario: {scenario}");
                    continue;
            }
            
            if (result != null)
            {
                report.Scenarios.Add(result);
            }
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Benchmark cancelled by user.");
            break;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error running scenario '{scenario}': {ex.Message}");
        }
    }
    
    // Generate reports
    Console.WriteLine();
    Console.WriteLine("Generating reports...");
    
    if (config.EmitMarkdown)
    {
        string mdPath = Path.Combine(config.OutputDir, "report.md");
        string markdown = MarkdownReportGenerator.Generate(report);
        await File.WriteAllTextAsync(mdPath, markdown);
        Console.WriteLine($"  Markdown: {mdPath}");
    }
    
    if (config.EmitJson)
    {
        string jsonPath = Path.Combine(config.OutputDir, "results.json");
        string json = JsonReportGenerator.Generate(report);
        await File.WriteAllTextAsync(jsonPath, json);
        Console.WriteLine($"  JSON: {jsonPath}");
    }
    
    Console.WriteLine();
    Console.WriteLine("Benchmarks complete!");
}

static async Task RunChildProcessAsync(BenchmarkConfig config)
{
    // This is a single iteration run for cold start mode
    // Output results as JSON to stdout for parent to parse
    
    using var engine = new EngineAdapter();
    using var cts = new CancellationTokenSource();
    
    try
    {
        await engine.LoadModelAsync(config.ModelPath, config.Threads, cts.Token);
        
        var prompt = PromptProfiles.GetPrompt(config.PromptProfile);
        var measurement = await engine.GenerateAsync(prompt, config.MaxNewTokens,
            config.Temperature, config.TopK, config.TopP, config.Seed, cts.Token);
        
        // Output JSON result
        var result = new
        {
            ttft_ms = measurement.TtftMs,
            latency_ms = measurement.LatencyMs,
            tokens_per_sec = measurement.OverallTokensPerSec,
            steady_tokens_per_sec = measurement.SteadyTokensPerSec,
            token_count = measurement.TokenCount
        };
        
        string json = System.Text.Json.JsonSerializer.Serialize(result);
        Console.WriteLine(json);
    }
    catch (Exception ex)
    {
        // Write error to stderr
        Console.Error.WriteLine($"Error: {ex.Message}");
        Environment.Exit(1);
    }
}

static List<string> GetScenariosToRun(string scenarioSpec)
{
    if (scenarioSpec == "all")
    {
        return new List<string>
        {
            "ttft",
            "tokens_per_sec",
            "latency",
            "memory",
            "gc",
            "concurrency"
        };
    }
    
    // Single scenario or comma-separated list
    var scenarios = new List<string>();
    foreach (var s in scenarioSpec.Split(','))
    {
        scenarios.Add(s.Trim().ToLowerInvariant());
    }
    
    return scenarios;
}
