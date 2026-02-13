using System;

namespace SmallMind.Benchmarks.Core.Measurement
{
    /// <summary>
    /// Defines a benchmark scenario configuration.
    /// </summary>
    public sealed class BenchmarkScenario
    {
        /// <summary>
        /// Unique name for this scenario.
        /// </summary>
        public string ScenarioName { get; set; } = string.Empty;

        /// <summary>
        /// Path to the GGUF model file.
        /// </summary>
        public string ModelPath { get; set; } = string.Empty;

        /// <summary>
        /// Model context size (maximum tokens in context window).
        /// </summary>
        public int ContextSize { get; set; } = 1024;

        /// <summary>
        /// Number of threads to use for inference (0 = auto-detect).
        /// </summary>
        public int ThreadCount { get; set; } = 0;

        /// <summary>
        /// Input prompt text to generate from.
        /// </summary>
        public string PromptText { get; set; } = "Once upon a time";

        /// <summary>
        /// Number of tokens to generate after the prompt.
        /// </summary>
        public int NumTokensToGenerate { get; set; } = 50;

        /// <summary>
        /// Number of warmup iterations (excluded from measurements).
        /// </summary>
        public int WarmupIterations { get; set; } = 2;

        /// <summary>
        /// Number of measured iterations.
        /// </summary>
        public int MeasuredIterations { get; set; } = 5;

        /// <summary>
        /// Sampling temperature (higher = more random).
        /// </summary>
        public double Temperature { get; set; } = 0.8;

        /// <summary>
        /// Top-K sampling (0 to disable).
        /// </summary>
        public int TopK { get; set; } = 40;

        /// <summary>
        /// Top-P nucleus sampling (1.0 to disable).
        /// </summary>
        public double TopP { get; set; } = 0.95;

        /// <summary>
        /// Random seed for reproducibility (null for non-deterministic).
        /// </summary>
        public int? Seed { get; set; }

        /// <summary>
        /// Optional cache directory for converted SMQ files.
        /// If null, uses system temp directory.
        /// </summary>
        public string? CacheDirectory { get; set; }

        /// <summary>
        /// Validates the scenario configuration.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when configuration is invalid.</exception>
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(ScenarioName))
            {
                throw new ArgumentException("ScenarioName cannot be null or empty", nameof(ScenarioName));
            }

            if (string.IsNullOrWhiteSpace(ModelPath))
            {
                throw new ArgumentException("ModelPath cannot be null or empty", nameof(ModelPath));
            }

            if (!System.IO.File.Exists(ModelPath))
            {
                throw new System.IO.FileNotFoundException($"Model file not found: {ModelPath}", ModelPath);
            }

            if (ContextSize <= 0)
            {
                throw new ArgumentException("ContextSize must be greater than 0", nameof(ContextSize));
            }

            if (ThreadCount < 0)
            {
                throw new ArgumentException("ThreadCount cannot be negative", nameof(ThreadCount));
            }

            if (NumTokensToGenerate <= 0)
            {
                throw new ArgumentException("NumTokensToGenerate must be greater than 0", nameof(NumTokensToGenerate));
            }

            if (WarmupIterations < 0)
            {
                throw new ArgumentException("WarmupIterations cannot be negative", nameof(WarmupIterations));
            }

            if (MeasuredIterations <= 0)
            {
                throw new ArgumentException("MeasuredIterations must be greater than 0", nameof(MeasuredIterations));
            }

            if (Temperature <= 0.0)
            {
                throw new ArgumentException("Temperature must be greater than 0", nameof(Temperature));
            }

            if (TopP < 0.0 || TopP > 1.0)
            {
                throw new ArgumentException("TopP must be between 0.0 and 1.0", nameof(TopP));
            }
        }

        /// <summary>
        /// Creates a default scenario for testing.
        /// </summary>
        public static BenchmarkScenario CreateDefault(string modelPath)
        {
            return new BenchmarkScenario
            {
                ScenarioName = "default",
                ModelPath = modelPath,
                ContextSize = 1024,
                ThreadCount = 0,
                PromptText = "Once upon a time",
                NumTokensToGenerate = 50,
                WarmupIterations = 2,
                MeasuredIterations = 5,
                Temperature = 0.8,
                TopK = 40,
                TopP = 0.95,
                Seed = null,
                CacheDirectory = null
            };
        }
    }
}
