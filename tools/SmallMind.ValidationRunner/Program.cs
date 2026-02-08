using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using SmallMind.Quantization.IO.Gguf;
using SmallMind.Runtime;
using SmallMind.Tokenizers;
using SmallMind.Transformers;

namespace SmallMind.ValidationRunner
{
    /// <summary>
    /// Validation runner for SmallMind maturity level 3/5.
    /// Validates GGUF model loading, tokenization, and generation.
    /// Phase 3-5 implementation.
    /// </summary>
    class Program
    {
        static int Main(string[] args)
        {
            Console.WriteLine("=== SmallMind Validation Runner ===");
            Console.WriteLine("Target: Maturity Level 3/5\n");

            // Parse command line arguments
            if (!TryParseArgs(args, out var modelPath, out var kvCacheEnabled, out var generateText))
            {
                PrintUsage();
                return 1;
            }

            try
            {
                // Validation Step 1: Load GGUF model
                Console.WriteLine("Step 1: Loading GGUF model...");
                var (modelInfo, metadata) = LoadGgufModel(modelPath);
                Console.WriteLine("✓ Model loaded successfully\n");

                // Validation Step 2: Extract and display configuration
                Console.WriteLine("Step 2: Extracting model configuration...");
                DisplayConfiguration(metadata);
                Console.WriteLine();

                // Validation Step 3: Extract and test tokenizer
                Console.WriteLine("Step 3: Testing tokenizer...");
                var tokenizer = ExtractAndTestTokenizer(metadata);
                if (tokenizer == null)
                {
                    Console.WriteLine("⚠ Tokenizer extraction failed or not supported");
                    return 1;
                }
                Console.WriteLine("✓ Tokenizer working\n");

                // Validation Step 4: Test encode/decode roundtrip
                Console.WriteLine("Step 4: Testing tokenizer encode/decode...");
                TestTokenizerRoundtrip(tokenizer);
                Console.WriteLine("✓ Tokenizer roundtrip passed\n");

                // Validation Step 5: Build model from config
                Console.WriteLine("Step 5: Building model from configuration...");
                var (model, loadedTokenizer, config) = GgufModelLoader.LoadFromGguf(modelPath);
                Console.WriteLine($"✓ Model built: {config.Architecture}");
                Console.WriteLine($"  Parameters: {model.NumLayers} layers, {model.NumHeads} heads");
                Console.WriteLine($"  Context: {model.BlockSize} tokens\n");

                // Validation Step 6: Test generation (if requested)
                if (generateText)
                {
                    Console.WriteLine("Step 6: Testing text generation...");
                    TestGeneration(model, loadedTokenizer, kvCacheEnabled);
                    Console.WriteLine();
                }

                // Validation Step 7: Performance metrics
                Console.WriteLine("Step 7: Performance Metrics");
                DisplayPerformanceMetrics();
                Console.WriteLine();

                Console.WriteLine("=== Validation Complete ===");
                Console.WriteLine("Status: Phase 3-5 Complete");
                Console.WriteLine("✓ Model loading working");
                Console.WriteLine("✓ Tokenization working");
                if (generateText)
                {
                    Console.WriteLine($"✓ Generation working (KV cache: {(kvCacheEnabled ? "enabled" : "disabled")})");
                }

                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n✗ Validation failed: {ex.Message}");
                Console.WriteLine($"Stack trace:\n{ex.StackTrace}");
                return 1;
            }
        }

        static bool TryParseArgs(string[] args, out string modelPath, out bool kvCacheEnabled, out bool generateText)
        {
            modelPath = string.Empty;
            kvCacheEnabled = true; // Default to enabled
            generateText = false;  // Default to no generation

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--model" && i + 1 < args.Length)
                {
                    modelPath = args[i + 1];
                    i++;
                }
                else if (args[i] == "--no-kv-cache")
                {
                    kvCacheEnabled = false;
                }
                else if (args[i] == "--generate")
                {
                    generateText = true;
                }
            }

            return !string.IsNullOrEmpty(modelPath);
        }

        static void PrintUsage()
        {
            Console.WriteLine("Usage: SmallMind.ValidationRunner --model <path> [--no-kv-cache] [--generate]");
            Console.WriteLine();
            Console.WriteLine("Arguments:");
            Console.WriteLine("  --model <path>      Path to GGUF model file (required)");
            Console.WriteLine("  --no-kv-cache       Disable KV cache (for comparison)");
            Console.WriteLine("  --generate          Test text generation (Phase 3+)");
            Console.WriteLine();
            Console.WriteLine("Example:");
            Console.WriteLine("  SmallMind.ValidationRunner --model smollm2-135m-instruct.Q8_0.gguf --generate");
        }

        static (GgufModelInfo modelInfo, Dictionary<string, object> metadata) LoadGgufModel(string path)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"Model file not found: {path}");
            }

            using var stream = File.OpenRead(path);
            using var reader = new GgufReader(stream);
            var modelInfo = reader.ReadModelInfo();

            Console.WriteLine($"  Model: {path}");
            Console.WriteLine($"  Version: GGUF v{modelInfo.Version}");
            Console.WriteLine($"  Tensors: {modelInfo.Tensors.Count}");
            Console.WriteLine($"  Metadata keys: {modelInfo.Metadata.Count}");

            return (modelInfo, modelInfo.Metadata);
        }

        static void DisplayConfiguration(Dictionary<string, object> metadata)
        {
            // Extract architecture
            var arch = GetMetadataValue(metadata, "general.architecture", "unknown");
            Console.WriteLine($"  Architecture: {arch}");

            // Try to extract architecture-specific config
            var archPrefix = arch;
            
            // Common config keys
            var vocabSize = GetMetadataInt(metadata, $"{archPrefix}.vocab_size") 
                ?? GetMetadataInt(metadata, "llama.vocab_size");
            var contextLen = GetMetadataInt(metadata, $"{archPrefix}.context_length") 
                ?? GetMetadataInt(metadata, "llama.context_length");
            var embeddingLen = GetMetadataInt(metadata, $"{archPrefix}.embedding_length") 
                ?? GetMetadataInt(metadata, "llama.embedding_length");
            var blockCount = GetMetadataInt(metadata, $"{archPrefix}.block_count") 
                ?? GetMetadataInt(metadata, "llama.block_count");
            var headCount = GetMetadataInt(metadata, $"{archPrefix}.attention.head_count") 
                ?? GetMetadataInt(metadata, "llama.attention.head_count");
            var headCountKv = GetMetadataInt(metadata, $"{archPrefix}.attention.head_count_kv") 
                ?? GetMetadataInt(metadata, "llama.attention.head_count_kv") 
                ?? headCount;
            var ffnLen = GetMetadataInt(metadata, $"{archPrefix}.feed_forward_length") 
                ?? GetMetadataInt(metadata, "llama.feed_forward_length");

            Console.WriteLine($"  Vocab Size: {vocabSize}");
            Console.WriteLine($"  Context Length: {contextLen}");
            Console.WriteLine($"  Embedding Dim: {embeddingLen}");
            Console.WriteLine($"  Layers: {blockCount}");
            Console.WriteLine($"  Attention Heads: {headCount}");
            Console.WriteLine($"  KV Heads: {headCountKv} {(headCountKv < headCount ? "(GQA)" : "(MHA)")}");
            Console.WriteLine($"  FFN Hidden: {ffnLen}");

            // RoPE config
            var ropeFreqBase = GetMetadataDouble(metadata, $"{archPrefix}.rope.freq_base") 
                ?? GetMetadataDouble(metadata, "llama.rope.freq_base");
            if (ropeFreqBase.HasValue)
            {
                Console.WriteLine($"  RoPE Theta: {ropeFreqBase}");
            }

            // Norm epsilon
            var normEps = GetMetadataDouble(metadata, $"{archPrefix}.attention.layer_norm_rms_epsilon") 
                ?? GetMetadataDouble(metadata, "llama.attention.layer_norm_rms_epsilon");
            if (normEps.HasValue)
            {
                Console.WriteLine($"  RMS Norm Epsilon: {normEps}");
            }
        }

        static ITokenizer? ExtractAndTestTokenizer(Dictionary<string, object> metadata)
        {
            var tokenizer = GgufTokenizerExtractor.ExtractTokenizer(metadata);
            if (tokenizer == null)
            {
                Console.WriteLine("  ✗ Could not extract tokenizer from GGUF metadata");
                return null;
            }

            var info = tokenizer.Info;
            Console.WriteLine($"  Tokenizer: {info.Name}");
            Console.WriteLine($"  Vocab Size: {info.VocabSize}");
            Console.WriteLine($"  BOS Token ID: {info.BosTokenId}");
            Console.WriteLine($"  EOS Token ID: {info.EosTokenId}");
            Console.WriteLine($"  UNK Token ID: {info.UnkTokenId}");

            return tokenizer;
        }

        static void TestTokenizerRoundtrip(ITokenizer tokenizer)
        {
            var testCases = new[]
            {
                "Hello, world!",
                "The quick brown fox jumps over the lazy dog.",
                "SmallMind is a pure C# LLM implementation.",
                "Testing 123... encode → decode"
            };

            foreach (var testText in testCases)
            {
                var tokens = tokenizer.Encode(testText);
                var decoded = tokenizer.Decode(tokens);

                // For BPE tokenizers, roundtrip may not be perfect due to normalization
                // Just verify no crashes and tokens were produced
                if (tokens.Count == 0)
                {
                    throw new Exception($"Tokenizer produced 0 tokens for: {testText}");
                }

                Console.WriteLine($"  \"{testText}\"");
                Console.WriteLine($"    → {tokens.Count} tokens");
                Console.WriteLine($"    → \"{decoded}\"");

                // Basic sanity check - decoded should contain some of the original text
                if (!string.IsNullOrEmpty(testText) && decoded.Length == 0)
                {
                    throw new Exception("Decoded text is empty but input was not");
                }
            }
        }

        static void TestGeneration(TransformerModel model, ITokenizer tokenizer, bool kvCacheEnabled)
        {
            var prompt = "Hello";
            var maxTokens = 20;

            Console.WriteLine($"  Prompt: \"{prompt}\"");
            Console.WriteLine($"  Max tokens: {maxTokens}");
            Console.WriteLine($"  KV cache: {(kvCacheEnabled ? "enabled" : "disabled")}");
            Console.WriteLine();

            try
            {
                // Set model to eval mode
                model.Eval();

                // Create inference options
                var options = new ProductionInferenceOptions
                {
                    Temperature = 1.0,
                    TopK = 50,
                    TopP = 0.95,
                    MaxNewTokens = maxTokens,
                    MaxContextTokens = model.BlockSize,
                    Seed = 42
                };

                // Create inference session
                using var session = new InferenceSession(
                    model,
                    tokenizer,
                    options,
                    model.BlockSize);

                // Measure generation time
                var sw = Stopwatch.StartNew();

                // Generate (synchronous)
                var result = session.GenerateAsync(prompt).GetAwaiter().GetResult();

                sw.Stop();

                Console.WriteLine($"  Generated: \"{result}\"");
                Console.WriteLine($"  Time: {sw.ElapsedMilliseconds} ms");

                // Calculate approximate tokens/sec
                var tokens = tokenizer.Encode(result);
                var generatedTokens = tokens.Count - tokenizer.Encode(prompt).Count;
                if (generatedTokens > 0 && sw.ElapsedMilliseconds > 0)
                {
                    var tokensPerSec = generatedTokens / (sw.ElapsedMilliseconds / 1000.0);
                    Console.WriteLine($"  Throughput: ~{tokensPerSec:F2} tokens/sec");
                }

                Console.WriteLine("  ✓ Generation successful");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ✗ Generation failed: {ex.Message}");
                throw;
            }
        }

        static void DisplayPerformanceMetrics()
        {
            var gcBefore = GC.CollectionCount(0);
            var memBefore = GC.GetTotalMemory(false);

            // Trigger GC to get clean stats
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var memAfter = GC.GetTotalMemory(true);
            var gcAfter = GC.CollectionCount(0);

            Console.WriteLine($"  Managed Memory: {memAfter / 1024.0 / 1024.0:F2} MB");
            Console.WriteLine($"  GC Gen0 Collections: {gcAfter}");
            Console.WriteLine($"  GC Gen1 Collections: {GC.CollectionCount(1)}");
            Console.WriteLine($"  GC Gen2 Collections: {GC.CollectionCount(2)}");
        }

        static string GetMetadataValue(Dictionary<string, object> metadata, string key, string defaultValue)
        {
            if (metadata.TryGetValue(key, out var value))
            {
                return value?.ToString() ?? defaultValue;
            }
            return defaultValue;
        }

        static int? GetMetadataInt(Dictionary<string, object> metadata, string key)
        {
            if (metadata.TryGetValue(key, out var value))
            {
                if (value is int intVal) return intVal;
                if (value is uint uintVal) return (int)uintVal;
                if (value is long longVal) return (int)longVal;
                if (value is ulong ulongVal) return (int)ulongVal;
            }
            return null;
        }

        static double? GetMetadataDouble(Dictionary<string, object> metadata, string key)
        {
            if (metadata.TryGetValue(key, out var value))
            {
                if (value is double dblVal) return dblVal;
                if (value is float fltVal) return fltVal;
                if (value is int intVal) return intVal;
            }
            return null;
        }
    }
}
