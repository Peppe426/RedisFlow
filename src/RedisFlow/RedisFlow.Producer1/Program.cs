using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RedisFlow.Domain.ValueObjects;
using RedisFlow.Services.Contracts;
using RedisFlow.Services.Implementations;
using StackExchange.Redis;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

// Register Redis connection
var redisConnectionString = builder.Configuration["ConnectionStrings:redis"] 
    ?? "localhost:6379";

builder.Services.AddSingleton<IConnectionMultiplexer>(sp => 
    ConnectionMultiplexer.Connect(redisConnectionString));

// Register Producer
builder.Services.AddSingleton<IProducer, RedisProducer>();

var host = builder.Build();

var logger = host.Services.GetRequiredService<ILogger<Program>>();
var producer = host.Services.GetRequiredService<IProducer>();

logger.LogInformation("Producer1 starting...");

var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    logger.LogInformation("Producer1 shutting down...");
    cts.Cancel();
    e.Cancel = true;
};

try
{
    var counter = 0;
    while (!cts.Token.IsCancellationRequested)
    {
        counter++;
        var message = new Message(
            producer: "Producer1",
            content: $"Message #{counter} from Producer1 at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}");

        await producer.ProduceAsync(message, cts.Token);
        
        await Task.Delay(TimeSpan.FromSeconds(2), cts.Token);
    }
}
catch (OperationCanceledException)
{
    logger.LogInformation("Producer1 cancelled");
}
catch (Exception ex)
{
    logger.LogError(ex, "Producer1 encountered an error");
    throw;
}

logger.LogInformation("Producer1 stopped");
await host.StopAsync();
