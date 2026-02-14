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


        #region SafeFileName Tests

        [Theory]
        [InlineData("validfile.txt")]
        [InlineData("file123.bin")]
        [InlineData("my-file_name.smq")]
        public void SafeFileName_WithValidFileName_ReturnsValue(string fileName)
        {
            // Act
            var result = Guard.SafeFileName(fileName);

            // Assert
            Assert.Equal(fileName, result);
        }

        [Theory]
        [InlineData("path/to/file.txt")]
        [InlineData("path\\to\\file.txt")]
        [InlineData("../file.txt")]
        [InlineData("..\\file.txt")]
        [InlineData("/etc/passwd")]
        [InlineData("C:\\Windows\\System32\\config")]
        public void SafeFileName_WithPathSeparators_ThrowsValidationException(string fileName)
        {
            // Act & Assert
            var ex = Assert.Throws<ValidationException>(() => Guard.SafeFileName(fileName));
            Assert.Contains("cannot contain path separators", ex.Message);
        }

        [Theory]
        [InlineData(".")]
        [InlineData("..")]
        public void SafeFileName_WithRelativePathComponent_ThrowsValidationException(string fileName)
        {
            // Act & Assert
            var ex = Assert.Throws<ValidationException>(() => Guard.SafeFileName(fileName));
            Assert.Contains("cannot be a relative path component", ex.Message);
        }

        [Fact]
        public void SafeFileName_WithInvalidCharacters_ThrowsValidationException()
        {
            // Arrange - Get a character that is actually invalid on this platform
            var invalidChars = Path.GetInvalidFileNameChars();
            if (invalidChars.Length == 0)
            {
                // Skip test if no invalid characters on this platform
                return;
            }
            
            // Use the first invalid character that's not a path separator
            var invalidChar = invalidChars.FirstOrDefault(c => c != '/' && c != '\\' && c != Path.DirectorySeparatorChar && c != Path.AltDirectorySeparatorChar);
            if (invalidChar == '\0')
            {
                // Skip if no suitable invalid character found
                return;
            }
            
            var fileName = $"file{invalidChar}name.txt";

            // Act & Assert
            var ex = Assert.Throws<ValidationException>(() => Guard.SafeFileName(fileName));
            Assert.Contains("contains invalid file name characters", ex.Message);
        }

        [Fact]
        public void SafeFileName_WithNull_ThrowsValidationException()
        {
            // Act & Assert
            Assert.Throws<ValidationException>(() => Guard.SafeFileName(null!));
        }

        #endregion

        #region PathWithinDirectory Tests

        [Fact]
        public void PathWithinDirectory_WithValidRelativePath_ReturnsFullPath()
        {
            // Arrange
            var tempDir = Path.GetTempPath();
            var relativePath = "subfolder/file.txt";

            // Act
            var result = Guard.PathWithinDirectory(tempDir, relativePath);

            // Assert
            Assert.StartsWith(Path.GetFullPath(tempDir), result);
            Assert.Contains("subfolder", result);
        }

        [Fact]
        public void PathWithinDirectory_WithPathTraversal_ThrowsValidationException()
        {
            // Arrange
            var tempDir = Path.GetTempPath();
            var maliciousPath = "../../../etc/passwd";

            // Act & Assert
            var ex = Assert.Throws<ValidationException>(() => Guard.PathWithinDirectory(tempDir, maliciousPath));
            Assert.Contains("would result in a path outside the base directory", ex.Message);
        }

        [Fact]
        public void PathWithinDirectory_WithAbsolutePath_ThrowsValidationException()
        {
            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), "base");
            Directory.CreateDirectory(tempDir);
            
            try
            {
                // Use a different absolute path (root or temp's parent)
                var absolutePath = Path.GetPathRoot(tempDir) ?? "/";

                // Act & Assert
                var ex = Assert.Throws<ValidationException>(() => Guard.PathWithinDirectory(tempDir, absolutePath));
                Assert.Contains("would result in a path outside the base directory", ex.Message);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [Fact]
        public void PathWithinDirectory_WithMultipleTraversals_ThrowsValidationException()
        {
            // Arrange
            var baseDir = Path.Combine(Path.GetTempPath(), "base");
            Directory.CreateDirectory(baseDir);
            
            try
            {
                var maliciousPath = Path.Combine("..", "..", "..", "etc", "passwd");

                // Act & Assert
                var ex = Assert.Throws<ValidationException>(() => Guard.PathWithinDirectory(baseDir, maliciousPath));
                Assert.Contains("would result in a path outside the base directory", ex.Message);
            }
            finally
            {
                if (Directory.Exists(baseDir))
                {
                    Directory.Delete(baseDir, true);
                }
            }
        }

        [Fact]
        public void PathWithinDirectory_WithNullBasePath_ThrowsValidationException()
        {
            // Act & Assert
            Assert.Throws<ValidationException>(() => Guard.PathWithinDirectory(null!, "relative"));
        }

        [Fact]
        public void PathWithinDirectory_WithNullRelativePath_ThrowsValidationException()
        {
            // Arrange
            var tempDir = Path.GetTempPath();

            // Act & Assert
            Assert.Throws<ValidationException>(() => Guard.PathWithinDirectory(tempDir, null!));
        }

        #endregion

    }
}
