using System;

namespace SmallMind.Configuration
{
    public sealed class TrainingOptions
    {
        public int BatchSize { get; set; } = 32;
        public double LearningRate { get; set; } = 3e-4;
        public int GradientAccumulationSteps { get; set; } = 1;
        public int WarmupSteps { get; set; } = 100;
        public float MinimumLearningRate { get; set; } = 0.0f;
        public string CheckpointDirectory { get; set; } = "./checkpoints";
        public int LogEvery { get; set; } = 10;
        public int SaveEvery { get; set; } = 100;
        
        public void Validate()
        {
            Validation.Guard.GreaterThan(BatchSize, 0);
            Validation.Guard.GreaterThan(LearningRate, 0.0);
            Validation.Guard.GreaterThan(GradientAccumulationSteps, 0);
        }
    }
}
