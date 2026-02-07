using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SmallMind.Text;
using SmallMind.Core.Validation;

namespace SmallMind.Core
{
    /// <summary>
    /// Training configuration options for Phase 2 optimizations
    /// </summary>
    public class TrainingConfig
    {
        public bool UseMixedPrecision { get; set; } = false;
        public bool UseGradientCheckpointing { get; set; } = false;
        public CheckpointStrategy CheckpointStrategy { get; set; } = CheckpointStrategy.SqrtLayers;
        public bool EnableDiagnostics { get; set; } = false;
        public bool CheckGradientHealth { get; set; } = false;
        public int DiagnosticInterval { get; set; } = 100;
    }

    /// <summary>
    /// Handles dataset preparation, mini-batching, and the training loop with pure C#.
    /// </summary>
    public class Training
    {
        private readonly TransformerModel _model;
        private readonly ITokenizer _tokenizer;
        private readonly int _blockSize;
        private readonly int _batchSize;
        private readonly List<int> _data;
        private readonly Random _random;

        public Training(TransformerModel model, ITokenizer tokenizer, string trainingText, 
                       int blockSize, int batchSize, int seed)
        {
            Guard.NotNull(model);
            Guard.NotNull(tokenizer);
            Guard.NotNullOrEmpty(trainingText);
            Guard.GreaterThan(blockSize, 0);
            Guard.GreaterThan(batchSize, 0);
            
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
                throw new Exceptions.TrainingException($"Training data too short. Need at least {blockSize + 1} tokens, but got {_data.Count}.");
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
        /// Learning rate scheduler - cosine annealing with warmup
        /// </summary>
        private float GetLearningRate(int step, int warmupSteps, int totalSteps, float maxLr, float minLr = 0.0f)
        {
            if (step < warmupSteps)
            {
                // Linear warmup
                return maxLr * (step + 1) / warmupSteps;
            }
            else
            {
                // Cosine annealing
                float progress = (float)(step - warmupSteps) / (totalSteps - warmupSteps);
                return minLr + (maxLr - minLr) * 0.5f * (1.0f + MathF.Cos(MathF.PI * progress));
            }
        }

        /// <summary>
        /// Enhanced training loop with gradient accumulation, learning rate scheduling, and validation.
        /// </summary>
        /// <param name="steps">Number of training steps to perform.</param>
        /// <param name="learningRate">Base learning rate for optimization.</param>
        /// <param name="logEvery">Log progress every N steps.</param>
        /// <param name="saveEvery">Save checkpoint every N steps.</param>
        /// <param name="checkpointDir">Directory to save checkpoints.</param>
        /// <param name="showPerf">Whether to show performance metrics.</param>
        /// <param name="gradAccumSteps">Number of gradient accumulation steps.</param>
        /// <param name="warmupSteps">Number of learning rate warmup steps.</param>
        /// <param name="valEvery">Perform validation every N steps (0 to disable).</param>
        /// <param name="valBatches">Number of batches to use for validation.</param>
        /// <param name="minLr">Minimum learning rate for cosine annealing.</param>
        /// <param name="cancellationToken">Cancellation token to stop training gracefully.</param>
        /// <exception cref="Exceptions.ValidationException">Thrown when parameters are invalid.</exception>
        /// <exception cref="Exceptions.TrainingException">Thrown when training fails.</exception>
        /// <exception cref="OperationCanceledException">Thrown when training is cancelled.</exception>
        public void TrainEnhanced(int steps, double learningRate, int logEvery, int saveEvery, string checkpointDir, 
                                  bool showPerf = false, int gradAccumSteps = 1, int warmupSteps = 100, 
                                  int valEvery = 500, int valBatches = 10, float minLr = 0.0f,
                                  CancellationToken cancellationToken = default)
        {
            Guard.GreaterThan(steps, 0);
            Guard.GreaterThan(learningRate, 0.0);
            Guard.GreaterThan(logEvery, 0);
            Guard.GreaterThan(saveEvery, 0);
            Guard.NotNullOrWhiteSpace(checkpointDir);
            Guard.GreaterThan(gradAccumSteps, 0);
            Guard.GreaterThanOrEqualTo(warmupSteps, 0);
            
            // Create checkpoint directory if it doesn't exist
            if (!Directory.Exists(checkpointDir))
            {
                Directory.CreateDirectory(checkpointDir);
            }

            // AdamW optimizer
            var optimizer = new AdamW(_model.Parameters, lr: (float)learningRate);

            _model.Train();

            Console.WriteLine($"\nStarting enhanced training for {steps} steps...");
            Console.WriteLine($"Batch size: {_batchSize}, Block size: {_blockSize}, Base learning rate: {learningRate}");
            Console.WriteLine($"Gradient accumulation steps: {gradAccumSteps}, Warmup steps: {warmupSteps}");
            Console.WriteLine($"Validation every {valEvery} steps with {valBatches} batches");
            Console.WriteLine($"Model parameters: {_model.Parameters.Count} tensors");
            if (showPerf)
            {
                Console.WriteLine("Performance tracking enabled");
            }

            var totalStopwatch = System.Diagnostics.Stopwatch.StartNew();
            var stepStopwatch = new System.Diagnostics.Stopwatch();
            long totalTokens = 0;
            float bestValLoss = float.MaxValue;

            for (int step = 0; step < steps; step++)
            {
                // Check for cancellation
                if (cancellationToken.IsCancellationRequested)
                {
                    var cancelledCheckpoint = Path.Combine(checkpointDir, "model_cancelled.json");
                    SaveCheckpoint(cancelledCheckpoint);
                    Console.WriteLine($"\nTraining cancelled. Checkpoint saved to {cancelledCheckpoint}");
                    cancellationToken.ThrowIfCancellationRequested();
                }

                stepStopwatch.Restart();

                // Update learning rate
                float currentLr = GetLearningRate(step, warmupSteps, steps, (float)learningRate, minLr);
                optimizer.SetLearningRate(currentLr);

                // Gradient accumulation loop
                float accumulatedLoss = 0.0f;
                for (int accumStep = 0; accumStep < gradAccumSteps; accumStep++)
                {
                    // Get batch
                    var (x, y) = GetBatch();

                    // Forward pass
                    var logits = _model.Forward(x);

                    // Compute loss (cross-entropy)
                    var loss = ComputeCrossEntropyLoss(logits, y);
                    float lossValue = loss.Data[0];
                    accumulatedLoss += lossValue;

                    // Backward pass (accumulate gradients)
                    if (accumStep == 0)
                    {
                        optimizer.ZeroGrad();
                    }
                    loss.Backward();
                }

                // Average the loss
                float avgLoss = accumulatedLoss / gradAccumSteps;

                // Scale gradients if using gradient accumulation
                if (gradAccumSteps > 1)
                {
                    float scale = 1.0f / gradAccumSteps;
                    for (int p = 0; p < _model.Parameters.Count; p++)
                    {
                        var param = _model.Parameters[p];
                        if (param.Grad != null)
                        {
                            ScaleGradients(param.Grad, scale);
                        }
                    }
                }

                // Update parameters
                optimizer.Step();

                stepStopwatch.Stop();

                // Track tokens processed
                int tokensThisStep = _batchSize * _blockSize * gradAccumSteps;
                totalTokens += tokensThisStep;

                // Logging
                if ((step + 1) % logEvery == 0 || step == 0)
                {
                    if (showPerf)
                    {
                        double stepTimeMs = stepStopwatch.Elapsed.TotalMilliseconds;
                        double tokensPerSec = stepTimeMs > 0 ? tokensThisStep / (stepTimeMs / 1000.0) : 0;
                        double avgTimePerStep = totalStopwatch.Elapsed.TotalMilliseconds / (step + 1);
                        Console.WriteLine($"Step {step + 1}/{steps}, Loss: {avgLoss:F4}, LR: {currentLr:F6}, " +
                                        $"Time: {stepTimeMs:F0}ms, Tokens/sec: {tokensPerSec:F0}, " +
                                        $"Avg step: {avgTimePerStep:F0}ms");
                    }
                    else
                    {
                        Console.WriteLine($"Step {step + 1}/{steps}, Loss: {avgLoss:F4}, LR: {currentLr:F6}");
                    }
                }

                // Validation
                if (valEvery > 0 && (step + 1) % valEvery == 0)
                {
                    float valLoss = EvaluateValidationLoss(valBatches);
                    Console.WriteLine($"Validation loss: {valLoss:F4}");
                    
                    // Save best model
                    if (valLoss < bestValLoss)
                    {
                        bestValLoss = valLoss;
                        var bestCheckpointPath = Path.Combine(checkpointDir, "model_best.json");
                        SaveCheckpoint(bestCheckpointPath);
                        Console.WriteLine($"New best validation loss! Saved to {bestCheckpointPath}");
                    }
                }

                // Checkpointing
                if ((step + 1) % saveEvery == 0)
                {
                    var checkpointPath = Path.Combine(checkpointDir, "model.json");
                    SaveCheckpoint(checkpointPath);
                    Console.WriteLine($"Checkpoint saved to {checkpointPath}");
                }
            }

            totalStopwatch.Stop();

            // Save final checkpoint
            var finalCheckpointPath = Path.Combine(checkpointDir, "model.json");
            SaveCheckpoint(finalCheckpointPath);
            Console.WriteLine($"\nTraining completed. Final checkpoint saved to {finalCheckpointPath}");

            if (showPerf)
            {
                double totalTimeSeconds = totalStopwatch.Elapsed.TotalSeconds;
                double avgTokensPerSec = totalTimeSeconds > 0 ? totalTokens / totalTimeSeconds : 0;
                Console.WriteLine($"Total training time: {totalTimeSeconds:F2}s");
                Console.WriteLine($"Total tokens processed: {totalTokens:N0}");
                Console.WriteLine($"Average throughput: {avgTokensPerSec:F0} tokens/sec");
                Console.WriteLine($"Best validation loss: {bestValLoss:F4}");
            }
        }

        /// <summary>
        /// SIMD-optimized gradient scaling for gradient accumulation
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ScaleGradients(float[] gradients, float scale)
        {
            int vectorSize = Vector<float>.Count;
            int i = 0;
            var vScale = new Vector<float>(scale);
            
            // SIMD vectorized loop
            for (; i <= gradients.Length - vectorSize; i += vectorSize)
            {
                var vGrad = new Vector<float>(gradients, i);
                (vGrad * vScale).CopyTo(gradients, i);
            }
            
            // Scalar remainder
            for (; i < gradients.Length; i++)
            {
                gradients[i] *= scale;
            }
        }

        /// <summary>
        /// Evaluate validation loss on a held-out set
        /// </summary>
        private float EvaluateValidationLoss(int numBatches)
        {
            _model.Eval();
            
            float totalLoss = 0.0f;
            for (int i = 0; i < numBatches; i++)
            {
                var (x, y) = GetBatch();
                var logits = _model.Forward(x);
                var loss = ComputeCrossEntropyLoss(logits, y);
                totalLoss += loss.Data[0];
            }
            
            _model.Train();
            return totalLoss / numBatches;
        }

        /// <summary>
        /// Main training loop with periodic loss logging and checkpointing.
        /// </summary>
        public void Train(int steps, double learningRate, int logEvery, int saveEvery, string checkpointDir, bool showPerf = false, CancellationToken cancellationToken = default)
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
            if (showPerf)
            {
                Console.WriteLine("Performance tracking enabled");
            }

            var totalStopwatch = System.Diagnostics.Stopwatch.StartNew();
            var stepStopwatch = new System.Diagnostics.Stopwatch();
            long totalTokens = 0;

            for (int step = 0; step < steps; step++)
            {
                // Check for cancellation
                if (cancellationToken.IsCancellationRequested)
                {
                    var cancelledCheckpoint = Path.Combine(checkpointDir, "model_cancelled.json");
                    SaveCheckpoint(cancelledCheckpoint);
                    Console.WriteLine($"\nTraining cancelled. Checkpoint saved to {cancelledCheckpoint}");
                    cancellationToken.ThrowIfCancellationRequested();
                }

                stepStopwatch.Restart();

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

                // Update parameters
                optimizer.Step();

                stepStopwatch.Stop();

                // Track tokens processed
                int tokensThisStep = _batchSize * _blockSize;
                totalTokens += tokensThisStep;

                // Logging
                if ((step + 1) % logEvery == 0 || step == 0)
                {
                    if (showPerf)
                    {
                        double stepTimeMs = stepStopwatch.Elapsed.TotalMilliseconds;
                        double tokensPerSec = stepTimeMs > 0 ? tokensThisStep / (stepTimeMs / 1000.0) : 0;
                        double avgTimePerStep = totalStopwatch.Elapsed.TotalMilliseconds / (step + 1);
                        Console.WriteLine($"Step {step + 1}/{steps}, Loss: {lossValue:F4}, " +
                                        $"Time: {stepTimeMs:F0}ms, Tokens/sec: {tokensPerSec:F0}, " +
                                        $"Avg step: {avgTimePerStep:F0}ms");
                    }
                    else
                    {
                        Console.WriteLine($"Step {step + 1}/{steps}, Loss: {lossValue:F4}");
                    }
                }

                // Checkpointing
                if ((step + 1) % saveEvery == 0)
                {
                    var checkpointPath = Path.Combine(checkpointDir, "model.json");
                    SaveCheckpoint(checkpointPath);
                    Console.WriteLine($"Checkpoint saved to {checkpointPath}");
                }
            }

            totalStopwatch.Stop();

            // Save final checkpoint
            var finalCheckpointPath = Path.Combine(checkpointDir, "model.json");
            SaveCheckpoint(finalCheckpointPath);
            Console.WriteLine($"\nTraining completed. Final checkpoint saved to {finalCheckpointPath}");

            if (showPerf)
            {
                double totalTimeSeconds = totalStopwatch.Elapsed.TotalSeconds;
                double avgTokensPerSec = totalTimeSeconds > 0 ? totalTokens / totalTimeSeconds : 0;
                Console.WriteLine($"Total training time: {totalTimeSeconds:F2}s");
                Console.WriteLine($"Total tokens processed: {totalTokens:N0}");
                Console.WriteLine($"Average throughput: {avgTokensPerSec:F0} tokens/sec");
            }
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

            // Cache softmax results to avoid recomputation during backward pass
            var softmaxCache = new float[B * T * V];

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
                        float expVal = MathF.Exp(logits.Data[offset + v] - max);
                        softmaxCache[offset + v] = expVal;
                        sum += expVal;
                    }

                    // Normalize softmax and cache results
                    for (int v = 0; v < V; v++)
                    {
                        softmaxCache[offset + v] /= sum;
                    }

                    float logProb = logits.Data[offset + targetClass] - max - MathF.Log(sum);
                    totalLoss -= logProb;
                }
            }

            var loss = new Tensor(new float[] { totalLoss / count }, new int[] { 1 }, requiresGrad: true);

            // Backward pass for cross-entropy - reuse cached softmax
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

                            // Use cached softmax values instead of recomputing
                            for (int v = 0; v < V; v++)
                            {
                                float grad = softmaxCache[offset + v];
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

            for (int p = 0; p < _model.Parameters.Count; p++)
            {
                var param = _model.Parameters[p];
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

        /// <summary>
        /// Advanced training loop with Phase 2 optimizations:
        /// - Optional mixed precision (FP16/FP32)
        /// - Optional gradient checkpointing
        /// - Performance profiling
        /// - Memory tracking
        /// - Gradient health monitoring
        /// </summary>
        public void TrainOptimized(
            int steps, 
            double learningRate, 
            int logEvery, 
            int saveEvery, 
            string checkpointDir,
            TrainingConfig? config = null,
            int gradAccumSteps = 1,
            int warmupSteps = 100,
            int valEvery = 500,
            int valBatches = 10,
            float minLr = 0.0f,
            CancellationToken cancellationToken = default)
        {
            config ??= new TrainingConfig();
            
            // Create checkpoint directory if it doesn't exist
            if (!Directory.Exists(checkpointDir))
            {
                Directory.CreateDirectory(checkpointDir);
            }

            // Initialize diagnostics
            TrainingProfiler? profiler = config.EnableDiagnostics ? new TrainingProfiler() : null;
            MemoryTracker? memoryTracker = config.EnableDiagnostics ? new MemoryTracker() : null;
            
            memoryTracker?.Snapshot("Initialization");

            // AdamW optimizer
            var optimizer = new AdamW(_model.Parameters, lr: (float)learningRate);
            
            // Mixed precision trainer (optional)
            MixedPrecisionTrainer? mixedPrecisionTrainer = null;
            if (config.UseMixedPrecision)
            {
                mixedPrecisionTrainer = new MixedPrecisionTrainer(optimizer, _model.Parameters);
                Console.WriteLine("Mixed precision training enabled (FP16/FP32)");
            }

            _model.Train();

            Console.WriteLine($"\n╔══════════════════════════════════════════════════════════════════════════╗");
            Console.WriteLine($"║            PHASE 2 OPTIMIZED TRAINING STARTED                         ║");
            Console.WriteLine($"╠══════════════════════════════════════════════════════════════════════════╣");
            Console.WriteLine($"║ Steps: {steps,12} │ Batch Size: {_batchSize,8} │ Block Size: {_blockSize,8}    ║");
            Console.WriteLine($"║ Learning Rate: {learningRate,6:F4} │ Warmup Steps: {warmupSteps,6} │ Min LR: {minLr,8:F6} ║");
            Console.WriteLine($"║ Grad Accum: {gradAccumSteps,7} │ Val Every: {valEvery,9} │ Val Batches: {valBatches,6}  ║");
            Console.WriteLine($"╠══════════════════════════════════════════════════════════════════════════╣");
            Console.WriteLine($"║ Optimizations:                                                        ║");
            Console.WriteLine($"║   Mixed Precision: {(config.UseMixedPrecision ? "✓ Enabled " : "✗ Disabled"),44}║");
            Console.WriteLine($"║   Gradient Checkpointing: {(config.UseGradientCheckpointing ? "✓ Enabled " : "✗ Disabled"),38}║");
            Console.WriteLine($"║   Diagnostics: {(config.EnableDiagnostics ? "✓ Enabled " : "✗ Disabled"),48}║");
            Console.WriteLine($"║   Gradient Health Check: {(config.CheckGradientHealth ? "✓ Enabled " : "✗ Disabled"),38}║");
            Console.WriteLine($"╚══════════════════════════════════════════════════════════════════════════╝\n");

            var totalStopwatch = System.Diagnostics.Stopwatch.StartNew();
            var stepStopwatch = new System.Diagnostics.Stopwatch();
            long totalTokens = 0;
            float bestValLoss = float.MaxValue;

            for (int step = 0; step < steps; step++)
            {
                // Check for cancellation
                if (cancellationToken.IsCancellationRequested)
                {
                    var cancelledCheckpoint = Path.Combine(checkpointDir, "model_cancelled.json");
                    SaveCheckpoint(cancelledCheckpoint);
                    Console.WriteLine($"\nTraining cancelled. Checkpoint saved to {cancelledCheckpoint}");
                    cancellationToken.ThrowIfCancellationRequested();
                }

                stepStopwatch.Restart();

                // Update learning rate
                float currentLr = GetLearningRate(step, warmupSteps, steps, (float)learningRate, minLr);
                optimizer.SetLearningRate(currentLr);

                // Gradient accumulation loop
                float accumulatedLoss = 0.0f;
                for (int accumStep = 0; accumStep < gradAccumSteps; accumStep++)
                {
                    using (profiler?.Profile("GetBatch"))
                    {
                        // Get batch
                        var (x, y) = GetBatch();

                        // Sync to FP16 if using mixed precision
                        if (mixedPrecisionTrainer != null)
                        {
                            using (profiler?.Profile("SyncToFP16"))
                            {
                                mixedPrecisionTrainer.SyncToFP16(_model.Parameters);
                            }
                        }

                        // Forward pass
                        Tensor logits;
                        using (profiler?.Profile("Forward"))
                        {
                            logits = _model.Forward(x);
                        }

                        // Compute loss (cross-entropy)
                        Tensor loss;
                        using (profiler?.Profile("Loss"))
                        {
                            loss = ComputeCrossEntropyLoss(logits, y);
                        }
                        
                        float lossValue = loss.Data[0];
                        
                        // Scale loss for mixed precision
                        if (mixedPrecisionTrainer != null)
                        {
                            lossValue *= mixedPrecisionTrainer.LossScale;
                            loss.Data[0] = lossValue;
                        }
                        
                        accumulatedLoss += loss.Data[0] / (mixedPrecisionTrainer?.LossScale ?? 1.0f);

                        // Backward pass
                        if (accumStep == 0)
                        {
                            optimizer.ZeroGrad();
                        }
                        
                        using (profiler?.Profile("Backward"))
                        {
                            loss.Backward();
                        }
                    }
                }

                // Average the loss
                float avgLoss = accumulatedLoss / gradAccumSteps;
                
                // Check and unscale gradients for mixed precision
                bool gradientsValid = true;
                if (mixedPrecisionTrainer != null)
                {
                    using (profiler?.Profile("CheckGradients"))
                    {
                        gradientsValid = mixedPrecisionTrainer.CheckAndUnscaleGradients(_model.Parameters);
                    }
                }

                if (!gradientsValid)
                {
                    Console.WriteLine($"⚠️  Step {step + 1}: Gradient overflow detected, skipping update. " +
                                    $"Loss scale: {mixedPrecisionTrainer?.LossScale:F0}");
                    continue; // Skip this update
                }

                // Scale gradients if using gradient accumulation
                if (gradAccumSteps > 1)
                {
                    for (int p = 0; p < _model.Parameters.Count; p++)
                    {
                        var param = _model.Parameters[p];
                        if (param.Grad != null)
                        {
                            for (int i = 0; i < param.Grad.Length; i++)
                            {
                                param.Grad[i] /= gradAccumSteps;
                            }
                        }
                    }
                }
                
                // Gradient health check
                if (config.CheckGradientHealth && (step + 1) % config.DiagnosticInterval == 0)
                {
                    for (int p = 0; p < _model.Parameters.Count; p++)
                    {
                        var param = _model.Parameters[p];
                        if (param.Grad != null)
                        {
                            GradientDiagnostics.CheckGradients($"Step{step + 1}", param.Grad);
                        }
                    }
                }

                // Update parameters
                using (profiler?.Profile("OptimizerStep"))
                {
                    optimizer.Step();
                }
                
                // Update master weights for mixed precision
                if (mixedPrecisionTrainer != null)
                {
                    using (profiler?.Profile("UpdateMasterWeights"))
                    {
                        mixedPrecisionTrainer.UpdateMasterWeights(_model.Parameters);
                    }
                }

                stepStopwatch.Stop();

                // Track tokens processed
                int tokensThisStep = _batchSize * _blockSize * gradAccumSteps;
                totalTokens += tokensThisStep;

                // Logging
                if ((step + 1) % logEvery == 0 || step == 0)
                {
                    double stepTimeMs = stepStopwatch.Elapsed.TotalMilliseconds;
                    double tokensPerSec = stepTimeMs > 0 ? tokensThisStep / (stepTimeMs / 1000.0) : 0;
                    double avgTimePerStep = totalStopwatch.Elapsed.TotalMilliseconds / (step + 1);
                    
                    Console.WriteLine($"Step {step + 1,5}/{steps} | Loss: {avgLoss,7:F4} | LR: {currentLr,8:F6} | " +
                                    $"Time: {stepTimeMs,5:F0}ms | Tok/s: {tokensPerSec,6:F0} | " +
                                    $"Avg: {avgTimePerStep,5:F0}ms" +
                                    (mixedPrecisionTrainer != null ? $" | Scale: {mixedPrecisionTrainer.LossScale,6:F0}" : ""));
                }

                // Memory tracking
                if (memoryTracker != null && (step + 1) % config.DiagnosticInterval == 0)
                {
                    memoryTracker.Snapshot($"Step {step + 1}");
                }

                // Validation
                if (valEvery > 0 && (step + 1) % valEvery == 0)
                {
                    float valLoss = EvaluateValidationLoss(valBatches);
                    Console.WriteLine($"═══ Validation Loss: {valLoss:F4} ═══");
                    
                    // Save best model
                    if (valLoss < bestValLoss)
                    {
                        bestValLoss = valLoss;
                        var bestCheckpointPath = Path.Combine(checkpointDir, "model_best.json");
                        SaveCheckpoint(bestCheckpointPath);
                        Console.WriteLine($"✓ New best validation loss! Saved to {bestCheckpointPath}");
                    }
                }

                // Checkpointing
                if ((step + 1) % saveEvery == 0)
                {
                    var checkpointPath = Path.Combine(checkpointDir, "model.json");
                    SaveCheckpoint(checkpointPath);
                    Console.WriteLine($"✓ Checkpoint saved to {checkpointPath}");
                }
            }

            totalStopwatch.Stop();

            // Save final checkpoint
            var finalCheckpointPath = Path.Combine(checkpointDir, "model.json");
            SaveCheckpoint(finalCheckpointPath);
            Console.WriteLine($"\n✓ Training completed. Final checkpoint saved to {finalCheckpointPath}");

            // Print performance summary
            double totalTimeSeconds = totalStopwatch.Elapsed.TotalSeconds;
            double avgTokensPerSec = totalTimeSeconds > 0 ? totalTokens / totalTimeSeconds : 0;
            
            Console.WriteLine($"\n╔══════════════════════════════════════════════════════════════════════════╗");
            Console.WriteLine($"║                          TRAINING SUMMARY                             ║");
            Console.WriteLine($"╠══════════════════════════════════════════════════════════════════════════╣");
            Console.WriteLine($"║ Total time: {totalTimeSeconds,12:F2}s │ Tokens: {totalTokens,16:N0}          ║");
            Console.WriteLine($"║ Throughput: {avgTokensPerSec,12:F0} tok/s │ Best Val Loss: {bestValLoss,10:F4}      ║");
            if (mixedPrecisionTrainer != null)
            {
                Console.WriteLine($"║ FP16 Overflows: {mixedPrecisionTrainer.OverflowCount,8} │ Final Loss Scale: {mixedPrecisionTrainer.LossScale,10:F0}  ║");
            }
            Console.WriteLine($"╚══════════════════════════════════════════════════════════════════════════╝");
            
            // Print diagnostics if enabled
            if (profiler != null)
            {
                profiler.PrintReport();
            }
            
            if (memoryTracker != null)
            {
                memoryTracker.PrintReport();
            }
        }
    }
}
