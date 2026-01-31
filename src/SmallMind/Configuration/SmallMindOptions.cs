using System;

namespace SmallMind.Configuration
{
    public sealed class SmallMindOptions
    {
        public ModelOptions Model { get; set; } = new ModelOptions();
        public TrainingOptions Training { get; set; } = new TrainingOptions();
        public InferenceOptions Inference { get; set; } = new InferenceOptions();
        public bool EnableMetrics { get; set; } = true;
        public bool EnableHealthChecks { get; set; } = true;
        
        public void Validate()
        {
            Model.Validate();
            Training.Validate();
            Inference.Validate();
        }
    }
}
