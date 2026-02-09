using Xunit;
using SmallMind;
using System;

namespace SmallMind.Tests
{
    /// <summary>
    /// Tests for SmallMindOptions validation.
    /// </summary>
    public class SmallMindOptionsTests
    {
        [Fact]
        public void Create_WithValidOptions_ThrowsUnsupportedFormat()
        {
            // Arrange
            var tempFile = System.IO.Path.GetTempFileName();
            try
            {
                var options = new SmallMindOptions
                {
                    ModelPath = tempFile,
                    MaxContextTokens = 2048,
                    EnableKvCache = true
                };

                // Act & Assert - temp files have .tmp extension which is unsupported
                Assert.Throws<UnsupportedModelFormatException>(() => SmallMindFactory.Create(options));
            }
            finally
            {
                if (System.IO.File.Exists(tempFile))
                {
                    System.IO.File.Delete(tempFile);
                }
            }
        }

        [Fact]
        public void Create_WithNullModelPath_ThrowsInvalidOptionsException()
        {
            // Arrange
            var options = new SmallMindOptions
            {
                ModelPath = null!,
                MaxContextTokens = 2048
            };

            // Act & Assert
            var ex = Assert.Throws<InvalidOptionsException>(() => SmallMindFactory.Create(options));
            Assert.Equal(nameof(options.ModelPath), ex.OptionName);
        }

        [Fact]
        public void Create_WithEmptyModelPath_ThrowsInvalidOptionsException()
        {
            // Arrange
            var options = new SmallMindOptions
            {
                ModelPath = "",
                MaxContextTokens = 2048
            };

            // Act & Assert
            var ex = Assert.Throws<InvalidOptionsException>(() => SmallMindFactory.Create(options));
            Assert.Equal(nameof(options.ModelPath), ex.OptionName);
        }

        [Fact]
        public void Create_WithNonExistentModelPath_ThrowsInvalidOptionsException()
        {
            // Arrange
            var options = new SmallMindOptions
            {
                ModelPath = "nonexistent_model_file_12345.smq",
                MaxContextTokens = 2048
            };

            // Act & Assert
            var ex = Assert.Throws<InvalidOptionsException>(() => SmallMindFactory.Create(options));
            Assert.Equal(nameof(options.ModelPath), ex.OptionName);
            Assert.Contains("not found", ex.Message);
        }

        [Fact]
        public void Create_WithInvalidMaxContextTokens_ThrowsInvalidOptionsException()
        {
            // Arrange
            var tempFile = System.IO.Path.GetTempFileName();
            try
            {
                var options = new SmallMindOptions
                {
                    ModelPath = tempFile,
                    MaxContextTokens = 0 // Invalid!
                };

                // Act & Assert
                var ex = Assert.Throws<InvalidOptionsException>(() => SmallMindFactory.Create(options));
                Assert.Equal(nameof(options.MaxContextTokens), ex.OptionName);
            }
            finally
            {
                if (System.IO.File.Exists(tempFile))
                {
                    System.IO.File.Delete(tempFile);
                }
            }
        }

        [Fact]
        public void Create_WithNegativeThreadCount_ThrowsInvalidOptionsException()
        {
            // Arrange
            var tempFile = System.IO.Path.GetTempFileName();
            try
            {
                var options = new SmallMindOptions
                {
                    ModelPath = tempFile,
                    MaxContextTokens = 2048,
                    ThreadCount = -1 // Invalid!
                };

                // Act & Assert
                var ex = Assert.Throws<InvalidOptionsException>(() => SmallMindFactory.Create(options));
                Assert.Equal(nameof(options.ThreadCount), ex.OptionName);
            }
            finally
            {
                if (System.IO.File.Exists(tempFile))
                {
                    System.IO.File.Delete(tempFile);
                }
            }
        }
    }

    /// <summary>
    /// Tests for TextGenerationOptions validation.
    /// </summary>
    public class TextGenerationOptionsTests
    {
        [Fact]
        public void TextGenerationOptions_DefaultValues_AreValid()
        {
            // Arrange & Act
            var options = new TextGenerationOptions();

            // Assert
            Assert.InRange(options.Temperature, 0.0f, 2.0f);
            Assert.InRange(options.TopP, 0.0f, 1.0f);
            Assert.True(options.TopK >= 0);
            Assert.True(options.MaxOutputTokens > 0);
        }
    }

    /// <summary>
    /// Tests for exception hierarchy.
    /// </summary>
    public class ExceptionTests
    {
        [Fact]
        public void SmallMindException_HasCorrectErrorCode()
        {
            // Arrange & Act
            var ex = new InvalidOptionsException("test", "test message");

            // Assert
            Assert.Equal(SmallMindErrorCode.InvalidOptions, ex.Code);
            Assert.IsAssignableFrom<SmallMindException>(ex);
        }

        [Fact]
        public void ModelLoadFailedException_PreservesInnerException()
        {
            // Arrange
            var inner = new Exception("Inner exception");

            // Act
            var ex = new ModelLoadFailedException("model.smq", "Failed", inner);

            // Assert
            Assert.Equal(SmallMindErrorCode.ModelLoadFailed, ex.Code);
            Assert.Same(inner, ex.InnerException);
            Assert.Equal("model.smq", ex.ModelPath);
        }

        [Fact]
        public void ContextOverflowException_StoresLengths()
        {
            // Arrange & Act
            var ex = new ContextOverflowException(5000, 4096);

            // Assert
            Assert.Equal(SmallMindErrorCode.ContextOverflow, ex.Code);
            Assert.Equal(5000, ex.RequestedLength);
            Assert.Equal(4096, ex.MaxLength);
        }
    }

    /// <summary>
    /// Tests for Usage and Timing structs.
    /// </summary>
    public class DTOTests
    {
        [Fact]
        public void Usage_TotalTokens_IsCorrect()
        {
            // Arrange & Act
            var usage = new Usage
            {
                PromptTokens = 10,
                CompletionTokens = 20
            };

            // Assert
            Assert.Equal(30, usage.TotalTokens);
        }

        [Fact]
        public void GenerationTimings_TokensPerSecond_IsCorrect()
        {
            // Arrange & Act
            var timings = new GenerationTimings(10.0, 1000.0, 50);

            // Assert
            Assert.Equal(10.0, timings.TimeToFirstTokenMs);
            Assert.Equal(1000.0, timings.TotalMs);
            Assert.Equal(50.0, timings.TokensPerSecond); // 50 tokens / 1 second
        }

        [Fact]
        public void GenerationTimings_ZeroTokens_ReturnsZeroRate()
        {
            // Arrange & Act
            var timings = new GenerationTimings(0, 1000.0, 0);

            // Assert
            Assert.Equal(0.0, timings.TokensPerSecond);
        }
    }
}
