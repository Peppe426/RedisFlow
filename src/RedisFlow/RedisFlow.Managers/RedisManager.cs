using RedisFlow.Managers.Contracts;
using StackExchange.Redis;

namespace RedisFlow.Managers;

public sealed class RedisService : IRedisManager, IDisposable
{
    private readonly Lazy<ConnectionMultiplexer> _connection;
    private readonly string _connectionString;

    public RedisService(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _connection = new Lazy<ConnectionMultiplexer>(() => ConnectionMultiplexer.Connect(BuildOptions(connectionString)));
    }

    private static ConfigurationOptions BuildOptions(string endpoint) => new()
    {
        EndPoints = { endpoint },
        AbortOnConnectFail = false,
        ConnectRetry = 3,
        ConnectTimeout = 5000
    };

    private IServer GetServer() =>
        _connection.Value.GetEndPoints().Select(ep => _connection.Value.GetServer(ep)).First();

    private IDatabase GetDb() => _connection.Value.GetDatabase();

    public async Task<bool> PingAsync()
    {
        try
        {
            var result = await GetDb().PingAsync();
            return result.TotalMilliseconds >= 0;
        }
        catch
        {
            return false;
        }
    }

    public async Task SetConfigAsync(string key, string value)
    {
        var server = GetServer();
        await server.ConfigSetAsync(key, value);
    }

    public async Task<Dictionary<string, string>> GetConfigAsync(string pattern = "*")
    {
        var server = GetServer();
        var configs = await server.ConfigGetAsync(pattern);
        return configs.ToDictionary(kv => kv.Key, kv => kv.Value);
    }

    public async Task<bool> KeyExistsAsync(string key)
        => await GetDb().KeyExistsAsync(key);

    public async Task SetValueAsync(string key, string value)
        => await GetDb().StringSetAsync(key, value);

    public async Task<string?> GetValueAsync(string key)
        => await GetDb().StringGetAsync(key);

    public async Task<bool> RemoveKeyAsync(string key)
        => await GetDb().KeyDeleteAsync(key);

    public void Dispose()
    {
        if (_connection.IsValueCreated)
            _connection.Value.Dispose();
    }
}