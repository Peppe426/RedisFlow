using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RedisFlow.Domain.ValueObjects;
using RedisFlow.Services;
using RedisFlow.Services.Contracts;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.AddRedisClient("redis");

builder.Services.AddSingleton<IProducer>(sp =>
{
    var redis = sp.GetRequiredService<StackExchange.Redis.IConnectionMultiplexer>();
    var logger = sp.GetRequiredService<ILogger<RedisStreamProducer>>();
    return new RedisStreamProducer(redis, logger);
});

builder.Services.AddHostedService<ProducerWorker>();

var host = builder.Build();
await host.RunAsync();

// Background service that produces messages
class ProducerWorker : BackgroundService
{
    private readonly IProducer _producer;
    private readonly ILogger<ProducerWorker> _logger;
    private readonly string _producerName = "Producer2";

    public ProducerWorker(IProducer producer, ILogger<ProducerWorker> logger)
    {
        _producer = producer;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("{ProducerName} starting...", _producerName);

        var messageCount = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                messageCount++;
                var message = new Message(
                    _producerName,
                    $"Message #{messageCount} from {_producerName} at {DateTime.UtcNow:HH:mm:ss}");

                await _producer.ProduceAsync(message, stoppingToken);

                _logger.LogInformation(
                    "{ProducerName} sent message #{Count}",
                    _producerName,
                    messageCount);

                // Wait 3 seconds before sending next message (different rate than Producer1)
                await Task.Delay(3000, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error producing message");
                await Task.Delay(5000, stoppingToken);
            }
        }

        _logger.LogInformation("{ProducerName} stopped", _producerName);
    }
}
