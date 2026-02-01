using System;
using System.Collections.Generic;
using SmallMind.Core;
using SmallMind.Explainability;

namespace SmallMind.Text
{
    /// <summary>
    /// Implements text generation with greedy decoding, temperature sampling, and top-k filtering.
    /// Pure C# implementation.
    /// </summary>
    public class Sampling
    {
        private readonly TransformerModel _model;
        private readonly ITokenizer _tokenizer;
        private readonly int _blockSize;
        
        // Reusable buffers to reduce allocations
        private float[]? _probabilityBuffer;

        public Sampling(TransformerModel model, ITokenizer tokenizer, int blockSize)
        {
            _model = model;
            _tokenizer = tokenizer;
            _blockSize = blockSize;
        }

        /// <summary>
        /// Generate text from a prompt with optional explainability capture.
        /// </summary>
        public string Generate(
            string prompt, 
            int maxNewTokens, 
            double temperature = 1.0, 
            int topK = 0, 
            int? seed = null, 
            bool showPerf = false, 
            bool isPerfJsonMode = false, 
            PerformanceMetrics? metrics = null,
            ExplainabilityOptions? explainabilityOptions = null,
            IExplainabilitySink? explainabilitySink = null)
        {
            _model.Eval();

            Random random;
            if (seed.HasValue)
            {
                random = new Random(seed.Value);
            }
            else
            {
                random = new Random();
            }

            // Encode the prompt
            var context = _tokenizer.Encode(prompt);
            if (context.Count == 0)
            {
                Console.WriteLine("Warning: Empty prompt, starting with empty context");
                context = new List<int> { 0 }; // Start with first token in vocab
            }

            int inputTokens = context.Count;

            // Initialize explainability sink if provided
            bool captureExplainability = explainabilitySink != null && explainabilitySink.IsEnabled;
            if (captureExplainability && explainabilityOptions != null)
            {
                explainabilityOptions.Validate();
                var ctx = new ExplainabilityContext(
                    context,
                    explainabilityOptions.RedactPromptText ? null : prompt,
                    explainabilityOptions);
                explainabilitySink!.OnGenerationStart(ctx);
            }

            // Use provided metrics or create new one if perf tracking is enabled
            bool isMetricsOwner = false;
            if (metrics == null && (showPerf || isPerfJsonMode))
            {
                metrics = new PerformanceMetrics();
                isMetricsOwner = true;
            }

            if (!isPerfJsonMode)
            {
                Console.WriteLine($"\nGenerating {maxNewTokens} tokens...");
                Console.WriteLine($"Temperature: {temperature}, Top-k: {topK}");
                Console.WriteLine($"Prompt: \"{prompt}\"");
                if (showPerf)
                {
                    Console.WriteLine("Performance tracking enabled");
                }
                Console.WriteLine("---");
            }

            var totalStopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            // Start metrics tracking
            int requestId = -1;
            if (metrics != null)
            {
                if (!metrics.IsEnabled)
                {
                    metrics.Start();
                }
                requestId = metrics.RecordRequestStart();
                metrics.RecordInferenceStart(requestId);
            }

            bool firstTokenRecorded = false;

            for (int i = 0; i < maxNewTokens; i++)
            {
                var stepStopwatch = captureExplainability && explainabilityOptions!.IncludeTiming 
                    ? System.Diagnostics.Stopwatch.StartNew() 
                    : null;

                // Crop context to last blockSize tokens (avoid LINQ allocation)
                List<int> contextCropped;
                if (context.Count <= _blockSize)
                {
                    contextCropped = context;
                }
                else
                {
                    // Manual copy of last blockSize tokens (faster than Skip().ToList())
                    contextCropped = new List<int>(_blockSize);
                    int startIdx = context.Count - _blockSize;
                    for (int idx = startIdx; idx < context.Count; idx++)
                    {
                        contextCropped.Add(context[idx]);
                    }
                }

                // Convert to tensor: (1, T)
                var contextData = new float[contextCropped.Count];
                for (int j = 0; j < contextCropped.Count; j++)
                {
                    contextData[j] = contextCropped[j];
                }
                var contextTensor = new Tensor(contextData, new int[] { 1, contextCropped.Count });

                // Forward pass: (1, T, vocab_size)
                var logits = _model.Forward(contextTensor);

                // Get logits for the last position: (vocab_size,)
                // logits shape: (1, T, vocab_size), we want position (0, T-1, :)
                int T = contextCropped.Count;
                int vocabSize = logits.Shape[2];
                var logitsLast = new float[vocabSize];
                int lastPosOffset = (T - 1) * vocabSize; // Offset for last position in batch 0
                for (int v = 0; v < vocabSize; v++)
                {
                    logitsLast[v] = logits.Data[lastPosOffset + v];
                }

                // Apply temperature
                if (temperature != 1.0)
                {
                    for (int v = 0; v < vocabSize; v++)
                    {
                        logitsLast[v] /= (float)temperature;
                    }
                }

                // Apply top-k filtering
                if (topK > 0)
                {
                    logitsLast = ApplyTopK(logitsLast, topK);
                }

                // Convert to probabilities (softmax)
                var probs = Softmax(logitsLast);

                // Sample from the distribution
                var nextToken = SampleFromProbs(probs, random);

                // Capture explainability data if enabled
                if (captureExplainability && explainabilityOptions != null)
                {
                    try
                    {
                        CaptureTokenStep(
                            explainabilitySink!,
                            explainabilityOptions,
                            i,
                            nextToken,
                            probs,
                            stepStopwatch);
                    }
                    catch
                    {
                        // Explainability failures must not fail generation
                        // Silently continue
                    }
                }

                // Add to context
                context.Add(nextToken);
                
                // Record first token for TTFT metric
                if (metrics != null && !firstTokenRecorded)
                {
                    metrics.RecordFirstToken(requestId);
                    firstTokenRecorded = true;
                }

                // Optional: print progress
                if (!showPerf && !isPerfJsonMode && (i + 1) % 50 == 0)
                {
                    Console.Write(".");
                }
            }

            totalStopwatch.Stop();
            
            // Notify explainability sink of completion
            if (captureExplainability)
            {
                try
                {
                    var summary = new ExplainabilitySummary(totalStopwatch.Elapsed, success: true);
                    explainabilitySink!.OnGenerationEnd(summary);
                }
                catch
                {
                    // Explainability failures must not fail generation
                }
            }

            // Record completion
            if (metrics != null)
            {
                metrics.RecordRequestComplete(requestId, inputTokens, maxNewTokens, success: true);
            }

            if (!showPerf && !isPerfJsonMode && maxNewTokens >= 50)
            {
                Console.WriteLine(); // New line after progress dots
            }

            // Output performance metrics
            if (isMetricsOwner && metrics != null)
            {
                metrics.Stop();
                var summary = metrics.GetSummary(maxTokensRequested: maxNewTokens, concurrencyLevel: 1);
                
                if (isPerfJsonMode)
                {
                    // JSON output only
                    Console.WriteLine(MetricsFormatter.FormatJson(summary));
                }
                else if (showPerf)
                {
                    // Text output
                    Console.WriteLine(MetricsFormatter.FormatText(summary));
                }
            }

            // Decode and return
            var generated = _tokenizer.Decode(context);
            return generated;
        }

        /// <summary>
        /// Generate text with KV caching for efficient inference.
        /// </summary>
        public string GenerateWithCache(
            string prompt, 
            int maxNewTokens, 
            double temperature = 1.0, 
            int topK = 0, 
            int? seed = null, 
            bool showPerf = false)
        {
            _model.Eval();

            Random random;
            if (seed.HasValue)
            {
                random = new Random(seed.Value);
            }
            else
            {
                random = new Random();
            }

            // Encode the prompt
            var context = _tokenizer.Encode(prompt);
            if (context.Count == 0)
            {
                Console.WriteLine("Warning: Empty prompt, starting with empty context");
                context = new List<int> { 0 }; // Start with first token in vocab
            }

            int inputTokens = context.Count;
            
            // Performance tracking
            int cacheHits = 0;
            int tokensGenerated = 0;
            var totalStopwatch = System.Diagnostics.Stopwatch.StartNew();
            var prefillStopwatch = System.Diagnostics.Stopwatch.StartNew();

            if (!showPerf)
            {
                Console.WriteLine($"\nGenerating {maxNewTokens} tokens with KV cache...");
                Console.WriteLine($"Temperature: {temperature}, Top-k: {topK}");
                Console.WriteLine($"Prompt: \"{prompt}\"");
                Console.WriteLine("---");
            }

            // Create inference session with KV cache
            using var session = new InferenceSession(
                _model.NumLayers, 
                _blockSize, 
                _model.NumHeads, 
                _model.HeadDim);

            // PREFILL PHASE: Process entire prompt
            var contextData = new float[context.Count];
            for (int j = 0; j < context.Count; j++)
            {
                contextData[j] = context[j];
            }
            var contextTensor = new Tensor(contextData, new int[] { 1, context.Count });
            
            // Forward pass for prefill - populates KV cache
            var logits = _model.Forward(contextTensor, session, isPrefill: true);
            
            prefillStopwatch.Stop();
            
            // Get logits for the last position to start generation
            int T = context.Count;
            int vocabSize = logits.Shape[2];
            var logitsLast = new float[vocabSize];
            int lastPosOffset = (T - 1) * vocabSize;
            for (int v = 0; v < vocabSize; v++)
            {
                logitsLast[v] = logits.Data[lastPosOffset + v];
            }

            // Apply temperature
            if (temperature != 1.0)
            {
                for (int v = 0; v < vocabSize; v++)
                {
                    logitsLast[v] /= (float)temperature;
                }
            }

            // Apply top-k filtering
            if (topK > 0)
            {
                logitsLast = ApplyTopK(logitsLast, topK);
            }

            // Convert to probabilities (softmax)
            var probs = Softmax(logitsLast);

            // Sample first new token
            var nextToken = SampleFromProbs(probs, random);
            context.Add(nextToken);
            tokensGenerated++;

            // DECODE PHASE: Generate one token at a time using cache
            var decodeStopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            for (int i = 1; i < maxNewTokens; i++)
            {
                // Create tensor for single new token
                var singleTokenData = new float[1] { nextToken };
                var singleTokenTensor = new Tensor(singleTokenData, new int[] { 1, 1 });

                // Forward pass for decode - uses KV cache
                logits = _model.Forward(singleTokenTensor, session, isPrefill: false);
                cacheHits++; // Each decode step is a cache hit

                // Get logits for the single new position
                vocabSize = logits.Shape[2];
                logitsLast = new float[vocabSize];
                for (int v = 0; v < vocabSize; v++)
                {
                    logitsLast[v] = logits.Data[v]; // Only one position now
                }

                // Apply temperature
                if (temperature != 1.0)
                {
                    for (int v = 0; v < vocabSize; v++)
                    {
                        logitsLast[v] /= (float)temperature;
                    }
                }

                // Apply top-k filtering
                if (topK > 0)
                {
                    logitsLast = ApplyTopK(logitsLast, topK);
                }

                // Convert to probabilities (softmax)
                probs = Softmax(logitsLast);

                // Sample from the distribution
                nextToken = SampleFromProbs(probs, random);
                context.Add(nextToken);
                tokensGenerated++;

                // Optional: print progress
                if (!showPerf && (i + 1) % 50 == 0)
                {
                    Console.Write(".");
                }
            }

            decodeStopwatch.Stop();
            totalStopwatch.Stop();

            if (!showPerf && maxNewTokens >= 50)
            {
                Console.WriteLine(); // New line after progress dots
            }

            // Output performance metrics
            if (showPerf)
            {
                double totalMs = totalStopwatch.Elapsed.TotalMilliseconds;
                double prefillMs = prefillStopwatch.Elapsed.TotalMilliseconds;
                double decodeMs = decodeStopwatch.Elapsed.TotalMilliseconds;
                double tokensPerSec = tokensGenerated / totalStopwatch.Elapsed.TotalSeconds;
                
                Console.WriteLine("\n=== KV Cache Performance ===");
                Console.WriteLine($"Prompt tokens: {inputTokens}");
                Console.WriteLine($"Generated tokens: {tokensGenerated}");
                Console.WriteLine($"Cache hits: {cacheHits}");
                Console.WriteLine($"Prefill time: {prefillMs:F2}ms");
                Console.WriteLine($"Decode time: {decodeMs:F2}ms");
                Console.WriteLine($"Total time: {totalMs:F2}ms");
                Console.WriteLine($"Throughput: {tokensPerSec:F2} tokens/sec");
                Console.WriteLine($"Avg decode latency: {(decodeMs / Math.Max(1, tokensGenerated - 1)):F2}ms/token");
            }

            // Decode and return
            var generated = _tokenizer.Decode(context);
            return generated;
        }

        /// <summary>
        /// Apply top-k filtering to logits
        /// </summary>
        private float[] ApplyTopK(float[] logits, int k)
        {
            if (k >= logits.Length)
            {
                return logits;
            }

            // Rent buffer for sorting to reduce allocations
            float[]? rentedBuffer = null;
            try
            {
                rentedBuffer = System.Buffers.ArrayPool<float>.Shared.Rent(logits.Length);
                Array.Copy(logits, rentedBuffer, logits.Length);
                
                // Partial sort - only need to find k-th largest
                Array.Sort(rentedBuffer, 0, logits.Length);
                Array.Reverse(rentedBuffer, 0, logits.Length); // Now in descending order
                float kthValue = rentedBuffer[Math.Min(k - 1, logits.Length - 1)];

                // Set all values below k-th to -inf
                var filtered = new float[logits.Length];
                for (int i = 0; i < logits.Length; i++)
                {
                    filtered[i] = logits[i] >= kthValue ? logits[i] : float.NegativeInfinity;
                }

                return filtered;
            }
            finally
            {
                if (rentedBuffer != null)
                {
                    System.Buffers.ArrayPool<float>.Shared.Return(rentedBuffer);
                }
            }
        }

        /// <summary>
        /// Compute softmax over an array (reuses buffer for performance)
        /// </summary>
        private float[] Softmax(float[] logits)
        {
            // Reuse probability buffer to reduce allocations
            if (_probabilityBuffer == null || _probabilityBuffer.Length != logits.Length)
            {
                _probabilityBuffer = new float[logits.Length];
            }
            
            // Find max for numerical stability
            float max = float.NegativeInfinity;
            foreach (var val in logits)
            {
                if (val != float.NegativeInfinity)
                {
                    max = Math.Max(max, val);
                }
            }

            // Compute exp and sum
            float sum = 0;
            for (int i = 0; i < logits.Length; i++)
            {
                if (logits[i] != float.NegativeInfinity)
                {
                    _probabilityBuffer[i] = MathF.Exp(logits[i] - max);
                    sum += _probabilityBuffer[i];
                }
                else
                {
                    _probabilityBuffer[i] = 0;
                }
            }

            // Normalize
            if (sum > 0)
            {
                for (int i = 0; i < _probabilityBuffer.Length; i++)
                {
                    _probabilityBuffer[i] /= sum;
                }
            }

            return _probabilityBuffer;
        }

        /// <summary>
        /// Sample a token index from a probability distribution
        /// </summary>
        private int SampleFromProbs(float[] probs, Random random)
        {
            double target = random.NextDouble();
            double cumSum = 0.0;

            for (int i = 0; i < probs.Length; i++)
            {
                cumSum += probs[i];
                if (cumSum >= target)
                {
                    return i;
                }
            }

            // Fallback
            return probs.Length - 1;
        }

        /// <summary>
        /// Captures explainability data for a single token generation step.
        /// Efficient top-k extraction without full sort.
        /// </summary>
        private void CaptureTokenStep(
            IExplainabilitySink sink,
            ExplainabilityOptions options,
            int stepIndex,
            int selectedTokenId,
            float[] probs,
            System.Diagnostics.Stopwatch? stepStopwatch)
        {
            // Get selected token probability
            double selectedProb = probs[selectedTokenId];

            // Extract top-k alternatives using efficient partial selection
            int k = Math.Min(options.TopKAlternatives, probs.Length);
            if (k == 0)
            {
                // No alternatives requested, just record the selected token
                var stepData = new TokenStepData(
                    stepIndex,
                    selectedTokenId,
                    _tokenizer.Decode(new List<int> { selectedTokenId }),
                    selectedProb,
                    Array.Empty<int>(),
                    Array.Empty<string>(),
                    Array.Empty<double>(),
                    entropy: null,
                    elapsed: stepStopwatch?.Elapsed);
                sink.OnTokenStep(stepData);
                return;
            }

            // Use partial heap-based top-k selection (O(n log k) instead of O(n log n))
            var topK = ExtractTopK(probs, k);

            // Decode token texts
            var tokenIds = new int[topK.Count];
            var tokenTexts = new string[topK.Count];
            var tokenProbs = new double[topK.Count];

            for (int i = 0; i < topK.Count; i++)
            {
                tokenIds[i] = topK[i].Index;
                tokenProbs[i] = topK[i].Prob;
                tokenTexts[i] = _tokenizer.Decode(new List<int> { topK[i].Index });
            }

            // Compute entropy if requested (Standard or Detailed level)
            double? entropy = null;
            if (options.Level >= ExplainabilityLevel.Standard)
            {
                entropy = ComputeEntropy(probs);
            }

            var stepDataFull = new TokenStepData(
                stepIndex,
                selectedTokenId,
                _tokenizer.Decode(new List<int> { selectedTokenId }),
                selectedProb,
                tokenIds,
                tokenTexts,
                tokenProbs,
                entropy,
                stepStopwatch?.Elapsed);

            sink.OnTokenStep(stepDataFull);
        }

        /// <summary>
        /// Efficiently extracts top-k probabilities using a min-heap.
        /// Returns indices and probabilities sorted by descending probability.
        /// </summary>
        private List<(int Index, double Prob)> ExtractTopK(float[] probs, int k)
        {
            // Use a simple O(n*k) approach for small k (which is typical: k <= 50)
            // For small k, this is faster than heap-based approaches due to better cache locality
            var topK = new List<(int Index, double Prob)>(k);

            for (int i = 0; i < probs.Length; i++)
            {
                double prob = probs[i];
                
                // Skip zero probabilities
                if (prob <= 0)
                    continue;

                // Insert into top-k list, maintaining sorted order
                if (topK.Count < k)
                {
                    // List not full yet, insert in sorted position
                    int insertPos = topK.Count;
                    for (int j = 0; j < topK.Count; j++)
                    {
                        if (prob > topK[j].Prob)
                        {
                            insertPos = j;
                            break;
                        }
                    }
                    topK.Insert(insertPos, (i, prob));
                }
                else if (prob > topK[k - 1].Prob)
                {
                    // Replace smallest element and bubble up
                    topK[k - 1] = (i, prob);
                    
                    // Bubble the new element up to maintain sorted order
                    for (int j = k - 2; j >= 0; j--)
                    {
                        if (topK[j + 1].Prob > topK[j].Prob)
                        {
                            var tmp = topK[j];
                            topK[j] = topK[j + 1];
                            topK[j + 1] = tmp;
                        }
                        else
                        {
                            break;
                        }
                    }
                }
            }

            // List is already sorted, no need for final sort
            return topK;
        }

        /// <summary>
        /// Computes Shannon entropy of a probability distribution.
        /// </summary>
        private double ComputeEntropy(float[] probs)
        {
            double entropy = 0.0;
            for (int i = 0; i < probs.Length; i++)
            {
                if (probs[i] > 0)
                {
                    entropy -= probs[i] * Math.Log(probs[i], 2.0);
                }
            }
            return entropy;
        }
    }
}
