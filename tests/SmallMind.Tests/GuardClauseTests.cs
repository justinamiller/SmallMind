using SmallMind.Core.Exceptions;
using SmallMind.Core.Validation;

namespace SmallMind.Tests
{
    /// <summary>
    /// Unit tests for Guard clause validation.
    /// Tests null, empty, range, and validation behavior on public APIs.
    /// </summary>
    public class GuardClauseTests
    {
        #region NotNull Tests

        [Fact]
        public void NotNull_WithNullValue_ThrowsValidationException()
        {
            // Arrange
            object? nullValue = null;

            // Act & Assert
            var ex = Assert.Throws<ValidationException>(() => Guard.NotNull(nullValue));
            Assert.Contains("cannot be null", ex.Message);
        }

        [Fact]
        public void NotNull_WithNonNullValue_ReturnsValue()
        {
            // Arrange
            var value = new object();

            // Act
            var result = Guard.NotNull(value);

            // Assert
            Assert.Same(value, result);
        }

        #endregion

        #region NotNullOrEmpty String Tests

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void NotNullOrEmpty_String_WithNullOrEmpty_ThrowsValidationException(string? value)
        {
            // Act & Assert
            var ex = Assert.Throws<ValidationException>(() => Guard.NotNullOrEmpty(value));
            Assert.Contains("cannot be null or empty", ex.Message);
        }

        [Theory]
        [InlineData("a")]
        [InlineData("  ")]
        [InlineData("valid string")]
        public void NotNullOrEmpty_String_WithValidValue_ReturnsValue(string value)
        {
            // Act
            var result = Guard.NotNullOrEmpty(value);

            // Assert
            Assert.Equal(value, result);
        }

        #endregion

        #region NotNullOrWhiteSpace Tests

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("  ")]
        [InlineData("\t")]
        [InlineData("\n")]
        public void NotNullOrWhiteSpace_WithNullEmptyOrWhitespace_ThrowsValidationException(string? value)
        {
            // Act & Assert
            var ex = Assert.Throws<ValidationException>(() => Guard.NotNullOrWhiteSpace(value));
            Assert.Contains("cannot be null, empty, or whitespace", ex.Message);
        }

        [Theory]
        [InlineData("a")]
        [InlineData("valid string")]
        [InlineData(" a ")]
        public void NotNullOrWhiteSpace_WithValidValue_ReturnsValue(string value)
        {
            // Act
            var result = Guard.NotNullOrWhiteSpace(value);

            // Assert
            Assert.Equal(value, result);
        }

        #endregion

        #region NotNullOrEmpty Collection Tests

        [Fact]
        public void NotNullOrEmpty_Collection_WithNull_ThrowsValidationException()
        {
            // Arrange
            int[]? nullCollection = null;

            // Act & Assert
            var ex = Assert.Throws<ValidationException>(() => Guard.NotNullOrEmpty(nullCollection));
            Assert.Contains("cannot be null or empty", ex.Message);
        }

        [Fact]
        public void NotNullOrEmpty_Collection_WithEmpty_ThrowsValidationException()
        {
            // Arrange
            var emptyCollection = Array.Empty<int>();

            // Act & Assert
            var ex = Assert.Throws<ValidationException>(() => Guard.NotNullOrEmpty(emptyCollection));
            Assert.Contains("cannot be null or empty", ex.Message);
        }

        [Fact]
        public void NotNullOrEmpty_Collection_WithValidValue_ReturnsValue()
        {
            // Arrange
            var collection = new[] { 1, 2, 3 };

            // Act
            var result = Guard.NotNullOrEmpty(collection);

            // Assert
            Assert.Equal(collection, result);
        }

        #endregion

        #region GreaterThanOrEqualTo Tests

        [Theory]
        [InlineData(5, 10)]
        [InlineData(-1, 0)]
        [InlineData(0, 1)]
        public void GreaterThanOrEqualTo_WithValueLessThanMinimum_ThrowsValidationException(int value, int minimum)
        {
            // Act & Assert
            var ex = Assert.Throws<ValidationException>(() => Guard.GreaterThanOrEqualTo(value, minimum));
            Assert.Contains("must be greater than or equal to", ex.Message);
            Assert.Contains(minimum.ToString(), ex.Message);
        }

        [Theory]
        [InlineData(10, 10)]
        [InlineData(11, 10)]
        [InlineData(0, -1)]
        public void GreaterThanOrEqualTo_WithValidValue_ReturnsValue(int value, int minimum)
        {
            // Act
            var result = Guard.GreaterThanOrEqualTo(value, minimum);

            // Assert
            Assert.Equal(value, result);
        }

        #endregion

        #region GreaterThan Tests

        [Theory]
        [InlineData(5, 10)]
        [InlineData(10, 10)]
        [InlineData(-1, 0)]
        public void GreaterThan_WithValueNotGreaterThanMinimum_ThrowsValidationException(int value, int minimum)
        {
            // Act & Assert
            var ex = Assert.Throws<ValidationException>(() => Guard.GreaterThan(value, minimum));
            Assert.Contains("must be greater than", ex.Message);
        }

        [Theory]
        [InlineData(11, 10)]
        [InlineData(1, 0)]
        [InlineData(0, -1)]
        public void GreaterThan_WithValidValue_ReturnsValue(int value, int minimum)
        {
            // Act
            var result = Guard.GreaterThan(value, minimum);

            // Assert
            Assert.Equal(value, result);
        }

        #endregion

        #region InRange Tests

        [Theory]
        [InlineData(-1, 0, 10)]
        [InlineData(11, 0, 10)]
        [InlineData(100, 0, 10)]
        public void InRange_WithValueOutOfRange_ThrowsValidationException(int value, int minimum, int maximum)
        {
            // Act & Assert
            var ex = Assert.Throws<ValidationException>(() => Guard.InRange(value, minimum, maximum));
            Assert.Contains("must be between", ex.Message);
        }

        [Theory]
        [InlineData(0, 0, 10)]
        [InlineData(5, 0, 10)]
        [InlineData(10, 0, 10)]
        public void InRange_WithValidValue_ReturnsValue(int value, int minimum, int maximum)
        {
            // Act
            var result = Guard.InRange(value, minimum, maximum);

            // Assert
            Assert.Equal(value, result);
        }

        #endregion


    }
}
