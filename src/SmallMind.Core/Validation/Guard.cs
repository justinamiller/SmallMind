using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using SmallMind.Core.Exceptions;

namespace SmallMind.Core.Validation
{
    /// <summary>
    /// Provides guard clauses for input validation across SmallMind.
    /// </summary>
    internal static class Guard
    {
        /// <summary>
        /// Throws <see cref="ValidationException"/> if the value is null.
        /// </summary>
        /// <typeparam name="T">The type of the value.</typeparam>
        /// <param name="value">The value to check.</param>
        /// <param name="parameterName">The name of the parameter being validated.</param>
        /// <returns>The non-null value.</returns>
        /// <exception cref="ValidationException">Thrown when value is null.</exception>
        public static T NotNull<T>([NotNull] T? value, [CallerArgumentExpression(nameof(value))] string? parameterName = null)
            where T : class
        {
            if (value is null)
            {
                throw new ValidationException($"Parameter '{parameterName}' cannot be null.", parameterName);
            }
            return value;
        }

        /// <summary>
        /// Throws <see cref="ValidationException"/> if the string is null or empty.
        /// </summary>
        /// <param name="value">The string to check.</param>
        /// <param name="parameterName">The name of the parameter being validated.</param>
        /// <returns>The non-null, non-empty string.</returns>
        /// <exception cref="ValidationException">Thrown when value is null or empty.</exception>
        public static string NotNullOrEmpty([NotNull] string? value, [CallerArgumentExpression(nameof(value))] string? parameterName = null)
        {
            if (string.IsNullOrEmpty(value))
            {
                throw new ValidationException($"Parameter '{parameterName}' cannot be null or empty.", parameterName);
            }
            return value;
        }

        /// <summary>
        /// Throws <see cref="ValidationException"/> if the string is null, empty, or whitespace.
        /// </summary>
        /// <param name="value">The string to check.</param>
        /// <param name="parameterName">The name of the parameter being validated.</param>
        /// <returns>The non-null, non-empty, non-whitespace string.</returns>
        /// <exception cref="ValidationException">Thrown when value is null, empty, or whitespace.</exception>
        public static string NotNullOrWhiteSpace([NotNull] string? value, [CallerArgumentExpression(nameof(value))] string? parameterName = null)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ValidationException($"Parameter '{parameterName}' cannot be null, empty, or whitespace.", parameterName);
            }
            return value;
        }

        /// <summary>
        /// Throws <see cref="ValidationException"/> if the collection is null or empty.
        /// </summary>
        /// <typeparam name="T">The type of elements in the collection.</typeparam>
        /// <param name="value">The collection to check.</param>
        /// <param name="parameterName">The name of the parameter being validated.</param>
        /// <returns>The non-null, non-empty collection.</returns>
        /// <exception cref="ValidationException">Thrown when value is null or empty.</exception>
        public static IEnumerable<T> NotNullOrEmpty<T>([NotNull] IEnumerable<T>? value, [CallerArgumentExpression(nameof(value))] string? parameterName = null)
        {
            if (value is null || !value.Any())
            {
                throw new ValidationException($"Parameter '{parameterName}' cannot be null or empty.", parameterName);
            }
            return value;
        }

        /// <summary>
        /// Throws <see cref="ValidationException"/> if the value is less than the minimum.
        /// </summary>
        /// <typeparam name="T">The type of the value (must be comparable).</typeparam>
        /// <param name="value">The value to check.</param>
        /// <param name="minimum">The minimum allowed value (inclusive).</param>
        /// <param name="parameterName">The name of the parameter being validated.</param>
        /// <returns>The validated value.</returns>
        /// <exception cref="ValidationException">Thrown when value is less than minimum.</exception>
        public static T GreaterThanOrEqualTo<T>(T value, T minimum, [CallerArgumentExpression(nameof(value))] string? parameterName = null)
            where T : IComparable<T>
        {
            if (value.CompareTo(minimum) < 0)
            {
                throw new ValidationException($"Parameter '{parameterName}' must be greater than or equal to {minimum}, but was {value}.", parameterName);
            }
            return value;
        }

        /// <summary>
        /// Throws <see cref="ValidationException"/> if the value is less than or equal to the minimum.
        /// </summary>
        /// <typeparam name="T">The type of the value (must be comparable).</typeparam>
        /// <param name="value">The value to check.</param>
        /// <param name="minimum">The minimum allowed value (exclusive).</param>
        /// <param name="parameterName">The name of the parameter being validated.</param>
        /// <returns>The validated value.</returns>
        /// <exception cref="ValidationException">Thrown when value is less than or equal to minimum.</exception>
        public static T GreaterThan<T>(T value, T minimum, [CallerArgumentExpression(nameof(value))] string? parameterName = null)
            where T : IComparable<T>
        {
            if (value.CompareTo(minimum) <= 0)
            {
                throw new ValidationException($"Parameter '{parameterName}' must be greater than {minimum}, but was {value}.", parameterName);
            }
            return value;
        }

        /// <summary>
        /// Throws <see cref="ValidationException"/> if the value is outside the specified range.
        /// </summary>
        /// <typeparam name="T">The type of the value (must be comparable).</typeparam>
        /// <param name="value">The value to check.</param>
        /// <param name="minimum">The minimum allowed value (inclusive).</param>
        /// <param name="maximum">The maximum allowed value (inclusive).</param>
        /// <param name="parameterName">The name of the parameter being validated.</param>
        /// <returns>The validated value.</returns>
        /// <exception cref="ValidationException">Thrown when value is outside the range.</exception>
        public static T InRange<T>(T value, T minimum, T maximum, [CallerArgumentExpression(nameof(value))] string? parameterName = null)
            where T : IComparable<T>
        {
            if (value.CompareTo(minimum) < 0 || value.CompareTo(maximum) > 0)
            {
                throw new ValidationException($"Parameter '{parameterName}' must be between {minimum} and {maximum} (inclusive), but was {value}.", parameterName);
            }
            return value;
        }

        /// <summary>
        /// Throws <see cref="ValidationException"/> if the array is null or has a different length than expected.
        /// </summary>
        /// <typeparam name="T">The type of elements in the array.</typeparam>
        /// <param name="value">The array to check.</param>
        /// <param name="expectedLength">The expected length.</param>
        /// <param name="parameterName">The name of the parameter being validated.</param>
        /// <returns>The validated array.</returns>
        /// <exception cref="ValidationException">Thrown when array is null or has incorrect length.</exception>
        public static T[] HasLength<T>([NotNull] T[]? value, int expectedLength, [CallerArgumentExpression(nameof(value))] string? parameterName = null)
        {
            NotNull(value, parameterName);
            if (value.Length != expectedLength)
            {
                throw new ValidationException($"Parameter '{parameterName}' must have length {expectedLength}, but has length {value.Length}.", parameterName);
            }
            return value;
        }

        /// <summary>
        /// Throws <see cref="ShapeMismatchException"/> if tensor shapes are incompatible.
        /// </summary>
        /// <param name="actualShape">The actual tensor shape.</param>
        /// <param name="expectedShape">The expected tensor shape.</param>
        /// <param name="operation">The operation being performed.</param>
        /// <exception cref="ShapeMismatchException">Thrown when shapes don't match.</exception>
        public static void ShapesMatch(int[] actualShape, int[] expectedShape, string operation)
        {
            NotNull(actualShape, nameof(actualShape));
            NotNull(expectedShape, nameof(expectedShape));

            if (actualShape.Length != expectedShape.Length)
            {
                throw ShapeMismatchException.Create(operation, expectedShape, actualShape);
            }

            for (int i = 0; i < actualShape.Length; i++)
            {
                if (actualShape[i] != expectedShape[i])
                {
                    throw ShapeMismatchException.Create(operation, expectedShape, actualShape);
                }
            }
        }

        /// <summary>
        /// Throws <see cref="ValidationException"/> if the file path does not exist.
        /// </summary>
        /// <param name="filePath">The file path to check.</param>
        /// <param name="parameterName">The name of the parameter being validated.</param>
        /// <returns>The validated file path.</returns>
        /// <exception cref="ValidationException">Thrown when file does not exist.</exception>
        public static string FileExists([NotNull] string? filePath, [CallerArgumentExpression(nameof(filePath))] string? parameterName = null)
        {
            NotNullOrWhiteSpace(filePath, parameterName);
            if (!File.Exists(filePath))
            {
                throw new ValidationException($"File '{filePath}' does not exist.", parameterName);
            }
            return filePath;
        }

        /// <summary>
        /// Throws <see cref="ValidationException"/> if the directory path does not exist.
        /// </summary>
        /// <param name="directoryPath">The directory path to check.</param>
        /// <param name="parameterName">The name of the parameter being validated.</param>
        /// <returns>The validated directory path.</returns>
        /// <exception cref="ValidationException">Thrown when directory does not exist.</exception>
        public static string DirectoryExists([NotNull] string? directoryPath, [CallerArgumentExpression(nameof(directoryPath))] string? parameterName = null)
        {
            NotNullOrWhiteSpace(directoryPath, parameterName);
            if (!Directory.Exists(directoryPath))
            {
                throw new ValidationException($"Directory '{directoryPath}' does not exist.", parameterName);
            }
            return directoryPath;
        }

        /// <summary>
        /// Throws <see cref="SmallMindObjectDisposedException"/> if the object has been disposed.
        /// </summary>
        /// <param name="isDisposed">True if the object has been disposed; otherwise, false.</param>
        /// <param name="objectName">The name of the object.</param>
        /// <exception cref="SmallMindObjectDisposedException">Thrown when the object has been disposed.</exception>
        public static void NotDisposed(bool isDisposed, string objectName)
        {
            if (isDisposed)
            {
                throw new SmallMindObjectDisposedException(objectName);
            }
        }
    }
}
