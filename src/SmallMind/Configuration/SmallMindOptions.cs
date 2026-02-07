using System;

namespace SmallMind.Configuration
{
    /// <summary>
    /// Root configuration options for SmallMind.
    /// Provides a unified configuration surface for model, training, and inference settings.
    /// </summary>
    public sealed class SmallMindOptions
    {
        /// <summary>
        /// Gets or sets the model configuration options.
        /// </summary>
        public ModelOptions Model { get; set; } = new ModelOptions();
        
        /// <summary>
        /// Gets or sets the training configuration options.
        /// </summary>
        public TrainingOptions Training { get; set; } = new TrainingOptions();
        
        /// <summary>
        /// Gets or sets the inference configuration options.
        /// </summary>
        public InferenceOptions Inference { get; set; } = new InferenceOptions();
        
        /// <summary>
        /// Gets or sets whether to enable performance metrics collection.
        /// </summary>
        public bool EnableMetrics { get; set; } = true;
        
        /// <summary>
        /// Gets or sets whether to enable health checks.
        /// </summary>
        public bool EnableHealthChecks { get; set; } = true;
        
        /// <summary>
        /// Validates all configuration options.
        /// </summary>
        /// <exception cref="Core.Exceptions.ValidationException">Thrown when any options are invalid.</exception>
        public void Validate()
        {
            Model.Validate();
            Training.Validate();
            Inference.Validate();
        }
    }
}
