using System;

namespace SmallMind.Configuration
{
    public sealed class InferenceOptions
    {
        public double Temperature { get; set; } = 0.8;
        public int TopK { get; set; } = 40;
        public double TopP { get; set; } = 0.95;
        public int MaxTokens { get; set; } = 100;
        
        public void Validate()
        {
            Validation.Guard.GreaterThan(Temperature, 0.0);
            Validation.Guard.GreaterThan(MaxTokens, 0);
        }
    }
}
