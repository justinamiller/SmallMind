using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace TinyLLM
{
    /// <summary>
    /// Handles dataset preparation, mini-batching, and the training loop with pure C#.
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
        /// </summary>
        private (Tensor, Tensor) GetBatch()
        {
            // Sample random starting positions
            var xData = new float[_batchSize * _blockSize];
            var yData = new float[_batchSize * _blockSize];

            for (int i = 0; i < _batchSize; i++)
            {
                int start = _random.Next(0, _data.Count - _blockSize);
                for (int j = 0; j < _blockSize; j++)
                {
                    xData[i * _blockSize + j] = _data[start + j];
                    yData[i * _blockSize + j] = _data[start + j + 1];
                }
            }

            var x = new Tensor(xData, new int[] { _batchSize, _blockSize });
            var y = new Tensor(yData, new int[] { _batchSize, _blockSize });

            return (x, y);
        }

        /// <summary>
        /// Main training loop with periodic loss logging and checkpointing.
        /// </summary>
        public void Train(int steps, double learningRate, int logEvery, int saveEvery, string checkpointDir)
        {
            // Create checkpoint directory if it doesn't exist
            if (!Directory.Exists(checkpointDir))
            {
                Directory.CreateDirectory(checkpointDir);
            }

            // AdamW optimizer
            var optimizer = new AdamW(_model.Parameters, lr: (float)learningRate);

            _model.Train();

            Console.WriteLine($"\nStarting training for {steps} steps...");
            Console.WriteLine($"Batch size: {_batchSize}, Block size: {_blockSize}, Learning rate: {learningRate}");
            Console.WriteLine($"Model parameters: {_model.Parameters.Count} tensors");

            for (int step = 0; step < steps; step++)
            {
                // Get batch
                var (x, y) = GetBatch();

                // Forward pass
                var logits = _model.Forward(x);

                // Compute loss (cross-entropy)
                var loss = ComputeCrossEntropyLoss(logits, y);
                float lossValue = loss.Data[0];

                // Backward pass
                optimizer.ZeroGrad();
                loss.Backward();

                // Backpropagate through the model
                logits.Backward(loss.Grad);

                // Update parameters
                optimizer.Step();

                // Logging
                if ((step + 1) % logEvery == 0 || step == 0)
                {
                    Console.WriteLine($"Step {step + 1}/{steps}, Loss: {lossValue:F4}");
                }

                // Checkpointing
                if ((step + 1) % saveEvery == 0)
                {
                    var checkpointPath = Path.Combine(checkpointDir, "model.json");
                    SaveCheckpoint(checkpointPath);
                    Console.WriteLine($"Checkpoint saved to {checkpointPath}");
                }
            }

            // Save final checkpoint
            var finalCheckpointPath = Path.Combine(checkpointDir, "model.json");
            SaveCheckpoint(finalCheckpointPath);
            Console.WriteLine($"\nTraining completed. Final checkpoint saved to {finalCheckpointPath}");
        }

        /// <summary>
        /// Compute cross-entropy loss
        /// </summary>
        private Tensor ComputeCrossEntropyLoss(Tensor logits, Tensor targets)
        {
            // logits: (B, T, vocab_size)
            // targets: (B, T) with class indices
            
            int B = logits.Shape[0];
            int T = logits.Shape[1];
            int V = logits.Shape[2];

            float totalLoss = 0;
            int count = B * T;

            // Compute loss for each position
            for (int b = 0; b < B; b++)
            {
                for (int t = 0; t < T; t++)
                {
                    int targetClass = (int)targets.Data[b * T + t];
                    if (targetClass < 0 || targetClass >= V) continue;

                    int offset = (b * T + t) * V;

                    // Softmax over vocab dimension
                    float max = float.NegativeInfinity;
                    for (int v = 0; v < V; v++)
                    {
                        max = Math.Max(max, logits.Data[offset + v]);
                    }

                    float sum = 0;
                    for (int v = 0; v < V; v++)
                    {
                        sum += MathF.Exp(logits.Data[offset + v] - max);
                    }

                    float logProb = logits.Data[offset + targetClass] - max - MathF.Log(sum);
                    totalLoss -= logProb;
                }
            }

            var loss = new Tensor(new float[] { totalLoss / count }, new int[] { 1 }, requiresGrad: true);

            // Backward pass for cross-entropy
            loss.SetBackward(() =>
            {
                if (logits.RequiresGrad)
                {
                    if (logits.Grad == null)
                    {
                        logits.Grad = new float[logits.Size];
                    }

                    float gradScale = loss.Grad[0] / count;

                    for (int b = 0; b < B; b++)
                    {
                        for (int t = 0; t < T; t++)
                        {
                            int targetClass = (int)targets.Data[b * T + t];
                            if (targetClass < 0 || targetClass >= V) continue;

                            int offset = (b * T + t) * V;

                            // Softmax
                            float max = float.NegativeInfinity;
                            for (int v = 0; v < V; v++)
                            {
                                max = Math.Max(max, logits.Data[offset + v]);
                            }

                            float sum = 0;
                            var softmax = new float[V];
                            for (int v = 0; v < V; v++)
                            {
                                softmax[v] = MathF.Exp(logits.Data[offset + v] - max);
                                sum += softmax[v];
                            }

                            for (int v = 0; v < V; v++)
                            {
                                softmax[v] /= sum;
                                float grad = softmax[v];
                                if (v == targetClass)
                                {
                                    grad -= 1.0f;
                                }
                                logits.Grad[offset + v] += grad * gradScale;
                            }
                        }
                    }
                }
            });

            return loss;
        }

        /// <summary>
        /// Save model checkpoint as JSON
        /// </summary>
        public void SaveCheckpoint(string path)
        {
            var checkpoint = new Dictionary<string, object>();
            checkpoint["parameters"] = new List<object>();

            foreach (var param in _model.Parameters)
            {
                var paramData = new Dictionary<string, object>
                {
                    ["shape"] = param.Shape,
                    ["data"] = param.Data
                };
                ((List<object>)checkpoint["parameters"]).Add(paramData);
            }

            var json = JsonSerializer.Serialize(checkpoint, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }

        /// <summary>
        /// Load model checkpoint from JSON
        /// </summary>
        public void LoadCheckpoint(string path)
        {
            var json = File.ReadAllText(path);
            var checkpoint = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

            if (checkpoint == null || !checkpoint.ContainsKey("parameters"))
            {
                throw new InvalidOperationException("Invalid checkpoint format");
            }

            var parameters = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(checkpoint["parameters"].ToString()!);
            
            if (parameters == null || parameters.Count != _model.Parameters.Count)
            {
                Console.WriteLine($"Warning: Checkpoint has {parameters?.Count ?? 0} parameters, model has {_model.Parameters.Count}");
                return;
            }

            for (int i = 0; i < parameters.Count && i < _model.Parameters.Count; i++)
            {
                var paramData = parameters[i];
                var data = JsonSerializer.Deserialize<float[]>(paramData["data"].ToString()!);
                
                if (data != null && data.Length == _model.Parameters[i].Size)
                {
                    Array.Copy(data, _model.Parameters[i].Data, data.Length);
                }
            }

            Console.WriteLine($"Checkpoint loaded from {path}");
        }
    }
}
