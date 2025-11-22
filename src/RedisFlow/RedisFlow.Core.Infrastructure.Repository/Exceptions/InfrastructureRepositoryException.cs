namespace RedisFlow.Infrastructure.IO.Exceptions;

internal class InfrastructureRepositoryException : Exception
{
    public InfrastructureRepositoryException()
    {
    }

    public InfrastructureRepositoryException(string? message) : base(message)
    {
    }

    public InfrastructureRepositoryException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}