using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RedisFlow.Services.Contracts;
using RedisFlow.Services.Implementations;
using StackExchange.Redis;

namespace RedisFlow.Services.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Redis producer services to the service collection.
    /// 
    /// Connection string resolution order (highest to lowest priority):
    /// 1. Environment variable: REDIS_CONNECTION_STRING
    /// 2. Configuration key: ConnectionStrings:redis (from Aspire or appsettings.json)
    /// 3. Default fallback: localhost:6379
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">The configuration instance (from HostBuilder or Aspire)</param>
    /// <param name="streamKey">Optional stream key override (default: "messages:stream")</param>
    /// <returns>The service collection for chaining</returns>
    /// <example>
    /// // In Producer Program.cs:
    /// var builder = Host.CreateApplicationBuilder(args);
    /// builder.Services.AddRedisProducer(builder.Configuration);
    /// 
    /// // Connection resolved from:
    /// // - Environment (if REDIS_CONNECTION_STRING is set)
    /// // - Aspire service discovery (if running via AppHost)
    /// // - appsettings.json (if using standalone)
    /// // - localhost:6379 (default fallback)
    /// </example>
    public static IServiceCollection AddRedisProducer(
        this IServiceCollection services,
        IConfiguration configuration,
        string streamKey = RedisConnectionConfiguration.DefaultStreamKey)
    {
        // Resolve connection string from multiple sources
        var redisConnectionString = RedisConnectionConfiguration.ResolveConnectionString(configuration);

        // Log the resolved connection (without exposing sensitive auth tokens if present)
        var logEndpoint = ExtractEndpointForLogging(redisConnectionString);

        // Register Redis connection as singleton (thread-safe, reused across all services)
        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<IConnectionMultiplexer>>();
            try
            {
                logger.LogInformation("Connecting to Redis at {Endpoint} for producer using stream key '{StreamKey}'",
                    logEndpoint, streamKey);
                var connection = ConnectionMultiplexer.Connect(redisConnectionString);
                logger.LogInformation("Successfully connected to Redis at {Endpoint}", logEndpoint);
                return connection;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to connect to Redis at {Endpoint}. " +
                    "Ensure Redis is running and accessible at the configured connection string.", logEndpoint);
                throw;
            }
        });

        // Register Producer service
        services.AddSingleton<IProducer>(sp =>
            new RedisProducer(
                sp.GetRequiredService<IConnectionMultiplexer>(),
                sp.GetRequiredService<ILogger<RedisProducer>>(),
                streamKey));

        return services;
    }

    /// <summary>
    /// Adds Redis consumer services to the service collection.
    /// 
    /// Connection string resolution order (highest to lowest priority):
    /// 1. Environment variable: REDIS_CONNECTION_STRING
    /// 2. Configuration key: ConnectionStrings:redis (from Aspire or appsettings.json)
    /// 3. Default fallback: localhost:6379
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">The configuration instance (from HostBuilder or Aspire)</param>
    /// <param name="streamKey">Redis stream key to consume from (default: "messages:stream")</param>
    /// <param name="groupName">Consumer group name (default: "default-group")</param>
    /// <param name="consumerName">Consumer identifier within the group (optional, auto-generated if null)</param>
    /// <returns>The service collection for chaining</returns>
    /// <example>
    /// // In Consumer Program.cs:
    /// var builder = Host.CreateApplicationBuilder(args);
    /// builder.Services.AddRedisConsumer(
    ///     builder.Configuration,
    ///     streamKey: "messages:stream",
    ///     groupName: "consumer-group-1",
    ///     consumerName: "consumer-1");
    /// </example>
    public static IServiceCollection AddRedisConsumer(
        this IServiceCollection services,
        IConfiguration configuration,
        string streamKey = RedisConnectionConfiguration.DefaultStreamKey,
        string groupName = RedisConnectionConfiguration.DefaultConsumerGroup,
        string? consumerName = null)
    {
        // Resolve connection string from multiple sources
        var redisConnectionString = RedisConnectionConfiguration.ResolveConnectionString(configuration);

        // Log the resolved connection
        var logEndpoint = ExtractEndpointForLogging(redisConnectionString);

        // Register Redis connection as singleton
        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<IConnectionMultiplexer>>();
            try
            {
                logger.LogInformation(
                    "Connecting to Redis at {Endpoint} for consumer using stream '{StreamKey}' in group '{GroupName}'",
                    logEndpoint, streamKey, groupName);
                var connection = ConnectionMultiplexer.Connect(redisConnectionString);
                logger.LogInformation("Successfully connected to Redis at {Endpoint}", logEndpoint);
                return connection;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to connect to Redis at {Endpoint}. " +
                    "Ensure Redis is running and accessible at the configured connection string.", logEndpoint);
                throw;
            }
        });

        // Note: Consumer registration would go here if you have a consumer service interface
        // For now, this is a placeholder for future consumer service registration
        // services.AddSingleton<IConsumer>(sp => ...);

        return services;
    }

    /// <summary>
    /// Extracts endpoint information for logging, masking any sensitive authentication details.
    /// This prevents connection strings with passwords from appearing in logs.
    /// </summary>
    /// <param name="connectionString">The full Redis connection string</param>
    /// <returns>A loggable representation of the endpoint</returns>
    private static string ExtractEndpointForLogging(string connectionString)
    {
        try
        {
            // If there's an @ symbol, it likely contains auth - log only the host:port part
            if (connectionString.Contains("@"))
            {
                var parts = connectionString.Split('@');
                return parts.Length > 1 ? parts[^1] : connectionString;
            }
            return connectionString;
        }
        catch
        {
            return "<redis>";
        }
    }
}
