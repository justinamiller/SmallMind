using System;
using SmallMind.Abstractions;
using SmallMind.Runtime.Cache;

namespace SmallMind.Examples
{
    /// <summary>
    /// Example demonstrating KV Cache Budget features.
    /// Shows per-session memory limits, LRU eviction, and budget telemetry.
    /// </summary>
    class KvCacheBudgetExample
    {
        static void Main(string[] args)
        {
            Console.WriteLine("SmallMind KV Cache Budget Example");
            Console.WriteLine("==================================");
            Console.WriteLine();

            // Example 1: Per-Session Budget Configuration
            Console.WriteLine("Example 1: Configuring Per-Session Budgets");
            Console.WriteLine("-------------------------------------------");
            DemonstratePerSessionBudget();
            Console.WriteLine();

            // Example 2: LRU Eviction with Telemetry
            Console.WriteLine("Example 2: LRU Eviction with Telemetry");
            Console.WriteLine("---------------------------------------");
            DemonstrateLruEvictionWithTelemetry();
            Console.WriteLine();

            // Example 3: Budget Exceeded Handling
            Console.WriteLine("Example 3: Budget Exceeded Handling");
            Console.WriteLine("------------------------------------");
            DemonstrateBudgetExceeded();
            Console.WriteLine();

            Console.WriteLine("All examples completed!");
        }

        static void DemonstratePerSessionBudget()
        {
            Console.WriteLine("// Configure KV cache with per-session memory budgets");
            Console.WriteLine("var options = new KvCacheOptions");
            Console.WriteLine("{");
            Console.WriteLine("    MaxBytesPerSession = 100L * 1024 * 1024,  // 100MB per session");
            Console.WriteLine("    MaxBytesTotal = 1024L * 1024 * 1024,      // 1GB total cache");
            Console.WriteLine("    MaxSessions = 100,                         // Max 100 concurrent sessions");
            Console.WriteLine("    Policy = KvEvictionPolicy.LRU");
            Console.WriteLine("};");
            Console.WriteLine();

            Console.WriteLine("// Create store with budget telemetry");
            Console.WriteLine("var telemetry = new ConsoleTelemetry();");
            Console.WriteLine("var store = new LruKvCacheStore(options, telemetry);");
            Console.WriteLine();

            Console.WriteLine("Benefits:");
            Console.WriteLine("  - Per-session budget prevents any single session from consuming all memory");
            Console.WriteLine("  - Global budget ensures total memory stays bounded");
            Console.WriteLine("  - LRU eviction removes least recently used sessions when limits reached");
        }

        static void DemonstrateLruEvictionWithTelemetry()
        {
            Console.WriteLine("// Create cache with tight budget to trigger evictions");
            Console.WriteLine("var options = new KvCacheOptions");
            Console.WriteLine("{");
            Console.WriteLine("    MaxBytesPerSession = 50L * 1024 * 1024,   // 50MB per session");
            Console.WriteLine("    MaxBytesTotal = 120L * 1024 * 1024,       // 120MB total (2-3 sessions)");
            Console.WriteLine("    MaxSessions = 10");
            Console.WriteLine("};");
            Console.WriteLine();

            Console.WriteLine("var telemetry = new ConsoleTelemetry();");
            Console.WriteLine("var store = new LruKvCacheStore(options, telemetry);");
            Console.WriteLine();

            Console.WriteLine("// Create multiple sessions - will trigger evictions");
            Console.WriteLine("var session1 = store.GetOrCreate(new SessionId(\"session-1\"), modelShape, 2048);");
            Console.WriteLine("var session2 = store.GetOrCreate(new SessionId(\"session-2\"), modelShape, 2048);");
            Console.WriteLine("var session3 = store.GetOrCreate(new SessionId(\"session-3\"), modelShape, 2048);");
            Console.WriteLine();

            Console.WriteLine("Console output (from telemetry):");
            Console.WriteLine("[EVICTION] Session 'session-1' evicted (LRU eviction): freed 50MB");
            Console.WriteLine();

            Console.WriteLine("// Check statistics");
            Console.WriteLine("var stats = store.GetStats();");
            Console.WriteLine("Console.WriteLine($\"Evictions: {stats.Evictions}\");");
            Console.WriteLine("Console.WriteLine($\"Current memory: {stats.CurrentBytes / 1024 / 1024}MB\");");
            Console.WriteLine("Console.WriteLine($\"Peak memory: {stats.PeakBytes / 1024 / 1024}MB\");");
        }

        static void DemonstrateBudgetExceeded()
        {
            Console.WriteLine("// Configure small per-session budget");
            Console.WriteLine("var options = new KvCacheOptions");
            Console.WriteLine("{");
            Console.WriteLine("    MaxBytesPerSession = 10L * 1024 * 1024,   // Only 10MB per session");
            Console.WriteLine("    MaxBytesTotal = 1024L * 1024 * 1024");
            Console.WriteLine("};");
            Console.WriteLine();

            Console.WriteLine("var telemetry = new ConsoleTelemetry();");
            Console.WriteLine("var store = new LruKvCacheStore(options, telemetry);");
            Console.WriteLine();

            Console.WriteLine("// Try to create a large cache that exceeds budget");
            Console.WriteLine("try");
            Console.WriteLine("{");
            Console.WriteLine("    // Large model: 24 layers, 32 heads, 128 head dim, 4096 tokens");
            Console.WriteLine("    var session = store.GetOrCreate(");
            Console.WriteLine("        new SessionId(\"large-session\"),");
            Console.WriteLine("        new ModelShape(24, 32, 128),");
            Console.WriteLine("        maxTokens: 4096);");
            Console.WriteLine("}");
            Console.WriteLine("catch (InvalidOperationException ex)");
            Console.WriteLine("{");
            Console.WriteLine("    Console.WriteLine($\"Error: {ex.Message}\");");
            Console.WriteLine("    // Error: Session cache size 201MB exceeds per-session budget 10MB");
            Console.WriteLine("}");
            Console.WriteLine();

            Console.WriteLine("Telemetry output:");
            Console.WriteLine("[large-session] KV cache budget EXCEEDED: 201MB / 10MB");
            Console.WriteLine();

            Console.WriteLine("Solution: Increase MaxBytesPerSession or use smaller model/tokens");
        }
    }
}
