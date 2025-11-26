using RedisFlow.Core.Exceptions;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace RedisFlow.Domain.Exceptions;

internal class StreamHandlerException : DomainException
{
    public StreamHandlerException()
    {
    }

    public StreamHandlerException(string? message) : base(message)
    {
    }

    public StreamHandlerException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}