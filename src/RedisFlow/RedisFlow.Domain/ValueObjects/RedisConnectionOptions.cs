namespace RedisFlow.Domain.ValueObjects;

public sealed record RedisConnectionOptions
{
    public bool AbortOnConnectFail { get; init; } = false;
    public int ConnectTimeout { get; init; } = 5000;
    public int SyncTimeout { get; init; } = 10000;
    public int AsyncTimeout { get; init; } = 10000;
    public int ReconnectRetryInitial { get; init; } = 500;
    public int ReconnectRetryMax { get; init; } = 10000;
    public bool? Ssl { get; init; } = null; // If null, use default logic
    public string? ClientName { get; init; } = null;
}