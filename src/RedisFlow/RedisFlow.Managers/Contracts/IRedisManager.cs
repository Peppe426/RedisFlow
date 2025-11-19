namespace RedisFlow.Managers.Contracts;

public interface IRedisManager
{
    Task<bool> PingAsync();

    Task SetConfigAsync(string key, string value);

    Task<Dictionary<string, string>> GetConfigAsync(string pattern = "*");

    Task<bool> KeyExistsAsync(string key);

    Task SetValueAsync(string key, string value);

    Task<string?> GetValueAsync(string key);

    Task<bool> RemoveKeyAsync(string key);
}