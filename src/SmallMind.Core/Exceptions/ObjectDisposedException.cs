namespace SmallMind.Core.Exceptions
{
    /// <summary>
    /// Exception thrown when an operation is attempted on a disposed object.
    /// </summary>
    public class SmallMindObjectDisposedException : SmallMindException
    {
        /// <summary>
        /// Gets the name of the disposed object.
        /// </summary>
        public string ObjectName { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SmallMindObjectDisposedException"/> class.
        /// </summary>
        /// <param name="objectName">The name of the disposed object.</param>
        public SmallMindObjectDisposedException(string objectName)
            : base($"Cannot access disposed object: {objectName}", "OBJECT_DISPOSED")
        {
            ObjectName = objectName;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SmallMindObjectDisposedException"/> class with a custom message.
        /// </summary>
        /// <param name="objectName">The name of the disposed object.</param>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        public SmallMindObjectDisposedException(string objectName, string message)
            : base(message, "OBJECT_DISPOSED")
        {
            ObjectName = objectName;
        }
    }
}
