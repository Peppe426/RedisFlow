using Aspire.Hosting;
using Aspire.Hosting.Testing;
using StackExchange.Redis;

namespace RedisFlow.Integration;

[TestFixture]
[Category("Integration test")]
[Parallelizable(ParallelScope.None)]
public abstract class RedisIntegrationTestBase : IAsyncDisposable
{
    private DistributedApplication? _app;
    private IConnectionMultiplexer? _redis;
    protected const string StreamKey = "test-messages";

    protected IConnectionMultiplexer Redis => _redis ?? throw new InvalidOperationException("Redis not initialized");

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        // Create the Aspire app host builder
        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.RedisFlow_AppHost>();
        
        _app = await appHost.BuildAsync();
        await _app.StartAsync();

        // Get the Redis connection string from the app
        var connectionString = await _app.GetConnectionStringAsync("redis");
        
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException("Redis connection string is null or empty. Ensure Redis is added in AppHost.");
        }

        _redis = await ConnectionMultiplexer.ConnectAsync(connectionString);
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await DisposeAsync();
    }

    [TearDown]
    public async Task TearDown()
    {
        // Clean up the stream after each test
        if (_redis != null)
        {
            var database = _redis.GetDatabase();
            await database.KeyDeleteAsync(StreamKey);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_redis != null)
        {
            await _redis.CloseAsync();
            _redis.Dispose();
            _redis = null;
        }

        if (_app != null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
            _app = null;
        }

        GC.SuppressFinalize(this);
    }
}
