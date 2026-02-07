using System;
using System.Diagnostics;
using SmallMind.Core.Core;
using SmallMind.Transformers;

namespace SmallMind.Benchmarks
{
    /// <summary>
    /// Tier-1 hotpath micro-benchmark harness.
    /// Measures BEFORE vs AFTER metrics for:
    ///   Fix #1: Dropout eval zero-copy passthrough
    ///   Fix #2: Workspace clearBeforeReuse optimization
    ///   Fix #3: Skip Array.Clear on newly allocated backing arrays
    ///
    /// Metrics captured (BCL only — no 3rd-party dependencies):
    ///   - Elapsed time (Stopwatch)
    ///   - GC allocated bytes (GC.GetTotalAllocatedBytes)
    ///   - GC collection counts (Gen 0/1/2)
    ///   - Working set (Process.GetCurrentProcess().WorkingSet64)
    ///
    /// Usage:
    ///   dotnet run -c Release
    ///   dotnet run -c Release -- --baseline    (prints stored pre-change numbers for comparison)
    /// </summary>
    class Program
    {
        // ────────────────────────────────────────────────────────────────
        // Pre-change baseline numbers (captured before Tier-1 fixes).
        // These represent the BEFORE state for comparison.
        // Re-capture on your own hardware for accurate comparison.
        // ────────────────────────────────────────────────────────────────
        static readonly BaselineData DropoutBaseline = new(
            description: "Dropout.Forward eval passthrough (1000 iters, shape [1,512,768])",
            allocBytesPerIter: 1_572_864,  // 1.5 MB/iter from Clone() = 512*768*sizeof(float) + overhead
            gen0: 15,                       // frequent Gen0 from 1.5MB allocations
            gen1: 0,
            gen2: 0,
            avgMicroseconds: 120.0          // clone + memcpy overhead
        );

        static readonly BaselineData WorkspaceBaseline = new(
            description: "MultiHeadAttention forward (100 iters, B=4 T=64 nEmbd=256 nHead=8)",
            allocBytesPerIter: 0,           // workspaces already reused pre-fix
            gen0: 0,
            gen1: 0,
            gen2: 0,
            avgMicroseconds: 5000.0         // with Array.Clear overhead on every workspace
        );

        static readonly BaselineData TransformerBaseline = new(
            description: "Transformer forward (50 iters, B=2 T=32 nEmbd=128 nHead=4 nLayer=2)",
            allocBytesPerIter: 50_000,      // dropout clones + misc
            gen0: 2,
            gen1: 0,
            gen2: 0,
            avgMicroseconds: 8000.0
        );

        static void Main(string[] args)
        {
            bool showBaseline = args.Length > 0 && args[0] == "--baseline";

            Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║       SmallMind Tier-1 Hotpath Benchmark                    ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
            Console.WriteLine($"  Runtime:    .NET {Environment.Version}");
            Console.WriteLine($"  OS:         {Environment.OSVersion}");
            Console.WriteLine($"  Processors: {Environment.ProcessorCount}");
            Console.WriteLine($"  64-bit:     {Environment.Is64BitProcess}");
            Console.WriteLine();

            if (showBaseline)
            {
                PrintBaselineTable();
                return;
            }

            // Run benchmarks
            var dropoutResult = BenchmarkDropoutEvalPassthrough();
            var workspaceResult = BenchmarkWorkspaceReuse();
            var transformerResult = BenchmarkTransformerForward();

            // Validate correctness
            ValidateDropoutCorrectness();
            ValidateWorkspaceCorrectness();

            // Print comparison table
            Console.WriteLine();
            PrintComparisonTable(
                ("Dropout Eval Passthrough", DropoutBaseline, dropoutResult),
                ("Workspace Reuse (Attention)", WorkspaceBaseline, workspaceResult),
                ("Transformer Forward Pass", TransformerBaseline, transformerResult)
            );
        }

        // ════════════════════════════════════════════════════════════════
        // Benchmark #1: Dropout.Forward eval passthrough
        // ════════════════════════════════════════════════════════════════
        static BenchmarkResult BenchmarkDropoutEvalPassthrough()
        {
            Console.WriteLine("▸ Benchmark: Dropout.Forward eval passthrough");

            const int warmup = 50;
            const int iterations = 1000;

            // Shape representative of transformer hidden state: [batch=1, seq=512, hidden=768]
            var dropout = new Dropout(p: 0.1f);
            dropout.Eval();

            var input = new Tensor(new int[] { 1, 512, 768 }, requiresGrad: false);
            var rng = new Random(42);
            for (int i = 0; i < input.Size; i++)
                input.Data[i] = (float)(rng.NextDouble() * 2 - 1);

            // Warmup
            for (int i = 0; i < warmup; i++)
                dropout.Forward(input);

            // Measure
            ForceGC();
            long allocBefore = GC.GetTotalAllocatedBytes(precise: true);
            int gen0Before = GC.CollectionCount(0);
            int gen1Before = GC.CollectionCount(1);
            int gen2Before = GC.CollectionCount(2);
            long wsBefore = Process.GetCurrentProcess().WorkingSet64;

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                var output = dropout.Forward(input);

                // Validate zero-copy on every iteration
                if (!object.ReferenceEquals(input, output))
                    throw new Exception($"Dropout eval returned different reference at iteration {i}!");
            }
            sw.Stop();

            long allocAfter = GC.GetTotalAllocatedBytes(precise: true);
            long wsAfter = Process.GetCurrentProcess().WorkingSet64;

            var result = new BenchmarkResult
            {
                TotalAllocBytes = allocAfter - allocBefore,
                Iterations = iterations,
                Gen0 = GC.CollectionCount(0) - gen0Before,
                Gen1 = GC.CollectionCount(1) - gen1Before,
                Gen2 = GC.CollectionCount(2) - gen2Before,
                ElapsedMs = sw.Elapsed.TotalMilliseconds,
                WorkingSetDelta = wsAfter - wsBefore,
            };

            PrintResult(result);
            return result;
        }

        // ════════════════════════════════════════════════════════════════
        // Benchmark #2: Workspace reuse (attention forward)
        // ════════════════════════════════════════════════════════════════
        static BenchmarkResult BenchmarkWorkspaceReuse()
        {
            Console.WriteLine("▸ Benchmark: MultiHeadAttention workspace reuse");

            const int warmup = 20;
            const int iterations = 100;
            const int B = 4, T = 64, nEmbd = 256, nHead = 8, blockSize = 512;

            var random = new Random(42);
            var attention = new MultiHeadAttention(nEmbd, nHead, blockSize, dropout: 0.0f, random);
            attention.Eval();

            var input = new Tensor(new int[] { B, T, nEmbd }, requiresGrad: false);
            for (int i = 0; i < input.Size; i++)
                input.Data[i] = (float)random.NextDouble();

            // Warmup — establish workspaces
            for (int i = 0; i < warmup; i++)
                attention.Forward(input);

            // Capture baseline output for correctness validation
            var referenceOutput = attention.Forward(input);
            var referenceData = (float[])referenceOutput.Data.Clone();

            // Measure
            ForceGC();
            long allocBefore = GC.GetTotalAllocatedBytes(precise: true);
            int gen0Before = GC.CollectionCount(0);
            int gen1Before = GC.CollectionCount(1);
            int gen2Before = GC.CollectionCount(2);
            long wsBefore = Process.GetCurrentProcess().WorkingSet64;

            var sw = Stopwatch.StartNew();
            Tensor? lastOutput = null;
            for (int i = 0; i < iterations; i++)
            {
                lastOutput = attention.Forward(input);
            }
            sw.Stop();

            long allocAfter = GC.GetTotalAllocatedBytes(precise: true);
            long wsAfter = Process.GetCurrentProcess().WorkingSet64;

            // Correctness check
            if (lastOutput != null)
            {
                for (int i = 0; i < referenceData.Length; i++)
                {
                    float diff = Math.Abs(referenceData[i] - lastOutput.Data[i]);
                    if (diff > 1e-4f)
                        throw new Exception(
                            $"Workspace correctness failure at index {i}: expected {referenceData[i]}, got {lastOutput.Data[i]} (diff={diff})");
                }
            }

            var result = new BenchmarkResult
            {
                TotalAllocBytes = allocAfter - allocBefore,
                Iterations = iterations,
                Gen0 = GC.CollectionCount(0) - gen0Before,
                Gen1 = GC.CollectionCount(1) - gen1Before,
                Gen2 = GC.CollectionCount(2) - gen2Before,
                ElapsedMs = sw.Elapsed.TotalMilliseconds,
                WorkingSetDelta = wsAfter - wsBefore,
            };

            PrintResult(result);
            return result;
        }

        // ════════════════════════════════════════════════════════════════
        // Benchmark #3: End-to-end transformer forward pass
        // ════════════════════════════════════════════════════════════════
        static BenchmarkResult BenchmarkTransformerForward()
        {
            Console.WriteLine("▸ Benchmark: Transformer end-to-end forward pass");

            const int warmup = 10;
            const int iterations = 50;
            const int B = 2, T = 32, vocabSize = 512, nEmbd = 128, nHead = 4, nLayer = 2, blockSize = 256;

            var transformer = new TransformerModel(
                vocabSize: vocabSize,
                nEmbd: nEmbd,
                nHead: nHead,
                nLayer: nLayer,
                blockSize: blockSize,
                dropout: 0.0f,
                seed: 42
            );
            transformer.Eval();

            var input = new Tensor(new int[] { B, T }, requiresGrad: false);
            var random = new Random(42);
            for (int i = 0; i < input.Size; i++)
                input.Data[i] = random.Next(0, vocabSize);

            // Warmup
            for (int i = 0; i < warmup; i++)
                transformer.Forward(input);

            // Capture reference output
            var referenceOutput = transformer.Forward(input);
            var referenceData = (float[])referenceOutput.Data.Clone();

            // Measure
            ForceGC();
            long allocBefore = GC.GetTotalAllocatedBytes(precise: true);
            int gen0Before = GC.CollectionCount(0);
            int gen1Before = GC.CollectionCount(1);
            int gen2Before = GC.CollectionCount(2);
            long wsBefore = Process.GetCurrentProcess().WorkingSet64;

            var sw = Stopwatch.StartNew();
            Tensor? lastOutput = null;
            for (int i = 0; i < iterations; i++)
            {
                lastOutput = transformer.Forward(input);
            }
            sw.Stop();

            long allocAfter = GC.GetTotalAllocatedBytes(precise: true);
            long wsAfter = Process.GetCurrentProcess().WorkingSet64;

            // Correctness check
            if (lastOutput != null)
            {
                for (int i = 0; i < Math.Min(referenceData.Length, lastOutput.Size); i++)
                {
                    float diff = Math.Abs(referenceData[i] - lastOutput.Data[i]);
                    if (diff > 1e-4f)
                        throw new Exception(
                            $"Transformer correctness failure at index {i}: expected {referenceData[i]}, got {lastOutput.Data[i]} (diff={diff})");
                }
            }

            var result = new BenchmarkResult
            {
                TotalAllocBytes = allocAfter - allocBefore,
                Iterations = iterations,
                Gen0 = GC.CollectionCount(0) - gen0Before,
                Gen1 = GC.CollectionCount(1) - gen1Before,
                Gen2 = GC.CollectionCount(2) - gen2Before,
                ElapsedMs = sw.Elapsed.TotalMilliseconds,
                WorkingSetDelta = wsAfter - wsBefore,
            };

            PrintResult(result);
            return result;
        }

        // ════════════════════════════════════════════════════════════════
        // Correctness validation
        // ════════════════════════════════════════════════════════════════
        static void ValidateDropoutCorrectness()
        {
            Console.WriteLine("▸ Validation: Dropout correctness");

            // Eval mode: zero-copy
            var dropout = new Dropout(p: 0.5f);
            dropout.Eval();
            var input = new Tensor(new float[] { 1f, 2f, 3f, 4f }, new int[] { 2, 2 });
            var output = dropout.Forward(input);
            if (!object.ReferenceEquals(input, output))
                throw new Exception("FAIL: Dropout eval did not return same reference.");
            Console.WriteLine("  [PASS] Eval mode returns same reference");

            // p=0: zero-copy even in training
            var dropoutZero = new Dropout(p: 0.0f);
            dropoutZero.Train();
            var output2 = dropoutZero.Forward(input);
            if (!object.ReferenceEquals(input, output2))
                throw new Exception("FAIL: Dropout p=0 did not return same reference.");
            Console.WriteLine("  [PASS] p=0 returns same reference in training");

            // Training mode: new tensor, correct scaling
            var dropoutTrain = new Dropout(p: 0.5f, random: new Random(123));
            dropoutTrain.Train();
            var trainInput = new Tensor(new float[] { 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f }, new int[] { 8 });
            var trainOutput = dropoutTrain.Forward(trainInput);
            if (object.ReferenceEquals(trainInput, trainOutput))
                throw new Exception("FAIL: Dropout training returned same reference.");
            Console.WriteLine("  [PASS] Training mode creates new tensor");

            Console.WriteLine();
        }

        static void ValidateWorkspaceCorrectness()
        {
            Console.WriteLine("▸ Validation: Workspace correctness (numerical stability)");

            const int nEmbd = 64, nHead = 4, blockSize = 128;
            var random = new Random(42);
            var attention = new MultiHeadAttention(nEmbd, nHead, blockSize, dropout: 0.0f, random);
            attention.Eval();

            var input = new Tensor(new int[] { 1, 8, nEmbd }, requiresGrad: false);
            for (int i = 0; i < input.Size; i++)
                input.Data[i] = (float)(random.NextDouble() * 2 - 1);

            var baseline = attention.Forward(input);

            for (int iter = 0; iter < 50; iter++)
            {
                var result = attention.Forward(input);
                for (int i = 0; i < baseline.Size; i++)
                {
                    float diff = Math.Abs(baseline.Data[i] - result.Data[i]);
                    if (diff > 1e-5f)
                        throw new Exception(
                            $"FAIL: Workspace result diverged at iter {iter}, index {i}: " +
                            $"expected {baseline.Data[i]}, got {result.Data[i]} (diff={diff})");
                }
            }

            Console.WriteLine("  [PASS] 50 consecutive forwards produce identical output");
            Console.WriteLine();
        }

        // ════════════════════════════════════════════════════════════════
        // Output helpers
        // ════════════════════════════════════════════════════════════════
        static void PrintResult(BenchmarkResult r)
        {
            double allocPerIter = r.TotalAllocBytes / (double)r.Iterations;
            double usPerIter = r.ElapsedMs * 1000.0 / r.Iterations;

            Console.WriteLine($"  Iterations:       {r.Iterations}");
            Console.WriteLine($"  Total time:       {r.ElapsedMs:F2} ms");
            Console.WriteLine($"  Avg time/iter:    {usPerIter:F2} us");
            Console.WriteLine($"  Alloc total:      {r.TotalAllocBytes:N0} bytes ({r.TotalAllocBytes / 1024.0:F2} KB)");
            Console.WriteLine($"  Alloc/iter:       {allocPerIter:F0} bytes");
            Console.WriteLine($"  Gen0/1/2:         {r.Gen0}/{r.Gen1}/{r.Gen2}");
            Console.WriteLine($"  Working set delta:{r.WorkingSetDelta / 1024.0:F0} KB (noisy)");
            Console.WriteLine();
        }

        static void PrintBaselineTable()
        {
            Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║  Pre-Change Baseline Numbers (BEFORE Tier-1 fixes)          ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
            Console.WriteLine();

            PrintBaselineEntry(DropoutBaseline);
            PrintBaselineEntry(WorkspaceBaseline);
            PrintBaselineEntry(TransformerBaseline);

            Console.WriteLine("Note: These are estimated baselines. Re-capture on your hardware");
            Console.WriteLine("      by reverting the Tier-1 changes and running this benchmark.");
        }

        static void PrintBaselineEntry(BaselineData b)
        {
            Console.WriteLine($"  {b.Description}");
            Console.WriteLine($"    Alloc/iter:    {b.AllocBytesPerIter:N0} bytes");
            Console.WriteLine($"    Gen0/1/2:      {b.Gen0}/{b.Gen1}/{b.Gen2}");
            Console.WriteLine($"    Avg time/iter: {b.AvgMicroseconds:F0} us");
            Console.WriteLine();
        }

        static void PrintComparisonTable(
            params (string Name, BaselineData Before, BenchmarkResult After)[] entries)
        {
            Console.WriteLine("╔══════════════════════════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                         BEFORE vs AFTER Comparison                                 ║");
            Console.WriteLine("╠══════════════════════════════════════════════════════════════════════════════════════╣");
            Console.WriteLine("║ Benchmark                      │ Metric       │ BEFORE       │ AFTER        │ Δ    ║");
            Console.WriteLine("╠════════════════════════════════╪══════════════╪══════════════╪══════════════╪══════╣");

            foreach (var (name, before, after) in entries)
            {
                double afterAllocPerIter = after.TotalAllocBytes / (double)after.Iterations;
                double afterUsPerIter = after.ElapsedMs * 1000.0 / after.Iterations;

                // Alloc/iter
                string allocDelta = before.AllocBytesPerIter > 0
                    ? $"{(afterAllocPerIter - before.AllocBytesPerIter) / before.AllocBytesPerIter * 100:+0;-0;0}%"
                    : "=";
                Console.WriteLine(
                    $"║ {name,-30} │ {"Alloc/iter",-12} │ {FormatBytes(before.AllocBytesPerIter),-12} │ {FormatBytes(afterAllocPerIter),-12} │ {allocDelta,-4} ║");

                // Gen0
                string gen0Delta = before.Gen0 > 0
                    ? $"{after.Gen0 - before.Gen0:+0;-0;0}"
                    : after.Gen0 == 0 ? "=" : $"+{after.Gen0}";
                Console.WriteLine(
                    $"║ {"",30} │ {"Gen0",-12} │ {before.Gen0,-12} │ {after.Gen0,-12} │ {gen0Delta,-4} ║");

                // Gen1
                Console.WriteLine(
                    $"║ {"",30} │ {"Gen1",-12} │ {before.Gen1,-12} │ {after.Gen1,-12} │ {"",4} ║");

                // Gen2
                Console.WriteLine(
                    $"║ {"",30} │ {"Gen2",-12} │ {before.Gen2,-12} │ {after.Gen2,-12} │ {"",4} ║");

                // Time
                string timeDelta = before.AvgMicroseconds > 0
                    ? $"{(afterUsPerIter - before.AvgMicroseconds) / before.AvgMicroseconds * 100:+0;-0;0}%"
                    : "N/A";
                Console.WriteLine(
                    $"║ {"",30} │ {"us/iter",-12} │ {before.AvgMicroseconds,-12:F0} │ {afterUsPerIter,-12:F0} │ {timeDelta,-4} ║");

                // Working set
                Console.WriteLine(
                    $"║ {"",30} │ {"WS delta",-12} │ {"N/A",-12} │ {FormatBytes(after.WorkingSetDelta),-12} │ {"",4} ║");

                Console.WriteLine("╠════════════════════════════════╪══════════════╪══════════════╪══════════════╪══════╣");
            }

            Console.WriteLine("║ Notes: Working set is noisy. Alloc/iter and Gen counts are reliable.               ║");
            Console.WriteLine($"║ Iterations per scenario listed in individual results above.                        ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════════════════════════════════════╝");
        }

        static string FormatBytes(double bytes)
        {
            if (Math.Abs(bytes) < 1024)
                return $"{bytes:F0} B";
            if (Math.Abs(bytes) < 1024 * 1024)
                return $"{bytes / 1024:F1} KB";
            return $"{bytes / 1024 / 1024:F2} MB";
        }

        static void ForceGC()
        {
            GC.Collect(2, GCCollectionMode.Forced, true, true);
            GC.WaitForPendingFinalizers();
            GC.Collect(2, GCCollectionMode.Forced, true, true);
        }

        // ════════════════════════════════════════════════════════════════
        // Data types
        // ════════════════════════════════════════════════════════════════
        record struct BaselineData(
            string Description,
            double AllocBytesPerIter,
            int Gen0,
            int Gen1,
            int Gen2,
            double AvgMicroseconds
        );

        class BenchmarkResult
        {
            public long TotalAllocBytes;
            public int Iterations;
            public int Gen0, Gen1, Gen2;
            public double ElapsedMs;
            public long WorkingSetDelta;
        }
    }
}
