namespace SmallMind.Core.Exceptions
{
    /// <summary>
    /// Exception thrown when checkpoint loading or saving fails.
    /// </summary>
    public class CheckpointException : SmallMindException
    {
        /// <summary>
        /// Gets the path to the checkpoint file that caused the error.
        /// </summary>
        public string? CheckpointPath { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="CheckpointException"/> class.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="checkpointPath">The path to the checkpoint file.</param>
        public CheckpointException(string message, string? checkpointPath = null)
            : base(message, "CHECKPOINT_ERROR")
        {
            CheckpointPath = checkpointPath;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CheckpointException"/> class with a specified error message and inner exception.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        /// <param name="checkpointPath">The path to the checkpoint file.</param>
        public CheckpointException(string message, Exception innerException, string? checkpointPath = null)
            : base(message, innerException, "CHECKPOINT_ERROR")
        {
            CheckpointPath = checkpointPath;
        }
    }
}
