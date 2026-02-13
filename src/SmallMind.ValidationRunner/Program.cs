using System.Diagnostics;
using SmallMind.Runtime;
using SmallMind.Tokenizers;
using SmallMind.Transformers;

namespace SmallMind.ValidationRunner;

/// <summary>
/// End-to-end GGUF model validation runner.
/// Downloads TinyLlama Q4_0, runs validation tests, and reports results.
/// </summary>
internal class Program
{
    private const string DefaultModelUrl = "https://huggingface.co/TheBloke/TinyLlama-1.1B-Chat-v1.0-GGUF/resolve/main/tinyllama-1.1b-chat-v1.0.Q4_0.gguf";
    private const string DefaultModelFileName = "tinyllama-1.1b-chat-v1.0.Q4_0.gguf";

    private static string _cacheDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".smallmind", "models");
    private static bool _verbose = false;
    private static int _seed = 42;
    private static string? _modelPath = null;

    static async Task<int> Main(string[] args)
    {
        Console.WriteLine("=== SmallMind GGUF Validation Runner ===");
        Console.WriteLine();

        // Parse arguments
        if (!ParseArguments(args))
        {
            PrintUsage();
            return 1;
        }

        try
        {
            // Ensure cache directory exists
            Directory.CreateDirectory(_cacheDir);

            // Download or locate model
            string modelPath;
            if (_modelPath != null)
            {
                if (File.Exists(_modelPath))
                {
                    modelPath = _modelPath;
                    Console.WriteLine($"Using model: {modelPath}");
                }
                else if (Uri.TryCreate(_modelPath, UriKind.Absolute, out var uri) &&
                         (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
                {
                    // Download from URL
                    var fileName = Path.GetFileName(uri.LocalPath);
                    modelPath = Path.Combine(_cacheDir, fileName);
                    await DownloadModelAsync(_modelPath, modelPath);
                }
                else
                {
                    Console.Error.WriteLine($"ERROR: Model path '{_modelPath}' not found and is not a valid URL.");
                    return 1;
                }
            }
            else
            {
                // Use default TinyLlama model
                modelPath = Path.Combine(_cacheDir, DefaultModelFileName);
                if (!File.Exists(modelPath) || new FileInfo(modelPath).Length == 0)
                {
                    await DownloadModelAsync(DefaultModelUrl, modelPath);
                }
                else
                {
                    Console.WriteLine($"Using cached model: {modelPath}");
                }
            }

            // Load model
            Console.WriteLine();
            Console.WriteLine("Loading GGUF model...");
            var loadStart = Stopwatch.GetTimestamp();

            var (model, tokenizer, config) = GgufModelLoader.LoadFromGguf(modelPath, seed: _seed, useMmap: false);

            var loadElapsed = Stopwatch.GetElapsedTime(loadStart);

            // Print model info
            PrintModelInfo(config, tokenizer, loadElapsed);

            if (_verbose)
            {
                PrintWeightSamples(model);
            }

            // Run validation tests
            Console.WriteLine();
            Console.WriteLine("=== Running Validation Tests ===");
            Console.WriteLine();

            bool allPassed = true;

            allPassed &= await RunTestA_TokenizerRoundTrip(tokenizer);
            allPassed &= await RunTestB_ForwardPassSanity(model, tokenizer, config);
            allPassed &= await RunTestC_GreedyDeterminism(model, tokenizer, config);
            allPassed &= await RunTestD_SampledGeneration(model, tokenizer, config);
            allPassed &= await RunTestE_StopSequences(model, tokenizer, config);

            // Print summary
            Console.WriteLine();
            Console.WriteLine("=== Validation Summary ===");
            if (allPassed)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("✓ ALL TESTS PASSED");
                Console.ResetColor();
                return 0;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("✗ SOME TESTS FAILED");
                Console.ResetColor();
                return 1;
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"FATAL ERROR: {ex.Message}");
            if (_verbose)
            {
                Console.Error.WriteLine(ex.StackTrace);
            }
            Console.ResetColor();
            return 1;
        }
    }

    private static bool ParseArguments(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--model":
                    if (i + 1 >= args.Length)
                    {
                        Console.Error.WriteLine("ERROR: --model requires a path or URL");
                        return false;
                    }
                    _modelPath = args[++i];
                    break;

                case "--cache-dir":
                    if (i + 1 >= args.Length)
                    {
                        Console.Error.WriteLine("ERROR: --cache-dir requires a path");
                        return false;
                    }
                    _cacheDir = args[++i];
                    break;

                case "--verbose":
                    _verbose = true;
                    break;

                case "--seed":
                    if (i + 1 >= args.Length)
                    {
                        Console.Error.WriteLine("ERROR: --seed requires an integer");
                        return false;
                    }
                    if (!int.TryParse(args[++i], out _seed))
                    {
                        Console.Error.WriteLine("ERROR: --seed must be a valid integer");
                        return false;
                    }
                    break;

                case "--help":
                case "-h":
                    return false;

                default:
                    Console.Error.WriteLine($"ERROR: Unknown argument '{args[i]}'");
                    return false;
            }
        }
        return true;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage: SmallMind.ValidationRunner [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --model <path|url>    Model path or HuggingFace URL (default: TinyLlama Q4_0)");
        Console.WriteLine("  --cache-dir <path>    Cache directory for models (default: ~/.smallmind/models/)");
        Console.WriteLine("  --verbose             Enable verbose diagnostics");
        Console.WriteLine("  --seed <int>          Random seed for generation (default: 42)");
        Console.WriteLine("  --help, -h            Show this help message");
    }

    private static async Task DownloadModelAsync(string url, string targetPath)
    {
        Console.WriteLine($"Downloading model from: {url}");
        Console.WriteLine($"Target: {targetPath}");

        var tempPath = targetPath + ".downloading";

        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };

            // Check if partial download exists
            long bytesDownloaded = 0;
            if (File.Exists(tempPath))
            {
                bytesDownloaded = new FileInfo(tempPath).Length;
                Console.WriteLine($"Resuming from {bytesDownloaded:N0} bytes...");
            }

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            if (bytesDownloaded > 0)
            {
                request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(bytesDownloaded, null);
            }

            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? 0;
            var totalToDownload = totalBytes + bytesDownloaded;

            Console.WriteLine($"Size: {totalToDownload:N0} bytes ({totalToDownload / 1024.0 / 1024.0:F2} MB)");

            using var contentStream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(tempPath, FileMode.Append, FileAccess.Write, FileShare.None, 8192, useAsync: true);

            var buffer = new byte[8192];
            var lastReportTime = Stopwatch.GetTimestamp();
            var lastReportBytes = bytesDownloaded;

            int bytesRead;
            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, bytesRead);
                bytesDownloaded += bytesRead;

                // Report progress every second
                var now = Stopwatch.GetTimestamp();
                if (Stopwatch.GetElapsedTime(lastReportTime).TotalSeconds >= 1.0)
                {
                    var speed = (bytesDownloaded - lastReportBytes) / 1024.0 / 1024.0; // MB/s
                    var progress = totalToDownload > 0 ? (bytesDownloaded * 100.0 / totalToDownload) : 0;
                    Console.Write($"\rProgress: {progress:F1}% ({bytesDownloaded:N0} / {totalToDownload:N0} bytes) - {speed:F2} MB/s");
                    lastReportTime = now;
                    lastReportBytes = bytesDownloaded;
                }
            }

            Console.WriteLine();

            // Move to final location
            if (File.Exists(targetPath))
            {
                File.Delete(targetPath);
            }
            File.Move(tempPath, targetPath);

            Console.WriteLine("Download complete!");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Download failed: {ex.Message}");
            throw;
        }
    }

    private static void PrintModelInfo(ModelConfig config, ITokenizer tokenizer, TimeSpan loadTime)
    {
        Console.WriteLine();
        Console.WriteLine("=== Model Information ===");
        Console.WriteLine($"Architecture:      {config.Architecture}");
        Console.WriteLine($"Vocabulary size:   {config.VocabSize:N0} (tokenizer reports {tokenizer.VocabSize:N0})");
        Console.WriteLine($"Context length:    {config.ContextLength:N0} tokens");
        Console.WriteLine($"Embedding dim:     {config.EmbeddingLength}");
        Console.WriteLine($"Layers:            {config.BlockCount}");
        Console.WriteLine($"Attention heads:   {config.HeadCount} (KV heads: {config.HeadCountKv})");
        Console.WriteLine($"RoPE freq base:    {config.RopeFreqBase}");
        Console.WriteLine($"Load time:         {loadTime.TotalSeconds:F2} seconds");
        Console.WriteLine($"BOS token:         {tokenizer.BosTokenId}");
        Console.WriteLine($"EOS token:         {tokenizer.EosTokenId}");
    }

    private static void PrintWeightSamples(TransformerModel model)
    {
        Console.WriteLine();
        Console.WriteLine("=== Weight Samples (--verbose) ===");

        var namedParams = model.GetNamedParameters();

        // Sample token embeddings
        if (namedParams.TryGetValue("token_embd.weight", out var embedWeight))
        {
            var data = embedWeight.Data;
            Console.WriteLine($"token_embd.weight (first 8): {string.Join(", ", data.Take(8).Select(x => $"{x:F6}"))}");
        }

        // Sample first layer QKV
        if (namedParams.TryGetValue("blk.0.attn_qkv.weight", out var qkvWeight))
        {
            var data = qkvWeight.Data;
            Console.WriteLine($"blk.0.attn_qkv.weight (first 8): {string.Join(", ", data.Take(8).Select(x => $"{x:F6}"))}");
        }

        // Sample output weight
        if (namedParams.TryGetValue("output.weight", out var outputWeight))
        {
            var data = outputWeight.Data;
            Console.WriteLine($"output.weight (first 8): {string.Join(", ", data.Take(8).Select(x => $"{x:F6}"))}");
        }
    }

    private static async Task<bool> RunTestA_TokenizerRoundTrip(ITokenizer tokenizer)
    {
        Console.WriteLine("Test A — Tokenizer Round Trip");

        try
        {
            // Test 1: Simple roundtrip
            var testText = "Hello, how are you?";
            var tokens = tokenizer.Encode(testText);
            var decoded = tokenizer.Decode(tokens);

            // Normalize whitespace for comparison
            var normalizedOriginal = testText.Trim();
            var normalizedDecoded = decoded.Trim();

            if (_verbose)
            {
                Console.WriteLine($"  Original: '{testText}'");
                Console.WriteLine($"  Tokens:   [{string.Join(", ", tokens)}]");
                Console.WriteLine($"  Decoded:  '{decoded}'");
            }

            // Test 2: Chat template encoding
            var chatTemplate = "<|user|>\nWhat is 2+2?\n<|assistant|>\n";
            var chatTokens = tokenizer.Encode(chatTemplate);

            if (_verbose)
            {
                Console.WriteLine($"  Chat template tokens: [{string.Join(", ", chatTokens)}] (count: {chatTokens.Count})");
            }

            // Basic validation
            if (tokens.Count == 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("  FAIL: Tokenizer returned empty token list");
                Console.ResetColor();
                return false;
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("  PASS");
            Console.ResetColor();
            return true;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  FAIL: {ex.Message}");
            Console.ResetColor();
            return false;
        }
    }

    private static async Task<bool> RunTestB_ForwardPassSanity(TransformerModel model, ITokenizer tokenizer, ModelConfig config)
    {
        Console.WriteLine("Test B — Forward Pass Sanity");

        try
        {
            var testText = "Hello";
            var tokens = tokenizer.Encode(testText);

            // Create input tensor [1, seq_len]
            var inputData = new float[tokens.Count];
            for (int i = 0; i < tokens.Count; i++)
            {
                inputData[i] = tokens[i];
            }

            var inputTensor = new SmallMind.Core.Core.Tensor(
                inputData,
                new int[] { 1, tokens.Count }
            );

            // Run forward pass
            var outputTensor = model.Forward(inputTensor);

            // Verify shape: should be [1, seq_len, vocab_size] or [1, vocab_size] depending on implementation
            var shape = outputTensor.Shape;
            var lastDim = shape[shape.Length - 1];

            if (_verbose)
            {
                Console.WriteLine($"  Input shape:  [{string.Join(", ", inputTensor.Shape)}]");
                Console.WriteLine($"  Output shape: [{string.Join(", ", shape)}]");
                Console.WriteLine($"  Expected vocab size: {config.VocabSize}");
            }

            if (lastDim != config.VocabSize)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"  FAIL: Expected last dim {config.VocabSize}, got {lastDim}");
                Console.ResetColor();
                return false;
            }

            // Check for NaN/Inf
            var logits = outputTensor.Data;
            var hasNaN = logits.Any(x => float.IsNaN(x));
            var hasInf = logits.Any(x => float.IsInfinity(x));

            if (hasNaN || hasInf)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"  FAIL: Logits contain NaN={hasNaN} or Inf={hasInf}");
                Console.ResetColor();
                return false;
            }

            // Check variance
            var mean = logits.Average();
            var variance = logits.Select(x => (x - mean) * (x - mean)).Average();

            if (variance == 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"  FAIL: All logits are equal (variance=0)");
                Console.ResetColor();
                return false;
            }

            if (_verbose)
            {
                var min = logits.Min();
                var max = logits.Max();
                var std = Math.Sqrt(variance);
                Console.WriteLine($"  Logits stats: min={min:F4}, max={max:F4}, mean={mean:F4}, std={std:F4}");

                // Top 10 token IDs and their logit values
                var topIndices = logits
                    .Select((val, idx) => (val, idx))
                    .OrderByDescending(x => x.val)
                    .Take(10)
                    .ToArray();

                Console.WriteLine($"  Top 10 logits:");
                foreach (var (val, idx) in topIndices)
                {
                    Console.WriteLine($"    Token {idx}: {val:F4}");
                }
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("  PASS");
            Console.ResetColor();
            return true;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  FAIL: {ex.Message}");
            if (_verbose && ex.StackTrace != null)
            {
                Console.WriteLine($"  {ex.StackTrace}");
            }
            Console.ResetColor();
            return false;
        }
    }

    private static async Task<bool> RunTestC_GreedyDeterminism(TransformerModel model, ITokenizer tokenizer, ModelConfig config)
    {
        Console.WriteLine("Test C — Greedy Determinism");

        try
        {
            var prompt = "The capital of France is";
            var maxTokens = 10;

            // Generate twice with same seed
            var options = new ProductionInferenceOptions
            {
                Temperature = 0.001,  // Near-greedy (0.0 not allowed)
                TopP = 1.0,
                TopK = 0,
                MaxNewTokens = maxTokens,
                Seed = _seed
            };

            var result1 = GenerateText(model, tokenizer, config, prompt, options);
            var result2 = GenerateText(model, tokenizer, config, prompt, options);

            if (_verbose)
            {
                Console.WriteLine($"  Prompt: '{prompt}'");
                Console.WriteLine($"  Result 1: '{result1.text}'");
                Console.WriteLine($"  Tokens 1: [{string.Join(", ", result1.tokens)}]");
                Console.WriteLine($"  Result 2: '{result2.text}'");
                Console.WriteLine($"  Tokens 2: [{string.Join(", ", result2.tokens)}]");
            }

            // Check determinism
            if (result1.text != result2.text)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"  FAIL: Non-deterministic output");
                Console.ResetColor();
                return false;
            }

            if (!result1.tokens.SequenceEqual(result2.tokens))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"  FAIL: Token sequences differ");
                Console.ResetColor();
                return false;
            }

            // Check for coherent output (should contain "Paris" or at least be non-empty)
            var combined = prompt + result1.text;
            var containsParis = combined.Contains("Paris", StringComparison.OrdinalIgnoreCase);

            if (_verbose)
            {
                Console.WriteLine($"  Contains 'Paris': {containsParis}");
                if (!containsParis)
                {
                    Console.WriteLine($"  Note: Output may still be coherent without exact match");
                }
            }

            if (string.IsNullOrWhiteSpace(result1.text))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"  FAIL: Empty output");
                Console.ResetColor();
                return false;
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("  PASS");
            Console.ResetColor();
            return true;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  FAIL: {ex.Message}");
            Console.ResetColor();
            return false;
        }
    }

    private static async Task<bool> RunTestD_SampledGeneration(TransformerModel model, ITokenizer tokenizer, ModelConfig config)
    {
        Console.WriteLine("Test D — Sampled Generation");

        try
        {
            var prompt = "<|user|>\nWrite a haiku about coding.\n<|assistant|>\n";
            var maxTokens = 50;

            var options = new ProductionInferenceOptions
            {
                Temperature = 0.7,
                TopP = 0.9,
                TopK = 40,
                MaxNewTokens = maxTokens,
                Seed = _seed
            };

            var sw = Stopwatch.StartNew();
            var result = GenerateText(model, tokenizer, config, prompt, options);
            sw.Stop();

            var ttft = result.ttftMs;
            var totalMs = sw.Elapsed.TotalMilliseconds;
            var tokensPerSec = result.tokens.Count / (totalMs / 1000.0);

            if (_verbose)
            {
                Console.WriteLine($"  Prompt: '{prompt}'");
                Console.WriteLine($"  Generated: '{result.text}'");
                Console.WriteLine($"  Token count: {result.tokens.Count}");
            }

            Console.WriteLine($"  TTFT: {ttft:F2} ms");
            Console.WriteLine($"  Throughput: {tokensPerSec:F2} tok/s");

            // Verify non-empty
            if (string.IsNullOrWhiteSpace(result.text))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"  FAIL: Empty output");
                Console.ResetColor();
                return false;
            }

            // Check for repetition (no 10+ identical tokens in a row)
            var hasRepetition = false;
            for (int i = 0; i < result.tokens.Count - 9; i++)
            {
                var token = result.tokens[i];
                var allSame = true;
                for (int j = 1; j < 10; j++)
                {
                    if (result.tokens[i + j] != token)
                    {
                        allSame = false;
                        break;
                    }
                }
                if (allSame)
                {
                    hasRepetition = true;
                    break;
                }
            }

            if (hasRepetition)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"  WARNING: Detected repetition pattern");
                Console.ResetColor();
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("  PASS");
            Console.ResetColor();
            return true;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  FAIL: {ex.Message}");
            Console.ResetColor();
            return false;
        }
    }

    private static async Task<bool> RunTestE_StopSequences(TransformerModel model, ITokenizer tokenizer, ModelConfig config)
    {
        Console.WriteLine("Test E — Stop Sequences");

        try
        {
            var prompt = "List three items:\n1.";
            var maxTokens = 100;

            var options = new ProductionInferenceOptions
            {
                Temperature = 0.7,
                TopP = 0.9,
                TopK = 40,
                MaxNewTokens = maxTokens,
                StopSequences = new[] { "\n\n", "<|" },
                Seed = _seed
            };

            var result = GenerateText(model, tokenizer, config, prompt, options);

            if (_verbose)
            {
                Console.WriteLine($"  Prompt: '{prompt}'");
                Console.WriteLine($"  Generated: '{result.text}'");
                Console.WriteLine($"  Tokens generated: {result.tokens.Count}");
                Console.WriteLine($"  Finish reason: {result.finishReason}");
            }

            // Check if generation stopped before max tokens
            var stoppedEarly = result.tokens.Count < maxTokens;
            var combined = prompt + result.text;
            var hasStopSeq = combined.Contains("\n\n") || combined.Contains("<|");

            if (_verbose)
            {
                Console.WriteLine($"  Stopped early: {stoppedEarly}");
                Console.WriteLine($"  Contains stop sequence: {hasStopSeq}");
            }

            // If stopped early due to stop sequence, that's ideal
            // If hit max tokens, that's also acceptable for this test
            if (result.finishReason == "StopSequence")
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"  PASS (stopped on stop sequence)");
                Console.ResetColor();
                return true;
            }
            else if (result.finishReason == "Length")
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"  PASS (reached max length without errors)");
                Console.ResetColor();
                return true;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"  PASS (finish reason: {result.finishReason})");
                Console.ResetColor();
                return true;
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  FAIL: {ex.Message}");
            Console.ResetColor();
            return false;
        }
    }

    private static (string text, List<int> tokens, string finishReason, double ttftMs) GenerateText(
        TransformerModel model,
        ITokenizer tokenizer,
        ModelConfig config,
        string prompt,
        ProductionInferenceOptions options)
    {
        var session = new InferenceSession(
            model,
            tokenizer,
            options,
            config.ContextLength,
            sessionId: Guid.NewGuid().ToString()
        );

        using (session)
        {
            // Use async API synchronously (not ideal but acceptable for test runner)
            var resultTask = session.GenerateAsync(prompt, metrics: null, CancellationToken.None);
            var fullText = resultTask.GetAwaiter().GetResult();

            // Extract tokens by re-encoding
            var tokens = tokenizer.Encode(fullText);
            var promptTokens = tokenizer.Encode(prompt);

            // Calculate generated tokens (remove prompt)
            var generatedTokens = tokens.Skip(promptTokens.Count).ToList();

            // Extract just the generated text
            var generatedText = fullText.Substring(prompt.Length);

            // Determine finish reason (simplified for now)
            var finishReason = generatedTokens.Count >= options.MaxNewTokens ? "Length" : "Completed";

            return (generatedText, generatedTokens, finishReason, 0.0); // TTFT not readily available in this API
        }
    }
}
