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
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">The configuration instance</param>
    /// <param name="streamKey">Optional stream key override (default: "messages:stream")</param>
    public static IServiceCollection AddRedisProducer(
        this IServiceCollection services,
        IConfiguration configuration,
        string streamKey = "messages:stream")
    {
        // Register Redis connection
        var redisConnectionString = configuration["ConnectionStrings:redis"] 
            ?? "localhost:6379";

        services.AddSingleton<IConnectionMultiplexer>(sp => 
            ConnectionMultiplexer.Connect(redisConnectionString));

        // Register Producer with optional stream key
        services.AddSingleton<IProducer>(sp => 
            new RedisProducer(
                sp.GetRequiredService<IConnectionMultiplexer>(),
                sp.GetRequiredService<ILogger<RedisProducer>>(),
                streamKey));

        return services;
    }
}
