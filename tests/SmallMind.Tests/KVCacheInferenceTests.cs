using Xunit;
using SmallMind.Core;
using SmallMind.Tokenizers;
using SmallMind.Transformers;
using SmallMind.Runtime;
using System;

namespace SmallMind.Tests
{
    /// <summary>
    /// Tests for KV cache inference to ensure correctness and determinism.
    /// </summary>
    public class KVCacheInferenceTests
    {
        [Fact]
        public void KVCache_SameSeedAndPrompt_ProducesSameOutputAsNonCached()
        {
            // Create a small test model
            int seed = 42;
            var model = new TransformerModel(
                vocabSize: 50,
                blockSize: 32,
                nEmbd: 64,
                nLayer: 2,
                nHead: 4,
                dropout: 0.0, // No dropout for determinism
                seed: seed);

            model.Eval(); // Set to eval mode

            // Create a simple tokenizer
            const string vocab = "abcdefghijklmnopqrstuvwxyz ";
            var tokenizer = new SmallMind.Text.CharTokenizer(vocab);

            var sampling = new Sampling(model, tokenizer, model.BlockSize);

            string prompt = "hello";
            int maxTokens = 10;
            double temperature = 1.0;
            int topK = 0;

            // Generate without cache
            string outputWithoutCache = sampling.Generate(
                prompt, 
                maxTokens, 
                temperature, 
                topK, 
                seed, 
                showPerf: false);

            // Generate with cache
            string outputWithCache = sampling.GenerateWithCache(
                prompt, 
                maxTokens, 
                temperature, 
                topK, 
                seed, 
                showPerf: false);

            // Outputs should be identical
            Assert.Equal(outputWithoutCache, outputWithCache);
        }

        [Fact]
        public void KVCache_MultipleTokenGeneration_MaintainsCorrectPosition()
        {
            // Create a small test model
            int seed = 123;
            var model = new TransformerModel(
                vocabSize: 50,
                blockSize: 64,
                nEmbd: 32,
                nLayer: 1,
                nHead: 2,
                dropout: 0.0,
                seed: seed);

            model.Eval();

            // Create inference session
            using var session = new InferenceSession(
                model.NumLayers,
                model.BlockSize,
                model.NumHeads,
                model.HeadDim);

            // Verify initial position
            Assert.Equal(0, session.CurrentPosition);

            // Simulate prefill with 5 tokens
            var promptTokens = new float[] { 1, 2, 3, 4, 5 };
            var promptTensor = new Tensor(promptTokens, new int[] { 1, 5 });
            
            var logits = model.Forward(promptTensor, session, isPrefill: true);

            // Position should be advanced by 5
            Assert.Equal(5, session.CurrentPosition);

            // Simulate decode steps
            for (int i = 0; i < 3; i++)
            {
                var singleToken = new float[] { i + 10 };
                var singleTensor = new Tensor(singleToken, new int[] { 1, 1 });
                
                logits = model.Forward(singleTensor, session, isPrefill: false);
                
                // Position should increment by 1 each time
                Assert.Equal(6 + i, session.CurrentPosition);
            }

            // Final position should be 8
            Assert.Equal(8, session.CurrentPosition);
        }

        [Fact]
        public void InferenceSession_Reset_ClearsStateCorrectly()
        {
            using var session = new InferenceSession(
                numLayers: 2,
                maxSeqLen: 64,
                numHeads: 4,
                headDim: 16);

            // Advance position
            session.AdvancePosition(10);
            Assert.Equal(10, session.CurrentPosition);

            // Write some data to cache
            var keyCache = session.GetKeyCache(0);
            keyCache[0] = 1.5f;
            keyCache[100] = 2.5f;

            // Reset should clear position and cache
            session.Reset();
            
            Assert.Equal(0, session.CurrentPosition);
            Assert.Equal(0.0f, keyCache[0]);
            Assert.Equal(0.0f, keyCache[100]);
        }

        [Fact]
        public void InferenceSession_ExceedsMaxLength_ThrowsException()
        {
            using var session = new InferenceSession(
                numLayers: 2,
                maxSeqLen: 10,
                numHeads: 4,
                headDim: 16);

            // Advance within bounds - should work
            session.AdvancePosition(5);
            session.AdvancePosition(4);
            Assert.Equal(9, session.CurrentPosition);

            // Exceed max length - should throw
            Assert.Throws<InvalidOperationException>(() => 
                session.AdvancePosition(2));
        }

        [Fact]
        public void KVCache_DifferentSeeds_ProduceDifferentOutputs()
        {
            var model = new TransformerModel(
                vocabSize: 50,
                blockSize: 32,
                nEmbd: 64,
                nLayer: 2,
                nHead: 4,
                dropout: 0.0,
                seed: 42);

            model.Eval();

            const string vocab = "abcdefghijklmnopqrstuvwxyz ";
            var tokenizer = new SmallMind.Text.CharTokenizer(vocab);
            var sampling = new Sampling(model, tokenizer, model.BlockSize);

            string prompt = "test";
            int maxTokens = 10;

            // Generate with seed 1
            string output1 = sampling.GenerateWithCache(
                prompt, maxTokens, 1.0, 0, seed: 1, showPerf: false);

            // Generate with seed 2
            string output2 = sampling.GenerateWithCache(
                prompt, maxTokens, 1.0, 0, seed: 2, showPerf: false);

            // Different seeds should produce different outputs
            Assert.NotEqual(output1, output2);
        }

        [Fact]
        public void KVCache_GreedyDecoding_IsDeterministic()
        {
            var model = new TransformerModel(
                vocabSize: 50,
                blockSize: 32,
                nEmbd: 64,
                nLayer: 2,
                nHead: 4,
                dropout: 0.0,
                seed: 42);

            model.Eval();

            const string vocab = "abcdefghijklmnopqrstuvwxyz ";
            var tokenizer = new SmallMind.Text.CharTokenizer(vocab);
            var sampling = new Sampling(model, tokenizer, model.BlockSize);

            string prompt = "abc";
            int maxTokens = 5;

            // Generate twice with same seed and greedy decoding (temperature = 0 approximated by very low)
            // Use seed to make sampling deterministic
            string output1 = sampling.GenerateWithCache(
                prompt, maxTokens, 0.1, 1, seed: 42, showPerf: false);

            string output2 = sampling.GenerateWithCache(
                prompt, maxTokens, 0.1, 1, seed: 42, showPerf: false);

            // Should be identical
            Assert.Equal(output1, output2);
        }
    }
}
