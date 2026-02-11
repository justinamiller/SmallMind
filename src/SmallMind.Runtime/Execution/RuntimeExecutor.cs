using System;
using System.Diagnostics;
using SmallMind.Core.Core;
using SmallMind.Runtime.Cache;
using SmallMind.Transformers;

namespace SmallMind.Runtime.Execution
{
    /// <summary>
    /// Runtime executor implementing hard prefill/decode separation.
    /// Provides explicit APIs for prompt processing (prefill) and single-token generation (decode).
    /// Ensures KV cache is mandatory and properly managed across both phases.
    /// </summary>
    internal sealed class RuntimeExecutor : IRuntimeExecutor
    {
        private readonly TransformerModel _model;
        private readonly KvCachePool _cachePool;
        private readonly int _blockSize;
        private readonly int _vocabSize;
        
        // Reusable decode tensor for single-token forward pass
        private readonly float[] _decodeData = new float[1];
        private readonly int[] _decodeShape = new int[] { 1, 1 };
        private Tensor? _decodeTensor;
        
        public RuntimeExecutor(TransformerModel model, KvCachePool cachePool, int blockSize)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _cachePool = cachePool ?? throw new ArgumentNullException(nameof(cachePool));
            _blockSize = blockSize;
            _vocabSize = model.VocabSize;
            
            // Ensure model is in eval mode
            _model.Eval();
        }
        
        /// <summary>
        /// Prefill phase: Processes the entire prompt to populate KV cache.
        /// Returns logits for the last token and a cache handle for decode.
        /// </summary>
        public PrefillResult Prefill(ReadOnlySpan<int> promptTokens, ExecutionContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            
            if (promptTokens.Length == 0)
                throw new ArgumentException("Prompt tokens cannot be empty", nameof(promptTokens));
            
            if (context.HasCache)
                throw new InvalidOperationException("Context already has a cache. Call Reset() before prefill.");
            
            var sw = Stopwatch.StartNew();
            
            // Crop context to last blockSize tokens if needed
            int startIdx = Math.Max(0, promptTokens.Length - _blockSize);
            int promptLength = promptTokens.Length - startIdx;
            var promptSlice = promptTokens.Slice(startIdx, promptLength);
            
            // Reset and enable KV-cache on model
            _model.ResetKVCache();
            _model.EnableKVCache();
            
            // Build tensor from prompt: shape (1, promptLength)
            var prefillData = new float[promptLength];
            for (int i = 0; i < promptLength; i++)
            {
                prefillData[i] = promptSlice[i];
            }
            var prefillTensor = new Tensor(prefillData, new int[] { 1, promptLength });
            
            // Forward pass with position offset 0 (start of sequence)
            var logits = _model.Forward(prefillTensor, positionOffset: 0);
            
            // Extract logits for last position
            int T = logits.Shape[1];  // Sequence length in output
            int vocabSize = logits.Shape[2];
            
            var lastLogits = new float[vocabSize];
            int lastPosOffset = (T - 1) * vocabSize;
            for (int v = 0; v < vocabSize; v++)
            {
                lastLogits[v] = logits.Data[lastPosOffset + v];
            }
            
            // Create KV cache handle (in real implementation, would rent from pool)
            // For now, create a simple wrapper
            var modelShape = new ModelShape(_model.NumLayers, _model.NumHeads, _model.EmbedDim / _model.NumHeads);
            var sessionId = new SessionId(Guid.NewGuid().ToString());
            var cacheEntry = _cachePool.Rent(modelShape, _blockSize, sessionId);
            var cacheHandle = new KvCacheHandle(cacheEntry);
            
            // Update context
            context.CacheHandle = cacheHandle;
            context.CurrentPosition = promptLength;
            
            sw.Stop();
            var metrics = new PrefillMetrics(promptLength, sw.Elapsed.TotalMilliseconds);
            
            // Record telemetry
            if (context.Options.EnableTelemetry)
            {
                context.Telemetry.RecordPrefillEnd(promptLength, sw.Elapsed.TotalMilliseconds);
            }
            
            return new PrefillResult(
                logits: new ReadOnlyMemory<float>(lastLogits),
                cacheHandle: cacheHandle,
                processedTokens: promptLength,
                metrics: metrics
            );
        }
        
        /// <summary>
        /// Decode phase: Processes a single token to generate the next token.
        /// Uses KV cache from context to avoid recomputing previous tokens.
        /// </summary>
        public DecodeResult Decode(int nextToken, ExecutionContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            
            if (!context.HasCache)
            {
                if (context.Options.RequireKvCache)
                {
                    throw new InvalidOperationException(
                        "Context does not have a cache. Call Prefill() first or disable RequireKvCache.");
                }
                // Fallback: could create cache here, but for now throw
                throw new InvalidOperationException("Decode requires prefill to be called first");
            }
            
            var sw = Stopwatch.StartNew();
            
            // Reuse decode tensor (zero allocation)
            _decodeData[0] = nextToken;
            if (_decodeTensor == null)
            {
                _decodeTensor = new Tensor(_decodeData, _decodeShape);
            }
            
            // Forward pass with current position offset
            // KV cache is already enabled from prefill
            var logits = _model.Forward(_decodeTensor, positionOffset: context.CurrentPosition);
            
            // Extract logits for the single output position
            int T = logits.Shape[1];  // Should be 1 for decode
            int vocabSize = logits.Shape[2];
            
            var decodedLogits = new float[vocabSize];
            int lastPosOffset = (T - 1) * vocabSize;
            for (int v = 0; v < vocabSize; v++)
            {
                decodedLogits[v] = logits.Data[lastPosOffset + v];
            }
            
            // Update context position
            context.CurrentPosition++;
            
            sw.Stop();
            var metrics = new DecodeMetrics(
                elapsedMs: sw.Elapsed.TotalMilliseconds,
                position: context.CurrentPosition,
                cacheUsed: true
            );
            
            // Record telemetry
            if (context.Options.EnableTelemetry)
            {
                context.Telemetry.RecordDecodeEnd(sw.Elapsed.TotalMilliseconds);
            }
            
            return new DecodeResult(
                logits: new ReadOnlyMemory<float>(decodedLogits),
                cacheHandle: context.CacheHandle!,
                metrics: metrics
            );
        }
    }
}
