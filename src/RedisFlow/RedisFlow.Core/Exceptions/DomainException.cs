using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace RedisFlow.Core.Exceptions;

/// <summary>
/// Base exception for domain errors. Provides guard methods for argument validation.
/// </summary>
public class DomainException : Exception
{
    public DomainException()
    {
    }

    public DomainException(string? message) : base(message)
    {
    }

    public DomainException(string? message, Exception? innerException) : base(message, innerException)
    {
    }

    /// <summary>
    /// Throws an exception if argument is null, empty, or consists only of white-space characters.
    /// </summary>
    /// <param name="argument">The string argument to validate.</param>
    /// <param name="paramName">The name of the parameter with which argument corresponds.</param>
    /// <exception cref="ArgumentNullException">argument is null.</exception>
    /// <exception cref="ArgumentException">argument is empty or consists only of white-space characters.</exception>
    public static void ThrowIfNullOrWhiteSpace(
        [NotNull] string? argument,
        [CallerArgumentExpression("argument")] string? paramName = null)
    {
        if (argument is null)
            throw new ArgumentNullException(paramName);
        if (string.IsNullOrWhiteSpace(argument))
            throw new ArgumentException("Argument cannot be empty or whitespace.", paramName);
    }
}