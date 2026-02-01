using System;
using System.Linq;

namespace SmallMind.Core.Exceptions
{
    /// <summary>
    /// Exception thrown when tensor shapes are incompatible for an operation.
    /// </summary>
    public class ShapeMismatchException : SmallMindException
    {
        /// <summary>
        /// Gets the expected shape.
        /// </summary>
        public int[]? ExpectedShape { get; }

        /// <summary>
        /// Gets the actual shape that was provided.
        /// </summary>
        public int[]? ActualShape { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ShapeMismatchException"/> class.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="expectedShape">The expected tensor shape.</param>
        /// <param name="actualShape">The actual tensor shape that was provided.</param>
        public ShapeMismatchException(string message, int[]? expectedShape = null, int[]? actualShape = null)
            : base(message, "SHAPE_MISMATCH")
        {
            ExpectedShape = expectedShape;
            ActualShape = actualShape;
        }

        /// <summary>
        /// Creates a <see cref="ShapeMismatchException"/> with formatted shape information.
        /// </summary>
        /// <param name="operation">The operation that failed.</param>
        /// <param name="expectedShape">The expected tensor shape.</param>
        /// <param name="actualShape">The actual tensor shape that was provided.</param>
        /// <returns>A new <see cref="ShapeMismatchException"/> instance.</returns>
        public static ShapeMismatchException Create(string operation, int[] expectedShape, int[] actualShape)
        {
            var message = $"{operation}: Expected shape [{string.Join(", ", expectedShape)}] but got [{string.Join(", ", actualShape)}]";
            return new ShapeMismatchException(message, expectedShape, actualShape);
        }
    }
}
