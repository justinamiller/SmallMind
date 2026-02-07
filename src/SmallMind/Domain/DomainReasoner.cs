using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SmallMind.Core.Core;
using SmallMind.Core.Validation;
using SmallMind.Runtime;
using SmallMind.Tokenizers;
using SmallMind.Transformers;

namespace SmallMind.Domain
{
    /// <summary>
    /// Implements domain-bounded reasoning with policy enforcement and safety constraints.
    /// </summary>
    public class DomainReasoner : IDomainReasoner
    {
        private readonly TransformerModel _model;
        private readonly ITokenizer _tokenizer;
        private readonly int _blockSize;
        private readonly ILogger<DomainReasoner>? _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="DomainReasoner"/> class.
        /// </summary>
        /// <param name="model">The transformer model.</param>
        /// <param name="tokenizer">The tokenizer.</param>
        /// <param name="blockSize">The block size (context window).</param>
        /// <param name="logger">Optional logger.</param>
        public DomainReasoner(TransformerModel model, ITokenizer tokenizer, int blockSize, ILogger<DomainReasoner>? logger = null)
        {
            _model = Guard.NotNull(model, nameof(model));
            _tokenizer = Guard.NotNull(tokenizer, nameof(tokenizer));
            _blockSize = Guard.GreaterThan(blockSize, 0, nameof(blockSize));
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<DomainAnswer> AskAsync(DomainQuestion question, DomainProfile domain, CancellationToken ct = default)
        {
            // Validate inputs
            Guard.NotNull(question, nameof(question));
            Guard.NotNull(domain, nameof(domain));
            question.Validate();
            domain.Validate();

            var stopwatch = Stopwatch.StartNew();
            var requestId = question.RequestId ?? Guid.NewGuid().ToString();

            _logger?.LogInformation("Domain reasoning request started: RequestId={RequestId}, Domain={DomainName}/{DomainVersion}",
                requestId, domain.Name, domain.Version);

            try
            {
                // Set model to eval mode
                _model.Eval();

                // Build prompt from question
                var prompt = BuildPrompt(question);

                // Validate input tokens
                var inputTokens = _tokenizer.Encode(prompt);
                if (inputTokens.Count > domain.MaxInputTokens)
                {
                    var reason = $"Input tokens ({inputTokens.Count}) exceed maximum ({domain.MaxInputTokens})";
                    _logger?.LogWarning("Domain reasoning rejected: RequestId={RequestId}, Reason={Reason}",
                        requestId, reason);
                    
                    return DomainAnswer.Rejected(
                        DomainAnswerStatus.RejectedPolicy,
                        reason,
                        stopwatch.Elapsed,
                        requestId);
                }

                // Create cancellation token source for timeout
                using var timeoutCts = domain.MaxExecutionTime.HasValue
                    ? new CancellationTokenSource(domain.MaxExecutionTime.Value)
                    : new CancellationTokenSource();

                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

                try
                {
                    // Generate with enforcement
                    var result = await GenerateWithEnforcementAsync(
                        prompt,
                        inputTokens,
                        domain,
                        linkedCts.Token);

                    stopwatch.Stop();

                    result.RequestId = requestId;
                    result.Duration = stopwatch.Elapsed;

                    _logger?.LogInformation(
                        "Domain reasoning completed: RequestId={RequestId}, Status={Status}, Duration={Duration}ms, InputTokens={InputTokens}, OutputTokens={OutputTokens}",
                        requestId, result.Status, stopwatch.Elapsed.TotalMilliseconds, result.InputTokens, result.OutputTokens);

                    return result;
                }
                catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested && !ct.IsCancellationRequested)
                {
                    // Timeout occurred
                    stopwatch.Stop();
                    var reason = $"Execution time exceeded maximum ({domain.MaxExecutionTime?.TotalSeconds:F1}s)";
                    
                    _logger?.LogWarning("Domain reasoning timeout: RequestId={RequestId}, Duration={Duration}ms",
                        requestId, stopwatch.Elapsed.TotalMilliseconds);

                    return DomainAnswer.Rejected(
                        DomainAnswerStatus.RejectedPolicy,
                        reason,
                        stopwatch.Elapsed,
                        requestId);
                }
            }
            catch (OperationCanceledException)
            {
                stopwatch.Stop();
                _logger?.LogInformation("Domain reasoning cancelled: RequestId={RequestId}", requestId);
                
                return DomainAnswer.Rejected(
                    DomainAnswerStatus.Cancelled,
                    "Request was cancelled",
                    stopwatch.Elapsed,
                    requestId);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger?.LogError(ex, "Domain reasoning failed: RequestId={RequestId}", requestId);
                
                return DomainAnswer.Failed(
                    $"Internal error: {ex.Message}",
                    stopwatch.Elapsed,
                    requestId);
            }
        }

        /// <inheritdoc/>
        public async IAsyncEnumerable<DomainToken> AskStreamAsync(
            DomainQuestion question,
            DomainProfile domain,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            // Validate inputs
            Guard.NotNull(question, nameof(question));
            Guard.NotNull(domain, nameof(domain));
            question.Validate();
            domain.Validate();

            var stopwatch = Stopwatch.StartNew();
            var requestId = question.RequestId ?? Guid.NewGuid().ToString();

            _logger?.LogInformation("Domain reasoning stream started: RequestId={RequestId}", requestId);

            // Set model to eval mode
            _model.Eval();

            // Build prompt
            var prompt = BuildPrompt(question);
            var inputTokens = _tokenizer.Encode(prompt);

            // Validate input tokens
            if (inputTokens.Count > domain.MaxInputTokens)
            {
                _logger?.LogWarning("Stream rejected: Input tokens exceed limit");
                yield break;
            }

            // Create timeout CTS
            using var timeoutCts = domain.MaxExecutionTime.HasValue
                ? new CancellationTokenSource(domain.MaxExecutionTime.Value)
                : new CancellationTokenSource();

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

            // Stream generation
            var tokenIndex = 0;
            await foreach (var token in GenerateStreamAsync(prompt, inputTokens, domain, linkedCts.Token))
            {
                yield return DomainToken.Create(
                    token.text,
                    token.id,
                    tokenIndex++,
                    stopwatch.Elapsed,
                    token.probability);
            }
        }

        private string BuildPrompt(DomainQuestion question)
        {
            if (string.IsNullOrEmpty(question.Context))
            {
                return question.Query;
            }

            return $"{question.Context}\n\nQuestion: {question.Query}\nAnswer:";
        }

        private async Task<DomainAnswer> GenerateWithEnforcementAsync(
            string prompt,
            List<int> inputTokens,
            DomainProfile domain,
            CancellationToken ct)
        {
            var random = domain.Sampling.Seed.HasValue
                ? new Random(domain.Sampling.Seed.Value)
                : new Random();

            var context = new List<int>(inputTokens);
            var generatedTokens = 0;
            var evidenceItems = new List<DomainEvidenceItem>();
            double confidenceSum = 0.0;
            int confidenceCount = 0;

            var temperature = domain.Sampling.GetEffectiveTemperature();
            var topK = domain.Sampling.GetEffectiveTopK();

            for (int i = 0; i < domain.MaxOutputTokens; i++)
            {
                ct.ThrowIfCancellationRequested();

                // Crop context to block size
                var contextCropped = CropContext(context);

                // Convert to tensor
                var contextTensor = CreateContextTensor(contextCropped);

                // Forward pass
                var logits = _model.Forward(contextTensor);

                // Get logits for last position
                var logitsLast = ExtractLastLogits(logits, contextCropped.Count);

                // Apply allowed token masking
                ApplyAllowedTokenMask(logitsLast, domain.AllowedTokens);

                // Apply temperature
                ApplyTemperature(logitsLast, temperature);

                // Apply top-k filtering
                if (topK > 0)
                {
                    ApplyTopK(logitsLast, topK);
                }

                // Convert to probabilities
                var probs = Softmax(logitsLast);

                // Sample token
                var nextToken = SampleFromProbs(probs, random);

                // Track provenance if enabled
                if (domain.Provenance.EnableProvenance && evidenceItems.Count < domain.Provenance.MaxEvidenceItems)
                {
                    var tokenText = _tokenizer.Decode(new List<int> { nextToken });
                    evidenceItems.Add(DomainEvidenceItem.Create(nextToken, tokenText, probs[nextToken], i));
                    confidenceSum += probs[nextToken];
                    confidenceCount++;
                }

                // Add to context
                context.Add(nextToken);
                generatedTokens++;

                // Check for unknown tokens if policy requires
                if (domain.Safety.DisallowUnknownTokens && nextToken >= _tokenizer.VocabSize)
                {
                    return DomainAnswer.Rejected(
                        DomainAnswerStatus.RejectedPolicy,
                        "Unknown token encountered",
                        TimeSpan.Zero);
                }
            }

            // Decode output
            var outputText = _tokenizer.Decode(context);
            
            // Extract only generated portion
            var generatedText = ExtractGeneratedText(outputText, prompt);

            // Validate output format
            var validationResult = await ValidateOutputFormat(generatedText, domain.Output, ct);
            if (!validationResult.isValid)
            {
                return DomainAnswer.Rejected(
                    DomainAnswerStatus.RejectedPolicy,
                    $"Output format validation failed: {validationResult.reason}",
                    TimeSpan.Zero);
            }

            // Build provenance if enabled
            DomainProvenance? provenance = null;
            if (domain.Provenance.EnableProvenance && confidenceCount > 0)
            {
                var confidence = confidenceSum / confidenceCount;
                
                // Check min confidence
                if (domain.Safety.MinConfidence > 0 && confidence < domain.Safety.MinConfidence)
                {
                    return DomainAnswer.Rejected(
                        DomainAnswerStatus.RejectedPolicy,
                        $"Confidence ({confidence:F3}) below minimum ({domain.Safety.MinConfidence:F3})",
                        TimeSpan.Zero);
                }
                
                provenance = DomainProvenance.Create(confidence, evidenceItems);
            }

            var answer = DomainAnswer.Success(
                generatedText,
                TimeSpan.Zero,
                inputTokens.Count,
                generatedTokens);

            answer.Provenance = provenance;

            return answer;
        }

        private async IAsyncEnumerable<(string text, int id, float probability)> GenerateStreamAsync(
            string prompt,
            List<int> inputTokens,
            DomainProfile domain,
            [EnumeratorCancellation] CancellationToken ct)
        {
            var random = domain.Sampling.Seed.HasValue
                ? new Random(domain.Sampling.Seed.Value)
                : new Random();

            var context = new List<int>(inputTokens);
            var temperature = domain.Sampling.GetEffectiveTemperature();
            var topK = domain.Sampling.GetEffectiveTopK();

            for (int i = 0; i < domain.MaxOutputTokens; i++)
            {
                ct.ThrowIfCancellationRequested();

                var contextCropped = CropContext(context);
                var contextTensor = CreateContextTensor(contextCropped);
                var logits = _model.Forward(contextTensor);
                var logitsLast = ExtractLastLogits(logits, contextCropped.Count);

                ApplyAllowedTokenMask(logitsLast, domain.AllowedTokens);
                ApplyTemperature(logitsLast, temperature);

                if (topK > 0)
                {
                    ApplyTopK(logitsLast, topK);
                }

                var probs = Softmax(logitsLast);
                var nextToken = SampleFromProbs(probs, random);

                context.Add(nextToken);

                var tokenText = _tokenizer.Decode(new List<int> { nextToken });
                yield return (tokenText, nextToken, probs[nextToken]);

                // Yield control to avoid blocking
                await Task.Yield();
            }
        }

        private List<int> CropContext(List<int> context)
        {
            if (context.Count <= _blockSize)
            {
                return context;
            }

            var cropped = new List<int>(_blockSize);
            int startIdx = context.Count - _blockSize;
            for (int idx = startIdx; idx < context.Count; idx++)
            {
                cropped.Add(context[idx]);
            }
            return cropped;
        }

        private Tensor CreateContextTensor(List<int> context)
        {
            var contextData = new float[context.Count];
            for (int j = 0; j < context.Count; j++)
            {
                contextData[j] = context[j];
            }
            return new Tensor(contextData, new int[] { 1, context.Count });
        }

        private float[] ExtractLastLogits(Tensor logits, int sequenceLength)
        {
            int vocabSize = logits.Shape[2];
            var logitsLast = new float[vocabSize];
            int lastPosOffset = (sequenceLength - 1) * vocabSize;
            
            for (int v = 0; v < vocabSize; v++)
            {
                logitsLast[v] = logits.Data[lastPosOffset + v];
            }
            
            return logitsLast;
        }

        private void ApplyAllowedTokenMask(float[] logits, Policies.AllowedTokenPolicy policy)
        {
            // If we have an allowlist, mask all tokens not in it
            if (policy.AllowedTokenIds != null && policy.AllowedTokenIds.Count > 0)
            {
                var allowedSet = new HashSet<int>(policy.AllowedTokenIds);
                for (int i = 0; i < logits.Length; i++)
                {
                    if (!allowedSet.Contains(i))
                    {
                        logits[i] = float.NegativeInfinity;
                    }
                }
            }

            // If we have a character allowlist (for character tokenizer)
            if (!string.IsNullOrEmpty(policy.AllowedCharacters))
            {
                var allowedChars = new HashSet<char>(policy.AllowedCharacters);
                for (int i = 0; i < logits.Length; i++)
                {
                    var tokenText = _tokenizer.Decode(new List<int> { i });
                    if (tokenText.Length == 1 && !allowedChars.Contains(tokenText[0]))
                    {
                        logits[i] = float.NegativeInfinity;
                    }
                }
            }

            // Apply blocklist
            if (policy.BlockedTokenIds != null && policy.BlockedTokenIds.Count > 0)
            {
                foreach (var blockedId in policy.BlockedTokenIds)
                {
                    if (blockedId >= 0 && blockedId < logits.Length)
                    {
                        logits[blockedId] = float.NegativeInfinity;
                    }
                }
            }

            // Apply blocked characters
            if (!string.IsNullOrEmpty(policy.BlockedCharacters))
            {
                var blockedChars = new HashSet<char>(policy.BlockedCharacters);
                for (int i = 0; i < logits.Length; i++)
                {
                    var tokenText = _tokenizer.Decode(new List<int> { i });
                    if (tokenText.Length == 1 && blockedChars.Contains(tokenText[0]))
                    {
                        logits[i] = float.NegativeInfinity;
                    }
                }
            }
        }

        private void ApplyTemperature(float[] logits, float temperature)
        {
            if (temperature != 1.0f && temperature > 0)
            {
                for (int i = 0; i < logits.Length; i++)
                {
                    logits[i] /= temperature;
                }
            }
        }

        private void ApplyTopK(float[] logits, int k)
        {
            if (k >= logits.Length || k <= 0)
            {
                return;
            }

            float[]? rentedBuffer = null;
            try
            {
                rentedBuffer = System.Buffers.ArrayPool<float>.Shared.Rent(logits.Length);
                Array.Copy(logits, rentedBuffer, logits.Length);

                Array.Sort(rentedBuffer, 0, logits.Length);
                Array.Reverse(rentedBuffer, 0, logits.Length);
                float kthValue = rentedBuffer[Math.Min(k - 1, logits.Length - 1)];

                for (int i = 0; i < logits.Length; i++)
                {
                    if (logits[i] < kthValue)
                    {
                        logits[i] = float.NegativeInfinity;
                    }
                }
            }
            finally
            {
                if (rentedBuffer != null)
                {
                    System.Buffers.ArrayPool<float>.Shared.Return(rentedBuffer);
                }
            }
        }

        private float[] Softmax(float[] logits)
        {
            var probs = new float[logits.Length];

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
                    probs[i] = MathF.Exp(logits[i] - max);
                    sum += probs[i];
                }
                else
                {
                    probs[i] = 0;
                }
            }

            // Normalize
            if (sum > 0)
            {
                for (int i = 0; i < probs.Length; i++)
                {
                    probs[i] /= sum;
                }
            }

            return probs;
        }

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

            return probs.Length - 1;
        }

        private string ExtractGeneratedText(string fullText, string prompt)
        {
            if (fullText.StartsWith(prompt))
            {
                return fullText.Substring(prompt.Length).Trim();
            }
            return fullText;
        }

        private async Task<(bool isValid, string? reason)> ValidateOutputFormat(
            string text,
            Policies.OutputPolicy policy,
            CancellationToken ct)
        {
            // Check max characters
            if (text.Length > policy.MaxCharacters)
            {
                return (false, $"Output length ({text.Length}) exceeds maximum ({policy.MaxCharacters})");
            }

            switch (policy.Format)
            {
                case OutputFormat.PlainText:
                    return (true, null);

                case OutputFormat.JsonOnly:
                    try
                    {
                        // Try to parse as JSON
                        using var doc = JsonDocument.Parse(text);
                        return (true, null);
                    }
                    catch (JsonException ex)
                    {
                        return (false, $"Invalid JSON: {ex.Message}");
                    }

                case OutputFormat.RegexConstrained:
                    if (string.IsNullOrEmpty(policy.Regex))
                    {
                        return (false, "Regex pattern not specified");
                    }

                    try
                    {
                        var regex = new Regex(policy.Regex, RegexOptions.None, TimeSpan.FromSeconds(1));
                        if (!regex.IsMatch(text))
                        {
                            return (false, $"Output does not match required pattern: {policy.Regex}");
                        }
                        return (true, null);
                    }
                    catch (RegexMatchTimeoutException)
                    {
                        return (false, "Regex matching timeout");
                    }
                    catch (Exception ex)
                    {
                        return (false, $"Regex error: {ex.Message}");
                    }

                default:
                    return (false, $"Unknown output format: {policy.Format}");
            }
        }
    }
}
