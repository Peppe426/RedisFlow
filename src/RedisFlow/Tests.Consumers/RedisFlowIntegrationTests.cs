using Aspire.Hosting.Testing;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RedisFlow.Domain.ValueObjects;
using RedisFlow.Services.Implementations;
using StackExchange.Redis;

namespace Tests.Consumers;

[TestFixture]
[Category("IntegrationTest")]
public class RedisFlowIntegrationTests
{
    [Test]
    public async Task Should_ProduceAndConsumeMessage_When_IntegrationTest()
    {
        try
        {
            // Given
            var appHost = await DistributedApplicationTestingBuilder
                .CreateAsync<Projects.RedisFlow_AppHost>();
            
            await using var app = await appHost.BuildAsync();
            await app.StartAsync();

            var connectionString = await app.GetConnectionStringAsync("redis") 
                ?? throw new InvalidOperationException("Redis connection string is null");

            var redis = await ConnectionMultiplexer.ConnectAsync(connectionString);
            var streamKey = $"test-stream-{Guid.NewGuid():N}";

            var producerLogger = Mock.Of<ILogger<RedisStreamProducer>>();
            var producer = new RedisStreamProducer(redis, producerLogger, streamKey);

            var consumerLogger = Mock.Of<ILogger<RedisStreamConsumer>>();
            var consumer = new RedisStreamConsumer(
                redis, 
                consumerLogger, 
                streamKey, 
                groupName: $"test-group-{Guid.NewGuid():N}");

            var receivedMessages = new List<Message>();
            var messageCount = 3;

            async Task Handler(Message msg, CancellationToken ct)
            {
                receivedMessages.Add(msg);
                await Task.CompletedTask;
            }

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            // When
            // Start consumer in background
            var consumerTask = Task.Run(async () => 
                await consumer.ConsumeAsync(Handler, cts.Token), cts.Token);

            // Give consumer time to start
            await Task.Delay(500);

            // Produce messages
            for (int i = 0; i < messageCount; i++)
            {
                var message = new Message($"producer-integration", $"test message {i}");
                await producer.ProduceAsync(message);
            }

            // Wait for messages to be consumed
            var timeout = DateTime.UtcNow.AddSeconds(5);
            while (receivedMessages.Count < messageCount && DateTime.UtcNow < timeout)
            {
                await Task.Delay(100);
            }

            cts.Cancel();
            
            try
            {
                await consumerTask;
            }
            catch (OperationCanceledException)
            {
                // Expected
            }

            // Then
            receivedMessages.Should().HaveCount(messageCount);
            receivedMessages.Should().AllSatisfy(msg =>
            {
                msg.Producer.Should().Be("producer-integration");
                msg.Content.Should().StartWith("test message");
            });
        }
        catch (Aspire.Hosting.DistributedApplicationException ex) when (ex.Message.Contains("Container runtime"))
        {
            Assert.Ignore("Docker is not available. Skipping Aspire integration tests that require Docker.");
        }
    }

    [Test]
    public async Task Should_ReprocessPendingMessages_When_ConsumerRestarts()
    {
        try
        {
            // Given
            var appHost = await DistributedApplicationTestingBuilder
                .CreateAsync<Projects.RedisFlow_AppHost>();
            
            await using var app = await appHost.BuildAsync();
            await app.StartAsync();

            var connectionString = await app.GetConnectionStringAsync("redis") 
                ?? throw new InvalidOperationException("Redis connection string is null");

            var redis = await ConnectionMultiplexer.ConnectAsync(connectionString);
            var streamKey = $"test-stream-{Guid.NewGuid():N}";
            var groupName = $"test-group-{Guid.NewGuid():N}";

            var producerLogger = Mock.Of<ILogger<RedisStreamProducer>>();
            var producer = new RedisStreamProducer(redis, producerLogger, streamKey);

            // Produce a message
            var originalMessage = new Message("producer-test", "pending message");
            await producer.ProduceAsync(originalMessage);

            var receivedMessages = new List<Message>();

            // First consumer - will fail to process
            var firstConsumerLogger = Mock.Of<ILogger<RedisStreamConsumer>>();
            var firstConsumer = new RedisStreamConsumer(
                redis, 
                firstConsumerLogger, 
                streamKey, 
                groupName,
                "consumer-1");

            var firstAttempt = true;
            async Task FailingHandler(Message msg, CancellationToken ct)
            {
                if (firstAttempt)
                {
                    firstAttempt = false;
                    throw new InvalidOperationException("Simulated failure");
                }
                receivedMessages.Add(msg);
                await Task.CompletedTask;
            }

            using var cts1 = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            
            // When
            // First consumer attempts to process but fails
            try
            {
                await firstConsumer.ConsumeAsync(FailingHandler, cts1.Token);
            }
            catch
            {
                // Expected to fail
            }

            // Wait for claim timeout (5 seconds)
            await Task.Delay(6000);

            // Second consumer - will successfully claim and process
            var secondConsumerLogger = Mock.Of<ILogger<RedisStreamConsumer>>();
            var secondConsumer = new RedisStreamConsumer(
                redis, 
                secondConsumerLogger, 
                streamKey, 
                groupName,
                "consumer-2");

            using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var consumerTask = Task.Run(async () => 
                await secondConsumer.ConsumeAsync(FailingHandler, cts2.Token), cts2.Token);

            // Wait for message to be processed
            var timeout = DateTime.UtcNow.AddSeconds(3);
            while (receivedMessages.Count == 0 && DateTime.UtcNow < timeout)
            {
                await Task.Delay(100);
            }

            cts2.Cancel();
            
            try
            {
                await consumerTask;
            }
            catch (OperationCanceledException)
            {
                // Expected
            }

            // Then
            receivedMessages.Should().HaveCount(1);
            receivedMessages[0].Producer.Should().Be("producer-test");
            receivedMessages[0].Content.Should().Be("pending message");
        }
        catch (Aspire.Hosting.DistributedApplicationException ex) when (ex.Message.Contains("Container runtime"))
        {
            Assert.Ignore("Docker is not available. Skipping Aspire integration tests that require Docker.");
        }
    }
}
