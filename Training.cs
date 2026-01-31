using System;
using System.Collections.Generic;
using System.Linq;
using TorchSharp;
using static TorchSharp.torch;

namespace TinyLLM
{
    /// <summary>
    /// Handles dataset preparation, mini-batching, and the training loop.
    /// Implements next-token prediction with cross-entropy loss.
    /// </summary>
    public class Training
    {
        private readonly TransformerModel _model;
        private readonly Tokenizer _tokenizer;
        private readonly int _blockSize;
        private readonly int _batchSize;
        private readonly List<int> _data;
        private readonly Random _random;

        public Training(TransformerModel model, Tokenizer tokenizer, string trainingText, 
                       int blockSize, int batchSize, int seed)
        {
            _model = model;
            _tokenizer = tokenizer;
            _blockSize = blockSize;
            _batchSize = batchSize;
            _random = new Random(seed);

            // Encode the entire training text
            _data = _tokenizer.Encode(trainingText);
            Console.WriteLine($"Training data: {_data.Count} tokens");

            if (_data.Count < blockSize + 1)
            {
                throw new ArgumentException($"Training data too short. Need at least {blockSize + 1} tokens.");
            }
        }

        /// <summary>
        /// Get a random batch of training data.
        /// Returns input sequences and target sequences (shifted by 1).
        /// </summary>
        private (Tensor, Tensor) GetBatch()
        {
            // Sample random starting positions
            var indices = new List<int>();
            for (int i = 0; i < _batchSize; i++)
            {
                indices.Add(_random.Next(0, _data.Count - _blockSize));
            }

            // Build input and target tensors
            var xData = new long[_batchSize, _blockSize];
            var yData = new long[_batchSize, _blockSize];

            for (int i = 0; i < _batchSize; i++)
            {
                var start = indices[i];
                for (int j = 0; j < _blockSize; j++)
                {
                    xData[i, j] = _data[start + j];
                    yData[i, j] = _data[start + j + 1];
                }
            }

            var x = torch.tensor(xData, dtype: ScalarType.Int64);
            var y = torch.tensor(yData, dtype: ScalarType.Int64);

            return (x, y);
        }

        /// <summary>
        /// Main training loop with periodic loss logging and checkpointing.
        /// </summary>
        public void Train(int steps, double learningRate, int logEvery, int saveEvery, string checkpointDir)
        {
            // Create checkpoint directory if it doesn't exist
            if (!System.IO.Directory.Exists(checkpointDir))
            {
                System.IO.Directory.CreateDirectory(checkpointDir);
            }

            // AdamW optimizer
            var optimizer = optim.AdamW(_model.parameters(), lr: learningRate);

            _model.train();

            Console.WriteLine($"\nStarting training for {steps} steps...");
            Console.WriteLine($"Batch size: {_batchSize}, Block size: {_blockSize}, Learning rate: {learningRate}");

            for (int step = 0; step < steps; step++)
            {
                // Get batch
                var (x, y) = GetBatch();

                // Forward pass
                var logits = _model.forward(x);

                // Compute loss
                // logits: (B, T, vocab_size), y: (B, T)
                // Reshape for cross_entropy: (B*T, vocab_size) and (B*T,)
                var B = logits.shape[0];
                var T = logits.shape[1];
                var V = logits.shape[2];

                var logitsFlat = logits.view(B * T, V);
                var yFlat = y.view(B * T);

                var loss = functional.cross_entropy(logitsFlat, yFlat);

                // Backward pass
                optimizer.zero_grad();
                loss.backward();
                optimizer.step();

                // Logging
                if ((step + 1) % logEvery == 0 || step == 0)
                {
                    Console.WriteLine($"Step {step + 1}/{steps}, Loss: {loss.item<float>():F4}");
                }

                // Checkpointing
                if ((step + 1) % saveEvery == 0)
                {
                    var checkpointPath = System.IO.Path.Combine(checkpointDir, "model.pt");
                    SaveCheckpoint(checkpointPath);
                    Console.WriteLine($"Checkpoint saved to {checkpointPath}");
                }

                // Dispose tensors to free memory
                x.Dispose();
                y.Dispose();
                logits.Dispose();
                loss.Dispose();
                logitsFlat.Dispose();
                yFlat.Dispose();
            }

            // Save final checkpoint
            var finalCheckpointPath = System.IO.Path.Combine(checkpointDir, "model.pt");
            SaveCheckpoint(finalCheckpointPath);
            Console.WriteLine($"\nTraining completed. Final checkpoint saved to {finalCheckpointPath}");
        }

        /// <summary>
        /// Save model checkpoint.
        /// </summary>
        public void SaveCheckpoint(string path)
        {
            _model.save(path);
        }

        /// <summary>
        /// Load model checkpoint.
        /// </summary>
        public void LoadCheckpoint(string path)
        {
            _model.load(path);
            Console.WriteLine($"Checkpoint loaded from {path}");
        }
    }
}
