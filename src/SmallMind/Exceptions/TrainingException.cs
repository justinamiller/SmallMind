using System;

namespace SmallMind.Exceptions
{
    /// <summary>
    /// Exception thrown when training operations fail.
    /// </summary>
    public class TrainingException : SmallMindException
    {
        /// <summary>
        /// Gets the training step at which the error occurred.
        /// </summary>
        public int? Step { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="TrainingException"/> class.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="step">The training step at which the error occurred.</param>
        public TrainingException(string message, int? step = null)
            : base(message, "TRAINING_ERROR")
        {
            Step = step;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TrainingException"/> class with a specified error message and inner exception.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        /// <param name="step">The training step at which the error occurred.</param>
        public TrainingException(string message, Exception innerException, int? step = null)
            : base(message, innerException, "TRAINING_ERROR")
        {
            Step = step;
        }
    }
}
