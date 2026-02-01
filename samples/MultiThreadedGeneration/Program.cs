using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SmallMind.Core.Core;
using SmallMind.Runtime;
using SmallMind.Tokenizers;
using SmallMind.Transformers;

namespace SmallMind.Samples.MultiThreadedGeneration
{
    /// <summary>
    /// Multi-threaded text generation sample demonstrating thread-safe concurrent inference.
    /// Pure C# implementation with no third-party dependencies.
    /// </summary>
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine("   SmallMind Multi-Threaded Generation Sample");
            Console.WriteLine("═══════════════════════════════════════════════════════════\n");

            // Initialize model (shared across threads for inference)
            Console.WriteLine("Initializing model...");
            var model = InitializeModel();
            var tokenizer = CreateTokenizer();
            var sampling = new Sampling(model, tokenizer, model.BlockSize);

            Console.WriteLine($"Model: {model.NumLayers} layers, {model.EmbedDim} dims, {model.NumHeads} heads");
            Console.WriteLine($"Vocab size: {tokenizer.VocabSize}, Block size: {model.BlockSize}\n");

            // Scenario selection
            while (true)
            {
                Console.WriteLine("\n═══════════════════════════════════════════════════════════");
                Console.WriteLine("Select a scenario:");
                Console.WriteLine("  1. Basic Concurrent Generation");
                Console.WriteLine("  2. Batch Processing with Progress");
                Console.WriteLine("  3. Performance Benchmark");
                Console.WriteLine("  0. Exit");
                Console.WriteLine("═══════════════════════════════════════════════════════════");
                Console.Write("\nChoice: ");
                
                var choice = Console.ReadLine();
                Console.WriteLine();

                switch (choice)
                {
                    case "1":
                        await Scenario1_BasicConcurrent(model, tokenizer, sampling);
                        break;
                    case "2":
                        await Scenario2_BatchProcessing(model, tokenizer, sampling);
                        break;
                    case "3":
                        await Scenario3_PerformanceBenchmark(model, tokenizer, sampling);
                        break;
                    case "0":
                        Console.WriteLine("Exiting...");
                        return;
                    default:
                        Console.WriteLine("Invalid choice. Please try again.");
                        break;
                }
            }
        }

        /// <summary>
        /// Scenario 1: Basic concurrent generation with multiple prompts.
        /// Demonstrates thread-safety of model inference.
        /// </summary>
        static async Task Scenario1_BasicConcurrent(
            TransformerModel model, 
            ITokenizer tokenizer, 
            Sampling sampling)
        {
            Console.WriteLine("───────────────────────────────────────────────────────────");
            Console.WriteLine("SCENARIO 1: Basic Concurrent Generation");
            Console.WriteLine("───────────────────────────────────────────────────────────\n");

            var prompts = new[]
            {
                "The future of AI",
                "Once upon a time",
                "In the year 2050",
                "The quick brown fox"
            };

            int maxTokens = 30;
            double temperature = 0.8;
            int topK = 20;

            Console.WriteLine($"Generating {prompts.Length} completions concurrently...");
            Console.WriteLine($"Parameters: maxTokens={maxTokens}, temperature={temperature}, topK={topK}\n");

            var stopwatch = Stopwatch.StartNew();

            // Process all prompts concurrently
            var tasks = prompts.Select(async (prompt, index) =>
            {
                var threadId = Environment.CurrentManagedThreadId;
                var result = await Task.Run(() =>
                {
                    // Thread-safe generation using the same model instance
                    lock (model) // Simple locking for demonstration
                    {
                        return sampling.Generate(
                            prompt,
                            maxTokens,
                            temperature,
                            topK,
                            seed: index, // Different seed for each prompt
                            showPerf: false,
                            isPerfJsonMode: true);
                    }
                });

                return new { Index = index, Prompt = prompt, Result = result, ThreadId = threadId };
            }).ToArray();

            var results = await Task.WhenAll(tasks);
            stopwatch.Stop();

            // Display results
            Console.WriteLine("Results:");
            Console.WriteLine("─────────────────────────────────────────────────────────\n");

            foreach (var result in results.OrderBy(r => r.Index))
            {
                Console.WriteLine($"[{result.Index + 1}] Thread {result.ThreadId}:");
                Console.WriteLine($"    Prompt: \"{result.Prompt}\"");
                Console.WriteLine($"    Output: \"{result.Result}\"");
                Console.WriteLine();
            }

            Console.WriteLine($"Total time: {stopwatch.ElapsedMilliseconds}ms");
            Console.WriteLine($"Average time per completion: {stopwatch.ElapsedMilliseconds / prompts.Length}ms");
        }

        /// <summary>
        /// Scenario 2: Batch processing with progress tracking.
        /// Demonstrates practical batch inference with visual feedback.
        /// </summary>
        static async Task Scenario2_BatchProcessing(
            TransformerModel model, 
            ITokenizer tokenizer, 
            Sampling sampling)
        {
            Console.WriteLine("───────────────────────────────────────────────────────────");
            Console.WriteLine("SCENARIO 2: Batch Processing with Progress");
            Console.WriteLine("───────────────────────────────────────────────────────────\n");

            // Generate a batch of prompts
            var batchSize = 12;
            var prompts = GenerateSamplePrompts(batchSize);

            Console.Write("Enter max tokens per generation (default 25): ");
            var input = Console.ReadLine();
            int maxTokens = string.IsNullOrWhiteSpace(input) ? 25 : int.Parse(input);

            Console.Write("Enter concurrency level (default 3): ");
            input = Console.ReadLine();
            int concurrency = string.IsNullOrWhiteSpace(input) ? 3 : int.Parse(input);

            Console.WriteLine($"\nProcessing {batchSize} prompts with concurrency={concurrency}...\n");

            var processor = new BatchProcessor<string, string>(
                concurrency,
                async (prompt, index, cancellationToken) =>
                {
                    return await Task.Run(() =>
                    {
                        lock (model)
                        {
                            return sampling.Generate(
                                prompt,
                                maxTokens,
                                temperature: 0.7,
                                topK: 30,
                                seed: index,
                                showPerf: false,
                                isPerfJsonMode: true);
                        }
                    }, cancellationToken);
                });

            var stopwatch = Stopwatch.StartNew();
            var results = await processor.ProcessBatchAsync(prompts, CancellationToken.None);
            stopwatch.Stop();

            Console.WriteLine("\n\nResults:");
            Console.WriteLine("─────────────────────────────────────────────────────────\n");

            for (int i = 0; i < prompts.Count && i < results.Count; i++)
            {
                Console.WriteLine($"[{i + 1}] {prompts[i]}");
                Console.WriteLine($"    → {results[i]}");
                Console.WriteLine();
            }

            Console.WriteLine($"Processed {results.Count} items in {stopwatch.ElapsedMilliseconds}ms");
            Console.WriteLine($"Throughput: {results.Count * 1000.0 / stopwatch.ElapsedMilliseconds:F2} items/sec");
        }

        /// <summary>
        /// Scenario 3: Performance benchmark comparing different concurrency levels.
        /// </summary>
        static async Task Scenario3_PerformanceBenchmark(
            TransformerModel model, 
            ITokenizer tokenizer, 
            Sampling sampling)
        {
            Console.WriteLine("───────────────────────────────────────────────────────────");
            Console.WriteLine("SCENARIO 3: Performance Benchmark");
            Console.WriteLine("───────────────────────────────────────────────────────────\n");

            var prompts = GenerateSamplePrompts(20);
            int maxTokens = 20;
            var concurrencyLevels = new[] { 1, 2, 4, 8 };

            Console.WriteLine($"Benchmark: {prompts.Count} prompts, {maxTokens} tokens each");
            Console.WriteLine($"Testing concurrency levels: {string.Join(", ", concurrencyLevels)}\n");

            var benchmarkResults = new List<BenchmarkResult>();

            foreach (var concurrency in concurrencyLevels)
            {
                Console.WriteLine($"Testing concurrency={concurrency}...");

                var processor = new BatchProcessor<string, string>(
                    concurrency,
                    async (prompt, index, cancellationToken) =>
                    {
                        return await Task.Run(() =>
                        {
                            lock (model)
                            {
                                return sampling.Generate(
                                    prompt,
                                    maxTokens,
                                    temperature: 0.8,
                                    topK: 20,
                                    seed: index,
                                    showPerf: false,
                                    isPerfJsonMode: true);
                            }
                        }, cancellationToken);
                    });

                var stopwatch = Stopwatch.StartNew();
                var results = await processor.ProcessBatchAsync(prompts, CancellationToken.None);
                stopwatch.Stop();

                var result = new BenchmarkResult
                {
                    Concurrency = concurrency,
                    TotalTimeMs = stopwatch.ElapsedMilliseconds,
                    ItemsProcessed = results.Count,
                    Throughput = results.Count * 1000.0 / stopwatch.ElapsedMilliseconds
                };

                benchmarkResults.Add(result);
                Console.WriteLine($"  Completed in {result.TotalTimeMs}ms");
            }

            Console.WriteLine("\n\n═══════════════════════════════════════════════════════════");
            Console.WriteLine("Benchmark Results");
            Console.WriteLine("═══════════════════════════════════════════════════════════\n");
            Console.WriteLine("Concurrency | Time (ms) | Throughput (items/s) | Speedup");
            Console.WriteLine("─────────────────────────────────────────────────────────");

            var baseline = benchmarkResults[0].TotalTimeMs;
            foreach (var result in benchmarkResults)
            {
                var speedup = baseline / (double)result.TotalTimeMs;
                Console.WriteLine($"     {result.Concurrency,2}     | {result.TotalTimeMs,9:F0} | {result.Throughput,20:F2} | {speedup,7:F2}x");
            }

            Console.WriteLine("\nNote: Speedup is relative to concurrency=1 (baseline)");
        }

        /// <summary>
        /// Initialize a small transformer model for demonstration.
        /// </summary>
        static TransformerModel InitializeModel()
        {
            return new TransformerModelBuilder()
                .UseTinyConfig()
                .WithVocabSize(128) // Small vocab for fast demo
                .WithBlockSize(64)  // Reduced context window
                .WithDropout(0.0)   // No dropout for inference
                .WithSeed(12345)
                .Build();
        }

        /// <summary>
        /// Create a character tokenizer with a basic alphabet.
        /// </summary>
        static ITokenizer CreateTokenizer()
        {
            // Create a sample text corpus for the tokenizer
            var sampleText = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 .,;:!?-\n";
            return new CharTokenizer(sampleText);
        }

        /// <summary>
        /// Generate sample prompts for batch processing.
        /// </summary>
        static List<string> GenerateSamplePrompts(int count)
        {
            var templates = new[]
            {
                "The future of",
                "In a world where",
                "Scientists discover",
                "Breaking news:",
                "Once upon a time",
                "The secret to",
                "How to build",
                "Why do we",
                "The story of",
                "Imagine if"
            };

            var prompts = new List<string>();
            for (int i = 0; i < count; i++)
            {
                prompts.Add(templates[i % templates.Length]);
            }
            return prompts;
        }

        /// <summary>
        /// Benchmark result data.
        /// </summary>
        class BenchmarkResult
        {
            public int Concurrency { get; set; }
            public long TotalTimeMs { get; set; }
            public int ItemsProcessed { get; set; }
            public double Throughput { get; set; }
        }
    }

    /// <summary>
    /// Batch processor for concurrent operations with controlled parallelism.
    /// Thread-safe implementation using semaphore for concurrency control.
    /// </summary>
    public class BatchProcessor<TInput, TOutput>
    {
        private readonly int _maxConcurrency;
        private readonly Func<TInput, int, CancellationToken, Task<TOutput>> _processFunc;

        public BatchProcessor(
            int maxConcurrency,
            Func<TInput, int, CancellationToken, Task<TOutput>> processFunc)
        {
            if (maxConcurrency <= 0)
                throw new ArgumentException("Concurrency must be greater than 0", nameof(maxConcurrency));

            _maxConcurrency = maxConcurrency;
            _processFunc = processFunc ?? throw new ArgumentNullException(nameof(processFunc));
        }

        /// <summary>
        /// Process a batch of items with controlled concurrency and progress reporting.
        /// </summary>
        public async Task<List<TOutput>> ProcessBatchAsync(
            List<TInput> items,
            CancellationToken cancellationToken)
        {
            if (items == null || items.Count == 0)
                return new List<TOutput>();

            var results = new ConcurrentDictionary<int, TOutput>();
            var semaphore = new SemaphoreSlim(_maxConcurrency, _maxConcurrency);
            var progressBar = new ProgressBar(items.Count);

            try
            {
                var tasks = items.Select(async (item, index) =>
                {
                    await semaphore.WaitAsync(cancellationToken);
                    try
                    {
                        var result = await _processFunc(item, index, cancellationToken);
                        results[index] = result;
                        progressBar.Increment();
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }).ToArray();

                await Task.WhenAll(tasks);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("\nProcessing cancelled.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nError during batch processing: {ex.Message}");
            }

            progressBar.Complete();

            // Convert results back to ordered list
            var orderedResults = new List<TOutput>(items.Count);
            for (int i = 0; i < items.Count; i++)
            {
                if (results.TryGetValue(i, out var result))
                {
                    orderedResults.Add(result);
                }
            }

            return orderedResults;
        }
    }

    /// <summary>
    /// Simple console progress bar for tracking batch processing.
    /// Pure C# implementation with no external dependencies.
    /// </summary>
    public class ProgressBar
    {
        private readonly int _total;
        private int _current;
        private readonly object _lock = new object();
        private readonly int _barWidth = 50;
        private DateTime _startTime;

        public ProgressBar(int total)
        {
            if (total <= 0)
                throw new ArgumentException("Total must be greater than 0", nameof(total));

            _total = total;
            _current = 0;
            _startTime = DateTime.Now;
            Render();
        }

        /// <summary>
        /// Increment the progress counter.
        /// </summary>
        public void Increment()
        {
            lock (_lock)
            {
                _current++;
                Render();
            }
        }

        /// <summary>
        /// Mark processing as complete.
        /// </summary>
        public void Complete()
        {
            lock (_lock)
            {
                _current = _total;
                Render();
                Console.WriteLine(); // Move to next line
            }
        }

        /// <summary>
        /// Render the progress bar to console.
        /// </summary>
        private void Render()
        {
            var percentage = (double)_current / _total;
            var filled = (int)(percentage * _barWidth);
            var empty = _barWidth - filled;

            var elapsed = DateTime.Now - _startTime;
            var rate = _current > 0 ? _current / elapsed.TotalSeconds : 0;
            var eta = rate > 0 ? TimeSpan.FromSeconds((_total - _current) / rate) : TimeSpan.Zero;

            var bar = new string('█', filled) + new string('░', empty);
            var stats = $"{_current}/{_total} ({percentage:P0}) | {rate:F1} items/s | ETA: {eta:mm\\:ss}";

            // Clear line and write progress
            Console.Write($"\r[{bar}] {stats}");
        }
    }
}
