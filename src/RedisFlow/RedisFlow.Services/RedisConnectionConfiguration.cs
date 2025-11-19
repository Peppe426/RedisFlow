using Microsoft.Extensions.Configuration;

namespace RedisFlow.Services;

/// <summary>
/// Centralized configuration constants for Redis connections.
/// This ensures consistency across all services and makes it easy to update connection strategies.
/// </summary>
public static class RedisConnectionConfiguration
{
    /// <summary>
    /// The well-known configuration key for Redis connection strings in appsettings.json
    /// and Aspire's service discovery. This key must match what AppHost registers.
    /// 
    /// Usage in appsettings.json:
    /// {
    ///   "ConnectionStrings": {
    ///     "redis": "localhost:6379"
    ///   }
    /// }
    /// </summary>
    public const string ConnectionStringKey = "ConnectionStrings:redis";

    /// <summary>
    /// Default connection string used as fallback if no configuration is provided.
    /// This is suitable for local development on Windows/Linux hosts.
    /// </summary>
    public const string DefaultConnectionString = "localhost:6379";

    /// <summary>
    /// Default Redis stream key for messages.
    /// </summary>
    public const string DefaultStreamKey = "messages:stream";

    /// <summary>
    /// Default consumer group name.
    /// </summary>
    public const string DefaultConsumerGroup = "default-group";

    /// <summary>
    /// Environment variable name for override (optional, takes precedence over appsettings.json).
    /// Usage: set REDIS_CONNECTION_STRING=172.21.116.2:6379
    /// </summary>
    public const string EnvironmentVariableKey = "REDIS_CONNECTION_STRING";

    /// <summary>
    /// Resolves the Redis connection string from multiple sources in order of precedence:
    /// 1. Environment variable (REDIS_CONNECTION_STRING)
    /// 2. Configuration from appsettings.json or Aspire (ConnectionStrings:redis)
    /// 3. Default fallback (localhost:6379)
    /// </summary>
    /// <param name="configuration">IConfiguration instance from DI</param>
    /// <returns>The resolved connection string</returns>
    public static string ResolveConnectionString(IConfiguration configuration)
    {
        // Highest priority: Environment variable (useful for CI/CD and container deployments)
        var envConnectionString = Environment.GetEnvironmentVariable(EnvironmentVariableKey);
        if (!string.IsNullOrWhiteSpace(envConnectionString))
        {
            return envConnectionString;
        }

        // Medium priority: Configuration from Aspire or appsettings.json
        var configConnectionString = configuration[ConnectionStringKey];
        if (!string.IsNullOrWhiteSpace(configConnectionString))
        {
            return configConnectionString;
        }

        // Lowest priority: Default fallback
        return DefaultConnectionString;
    }
}
