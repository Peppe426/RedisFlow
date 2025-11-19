using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RedisFlow.Domain.ValueObjects;
using RedisFlow.Services.Contracts;
using RedisFlow.Services.Implementations;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.AddRedisClient("redis");

builder.Services.AddSingleton<IConsumer>(sp =>
{
    var redis = sp.GetRequiredService<StackExchange.Redis.IConnectionMultiplexer>();
    var logger = sp.GetRequiredService<ILogger<RedisStreamConsumer>>();
    return new RedisStreamConsumer(
        redis, 
        logger,
        consumerGroup: "default-group",
        consumerName: Environment.MachineName);
});

builder.Services.AddHostedService<ConsumerWorker>();

var host = builder.Build();
await host.RunAsync();

// Background service that consumes messages
class ConsumerWorker : BackgroundService
{
    private readonly IConsumer _consumer;
    private readonly ILogger<ConsumerWorker> _logger;

    public ConsumerWorker(IConsumer consumer, ILogger<ConsumerWorker> logger)
    {
        _consumer = consumer;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Consumer starting...");

        await _consumer.ConsumeAsync(HandleMessageAsync, stoppingToken);

        _logger.LogInformation("Consumer stopped");
    }

    private async Task HandleMessageAsync(Message message, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Received: [{Producer}] {Content}",
            message.Producer,
            message.Content);

        // Simulate processing time
        await Task.Delay(500, cancellationToken);
    }
}
