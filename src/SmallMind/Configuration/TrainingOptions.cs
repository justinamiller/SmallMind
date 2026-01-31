using System;

namespace SmallMind.Configuration
{
    /// <summary>
    /// Configuration options for SmallMind training operations.
    /// Controls batch size, learning rate, gradient accumulation, and checkpointing behavior.
    /// </summary>
    public sealed class TrainingOptions
    {
        /// <summary>
        /// Gets or sets the batch size.
        /// </summary>
        public int BatchSize { get; set; } = 32;
        
        /// <summary>
        /// Gets or sets the base learning rate.
        /// </summary>
        public double LearningRate { get; set; } = 3e-4;
        
        /// <summary>
        /// Gets or sets the number of gradient accumulation steps.
        /// </summary>
        public int GradientAccumulationSteps { get; set; } = 1;
        
        /// <summary>
        /// Gets or sets the number of warmup steps for learning rate scheduling.
        /// </summary>
        public int WarmupSteps { get; set; } = 100;
        
        /// <summary>
        /// Gets or sets the minimum learning rate for cosine annealing.
        /// </summary>
        public float MinimumLearningRate { get; set; } = 0.0f;
        
        /// <summary>
        /// Gets or sets the checkpoint directory path.
        /// </summary>
        public string CheckpointDirectory { get; set; } = "./checkpoints";
        
        /// <summary>
        /// Gets or sets how often to log training progress (in steps).
        /// </summary>
        public int LogEvery { get; set; } = 10;
        
        /// <summary>
        /// Gets or sets how often to save checkpoints (in steps).
        /// </summary>
        public int SaveEvery { get; set; } = 100;
        
        /// <summary>
        /// Validates the training options.
        /// </summary>
        /// <exception cref="Exceptions.ValidationException">Thrown when options are invalid.</exception>
        public void Validate()
        {
            Validation.Guard.GreaterThan(BatchSize, 0);
            Validation.Guard.GreaterThan(LearningRate, 0.0);
            Validation.Guard.GreaterThan(GradientAccumulationSteps, 0);
        }
    }
}
