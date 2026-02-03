using System;

namespace SmallMind.Public
{
    /// <summary>
    /// Base exception for all SmallMind public API errors.
    /// Provides structured error information for programmatic handling.
    /// </summary>
    public abstract class SmallMindException : Exception
    {
        /// <summary>
        /// Gets the error code for this exception.
        /// </summary>
        public SmallMindErrorCode Code { get; }

        /// <summary>
        /// Creates a new SmallMindException.
        /// </summary>
        /// <param name="code">Error code.</param>
        /// <param name="message">Error message.</param>
        protected SmallMindException(SmallMindErrorCode code, string message)
            : base(message)
        {
            Code = code;
        }

        /// <summary>
        /// Creates a new SmallMindException with an inner exception.
        /// </summary>
        /// <param name="code">Error code.</param>
        /// <param name="message">Error message.</param>
        /// <param name="innerException">Inner exception.</param>
        protected SmallMindException(SmallMindErrorCode code, string message, Exception innerException)
            : base(message, innerException)
        {
            Code = code;
        }
    }

    /// <summary>
    /// Error codes for SmallMind exceptions.
    /// </summary>
    public enum SmallMindErrorCode
    {
        /// <summary>
        /// Invalid options were provided.
        /// </summary>
        InvalidOptions,

        /// <summary>
        /// Model failed to load.
        /// </summary>
        ModelLoadFailed,

        /// <summary>
        /// Unsupported model format.
        /// </summary>
        UnsupportedModelFormat,

        /// <summary>
        /// Tokenizer mismatch or initialization failure.
        /// </summary>
        TokenizerMismatch,

        /// <summary>
        /// Context overflow (prompt + output exceeds max context).
        /// </summary>
        ContextOverflow,

        /// <summary>
        /// Request was cancelled.
        /// </summary>
        RequestCancelled,

        /// <summary>
        /// Inference operation failed.
        /// </summary>
        InferenceFailed,

        /// <summary>
        /// Embedding generation failed.
        /// </summary>
        EmbeddingFailed,

        /// <summary>
        /// Internal error (should be rare).
        /// </summary>
        InternalError
    }

    /// <summary>
    /// Thrown when invalid options are provided to the engine or a session.
    /// </summary>
    public sealed class InvalidOptionsException : SmallMindException
    {
        /// <summary>
        /// Gets the name of the invalid option.
        /// </summary>
        public string OptionName { get; }

        /// <summary>
        /// Creates a new InvalidOptionsException.
        /// </summary>
        /// <param name="optionName">Name of the invalid option.</param>
        /// <param name="message">Detailed error message.</param>
        public InvalidOptionsException(string optionName, string message)
            : base(SmallMindErrorCode.InvalidOptions, $"Invalid option '{optionName}': {message}")
        {
            OptionName = optionName;
        }
    }

    /// <summary>
    /// Thrown when a model fails to load.
    /// </summary>
    public sealed class ModelLoadFailedException : SmallMindException
    {
        /// <summary>
        /// Gets the model path that failed to load.
        /// </summary>
        public string ModelPath { get; }

        /// <summary>
        /// Creates a new ModelLoadFailedException.
        /// </summary>
        /// <param name="modelPath">Path to the model that failed.</param>
        /// <param name="message">Detailed error message.</param>
        public ModelLoadFailedException(string modelPath, string message)
            : base(SmallMindErrorCode.ModelLoadFailed, $"Failed to load model from '{modelPath}': {message}")
        {
            ModelPath = modelPath;
        }

        /// <summary>
        /// Creates a new ModelLoadFailedException with an inner exception.
        /// </summary>
        /// <param name="modelPath">Path to the model that failed.</param>
        /// <param name="message">Detailed error message.</param>
        /// <param name="innerException">Inner exception.</param>
        public ModelLoadFailedException(string modelPath, string message, Exception innerException)
            : base(SmallMindErrorCode.ModelLoadFailed, $"Failed to load model from '{modelPath}': {message}", innerException)
        {
            ModelPath = modelPath;
        }
    }

    /// <summary>
    /// Thrown when an unsupported model format is encountered.
    /// </summary>
    public sealed class UnsupportedModelFormatException : SmallMindException
    {
        /// <summary>
        /// Gets the file extension that was unsupported.
        /// </summary>
        public string Extension { get; }

        /// <summary>
        /// Creates a new UnsupportedModelFormatException.
        /// </summary>
        /// <param name="extension">The unsupported file extension.</param>
        /// <param name="message">Detailed error message.</param>
        public UnsupportedModelFormatException(string extension, string message)
            : base(SmallMindErrorCode.UnsupportedModelFormat, $"Unsupported model format '{extension}': {message}")
        {
            Extension = extension;
        }
    }

    /// <summary>
    /// Thrown when a tokenizer error occurs.
    /// </summary>
    public sealed class TokenizerException : SmallMindException
    {
        /// <summary>
        /// Creates a new TokenizerException.
        /// </summary>
        /// <param name="message">Detailed error message.</param>
        public TokenizerException(string message)
            : base(SmallMindErrorCode.TokenizerMismatch, message)
        {
        }

        /// <summary>
        /// Creates a new TokenizerException with an inner exception.
        /// </summary>
        /// <param name="message">Detailed error message.</param>
        /// <param name="innerException">Inner exception.</param>
        public TokenizerException(string message, Exception innerException)
            : base(SmallMindErrorCode.TokenizerMismatch, message, innerException)
        {
        }
    }

    /// <summary>
    /// Thrown when the context length is exceeded.
    /// </summary>
    public sealed class ContextOverflowException : SmallMindException
    {
        /// <summary>
        /// Gets the requested context length.
        /// </summary>
        public int RequestedLength { get; }

        /// <summary>
        /// Gets the maximum allowed context length.
        /// </summary>
        public int MaxLength { get; }

        /// <summary>
        /// Creates a new ContextOverflowException.
        /// </summary>
        /// <param name="requestedLength">The requested context length.</param>
        /// <param name="maxLength">The maximum allowed length.</param>
        public ContextOverflowException(int requestedLength, int maxLength)
            : base(SmallMindErrorCode.ContextOverflow,
                   $"Context overflow: requested {requestedLength} tokens, but maximum is {maxLength}. " +
                   $"Reduce input size or increase MaxContextTokens.")
        {
            RequestedLength = requestedLength;
            MaxLength = maxLength;
        }
    }

    /// <summary>
    /// Thrown when a request is cancelled.
    /// </summary>
    public sealed class RequestCancelledException : SmallMindException
    {
        /// <summary>
        /// Creates a new RequestCancelledException.
        /// </summary>
        /// <param name="message">Cancellation reason.</param>
        public RequestCancelledException(string message)
            : base(SmallMindErrorCode.RequestCancelled, $"Request cancelled: {message}")
        {
        }
    }

    /// <summary>
    /// Thrown when an inference operation fails.
    /// </summary>
    public sealed class InferenceFailedException : SmallMindException
    {
        /// <summary>
        /// Creates a new InferenceFailedException.
        /// </summary>
        /// <param name="message">Detailed error message.</param>
        public InferenceFailedException(string message)
            : base(SmallMindErrorCode.InferenceFailed, message)
        {
        }

        /// <summary>
        /// Creates a new InferenceFailedException with an inner exception.
        /// </summary>
        /// <param name="message">Detailed error message.</param>
        /// <param name="innerException">Inner exception.</param>
        public InferenceFailedException(string message, Exception innerException)
            : base(SmallMindErrorCode.InferenceFailed, message, innerException)
        {
        }
    }

    /// <summary>
    /// Thrown when an embedding operation fails.
    /// </summary>
    public sealed class EmbeddingFailedException : SmallMindException
    {
        /// <summary>
        /// Creates a new EmbeddingFailedException.
        /// </summary>
        /// <param name="message">Detailed error message.</param>
        public EmbeddingFailedException(string message)
            : base(SmallMindErrorCode.EmbeddingFailed, message)
        {
        }

        /// <summary>
        /// Creates a new EmbeddingFailedException with an inner exception.
        /// </summary>
        /// <param name="message">Detailed error message.</param>
        /// <param name="innerException">Inner exception.</param>
        public EmbeddingFailedException(string message, Exception innerException)
            : base(SmallMindErrorCode.EmbeddingFailed, message, innerException)
        {
        }
    }

    /// <summary>
    /// Thrown when an internal error occurs that should not normally happen.
    /// </summary>
    public sealed class InternalErrorException : SmallMindException
    {
        /// <summary>
        /// Creates a new InternalErrorException.
        /// </summary>
        /// <param name="message">Detailed error message.</param>
        public InternalErrorException(string message)
            : base(SmallMindErrorCode.InternalError, $"Internal error: {message}")
        {
        }

        /// <summary>
        /// Creates a new InternalErrorException with an inner exception.
        /// </summary>
        /// <param name="message">Detailed error message.</param>
        /// <param name="innerException">Inner exception.</param>
        public InternalErrorException(string message, Exception innerException)
            : base(SmallMindErrorCode.InternalError, $"Internal error: {message}", innerException)
        {
        }
    }
}
