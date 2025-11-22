namespace RedisFlow.Infrastructure.IO.Exceptions;

internal class InfrastructureIOException : Exception
{
    public InfrastructureIOException()
    {
    }

    public InfrastructureIOException(string? message) : base(message)
    {
    }

    public InfrastructureIOException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}