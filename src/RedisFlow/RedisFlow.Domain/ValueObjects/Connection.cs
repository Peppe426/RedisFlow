namespace RedisFlow.Domain.ValueObjects;

public class Connection
{
    public string Host
    {
        get;
    }

    public int Port
    {
        get;
    }

    public string? Password
    {
        get;
        private set;
    }

    public Connection(string host, int port, string? password = null)
    {
        Host = host;
        Port = port;
        Password = password;
    }
}