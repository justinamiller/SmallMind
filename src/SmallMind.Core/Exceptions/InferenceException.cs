namespace SmallMind.Core.Exceptions
{
    /// <summary>
    /// Base exception for inference-related errors.
    /// </summary>
    public class InferenceException : SmallMindException
    {
        public InferenceException(string message) : base(message) { }
        public InferenceException(string message, Exception innerException) : base(message, innerException) { }
    }
}
