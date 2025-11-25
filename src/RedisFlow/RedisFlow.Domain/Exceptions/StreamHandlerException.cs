namespace RedisFlow.Domain.Exceptions;

internal class StreamHandlerException : Exception
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