using SmallMind.Core.Exceptions;
using CoreSmallMindException = SmallMind.Core.Exceptions.SmallMindException;

namespace SmallMind.Tests
{
    /// <summary>
    /// Tests for custom exception types.
    /// Validates exception metadata, error codes, and message formatting.
    /// </summary>
    public class ExceptionTests
    {
        #region ValidationException Tests

        [Fact]
        public void ValidationException_WithMessage_SetsProperties()
        {
            // Arrange
            const string message = "Invalid parameter value";
            const string paramName = "testParam";

            // Act
            var exception = new ValidationException(message, paramName);

            // Assert
            Assert.Equal(message, exception.Message);
            Assert.Equal(paramName, exception.ParameterName);
            Assert.Equal("VALIDATION_ERROR", exception.ErrorCode);
        }

        [Fact]
        public void ValidationException_WithInnerException_PreservesInnerException()
        {
            // Arrange
            var inner = new InvalidOperationException("Inner error");
            const string message = "Validation failed";

            // Act
            var exception = new ValidationException(message, inner, "param");

            // Assert
            Assert.Same(inner, exception.InnerException);
            Assert.Equal(message, exception.Message);
        }

        #endregion

        #region ShapeMismatchException Tests

        [Fact]
        public void ShapeMismatchException_WithShapes_IncludesShapeInformation()
        {
            // Arrange
            int[] expected = new[] { 2, 3 };
            int[] actual = new[] { 3, 2 };
            const string operation = "MatMul";

            // Act
            var exception = ShapeMismatchException.Create(operation, expected, actual);

            // Assert
            Assert.Contains(operation, exception.Message);
            Assert.Contains("2, 3", exception.Message); // Expected shape
            Assert.Contains("3, 2", exception.Message); // Actual shape
            Assert.Equal("SHAPE_MISMATCH", exception.ErrorCode);
            Assert.Equal(expected, exception.ExpectedShape);
            Assert.Equal(actual, exception.ActualShape);
        }

        [Fact]
        public void ShapeMismatchException_WithDetailedMessage_SetsProperties()
        {
            // Arrange
            const string message = "Cannot broadcast shapes";
            int[] expected = new[] { 5 };
            int[] actual = new[] { 3 };

            // Act
            var exception = new ShapeMismatchException(message, expected, actual);

            // Assert
            Assert.Contains(message, exception.Message);
            Assert.Equal(expected, exception.ExpectedShape);
            Assert.Equal(actual, exception.ActualShape);
        }

        #endregion

        #region CheckpointException Tests

        [Fact]
        public void CheckpointException_WithPath_IncludesPathInMessage()
        {
            // Arrange
            const string path = "/tmp/checkpoint.json";
            const string message = "Failed to save checkpoint";

            // Act
            var exception = new CheckpointException(message, path);

            // Assert
            Assert.Contains(message, exception.Message);
            Assert.Equal(path, exception.CheckpointPath);
            Assert.Equal("CHECKPOINT_ERROR", exception.ErrorCode);
        }

        [Fact]
        public void CheckpointException_WithInnerException_PreservesStackTrace()
        {
            // Arrange
            var inner = new System.IO.IOException("Disk full");
            const string message = "Checkpoint save failed";
            const string path = "/tmp/model.json";

            // Act
            var exception = new CheckpointException(message, inner, path);

            // Assert
            Assert.Same(inner, exception.InnerException);
            Assert.Contains("Disk full", exception.InnerException.Message);
        }

        #endregion

        #region TrainingException Tests

        [Fact]
        public void TrainingException_WithStep_IncludesStepInformation()
        {
            // Arrange
            const string message = "Training diverged";
            const int step = 42;

            // Act
            var exception = new TrainingException(message, step);

            // Assert
            Assert.Contains(message, exception.Message);
            Assert.Equal(step, exception.Step);
            Assert.Equal("TRAINING_ERROR", exception.ErrorCode);
        }

        [Fact]
        public void TrainingException_WithMetrics_PreservesMetrics()
        {
            // Arrange
            const string message = "Loss explosion detected";
            const int step = 100;

            // Act
            var exception = new TrainingException(message, step);

            // Assert
            Assert.Equal(100, exception.Step);
            Assert.Contains(message, exception.Message);
        }

        #endregion

        #region CoreSmallMindException Base Tests

        [Fact]
        public void SmallMindException_CanBeUsedAsBaseException()
        {
            // Arrange
            const string message = "Custom error";
            const string errorCode = "CUSTOM_ERROR";

            // Act
            var exception = new CoreSmallMindException(message, errorCode);

            // Assert
            Assert.Equal(message, exception.Message);
            Assert.Equal(errorCode, exception.ErrorCode);
        }

        [Fact]
        public void SmallMindException_WithContext_IncludesContextInformation()
        {
            // Arrange
            const string message = "Operation failed";
            const string errorCode = "OP_FAILED";
            var context = new { Layer = "attention", Index = 3 };

            // Act
            var exception = new CoreSmallMindException(message, errorCode);
            exception.Data["Context"] = context;

            // Assert
            Assert.NotNull(exception.Data["Context"]);
        }

        #endregion

        #region Exception Hierarchy Tests

        [Fact]
        public void AllCustomExceptions_InheritFromSmallMindException()
        {
            // Assert
            Assert.IsAssignableFrom<CoreSmallMindException>(new ValidationException("test", "param"));
            Assert.IsAssignableFrom<CoreSmallMindException>(new ShapeMismatchException("test", new[] { 1 }, new[] { 2 }));
            Assert.IsAssignableFrom<CoreSmallMindException>(new CheckpointException("test", "path"));
            Assert.IsAssignableFrom<CoreSmallMindException>(new TrainingException("test", 1));
        }

        [Fact]
        public void AllCustomExceptions_InheritFromException()
        {
            // Assert
            Assert.IsAssignableFrom<Exception>(new ValidationException("test", "param"));
            Assert.IsAssignableFrom<Exception>(new ShapeMismatchException("test", new[] { 1 }, new[] { 2 }));
            Assert.IsAssignableFrom<Exception>(new CheckpointException("test", "path"));
            Assert.IsAssignableFrom<Exception>(new TrainingException("test", 1));
        }

        #endregion

        #region Error Code Tests

        [Fact]
        public void AllExceptions_HaveUniqueErrorCodes()
        {
            // Arrange
            var validationCode = new ValidationException("test", "param").ErrorCode;
            var shapeMismatchCode = new ShapeMismatchException("test", new[] { 1 }, new[] { 2 }).ErrorCode;
            var checkpointCode = new CheckpointException("test", "path").ErrorCode;
            var trainingCode = new TrainingException("test", 1).ErrorCode;

            // Assert - All error codes should be unique
            var codes = new[] { validationCode, shapeMismatchCode, checkpointCode, trainingCode };
            Assert.Equal(codes.Length, codes.Distinct().Count());
        }

        [Theory]
        [InlineData(typeof(ValidationException), "VALIDATION_ERROR")]
        [InlineData(typeof(ShapeMismatchException), "SHAPE_MISMATCH")]
        [InlineData(typeof(CheckpointException), "CHECKPOINT_ERROR")]
        [InlineData(typeof(TrainingException), "TRAINING_ERROR")]
        public void Exception_HasExpectedErrorCode(Type exceptionType, string expectedCode)
        {
            // Arrange & Act
            CoreSmallMindException exception = exceptionType.Name switch
            {
                nameof(ValidationException) => new ValidationException("test", "param"),
                nameof(ShapeMismatchException) => new ShapeMismatchException("test", new[] { 1 }, new[] { 2 }),
                nameof(CheckpointException) => new CheckpointException("test", "path"),
                nameof(TrainingException) => new TrainingException("test", 1),
                _ => throw new ArgumentException("Unknown exception type")
            };

            // Assert
            Assert.Equal(expectedCode, exception.ErrorCode);
        }

        #endregion
    }
}
