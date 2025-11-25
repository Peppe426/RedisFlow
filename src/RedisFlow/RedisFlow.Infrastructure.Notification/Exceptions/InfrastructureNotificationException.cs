namespace RedisFlow.Infrastructure.Notification.Exceptions;

internal class InfrastructureNotificationException : Exception
{
    public InfrastructureNotificationException()
    {
    }

    public InfrastructureNotificationException(string? message) : base(message)
    {
    }

    public InfrastructureNotificationException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}