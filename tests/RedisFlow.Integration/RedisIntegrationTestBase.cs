using System.Diagnostics;
using StackExchange.Redis;

namespace RedisFlow.Integration;

[TestFixture]
[Category("Integration test")]
[Parallelizable(ParallelScope.None)]
public abstract class RedisIntegrationTestBase : IAsyncDisposable
{
    private Process? _redisProcess;
    private IConnectionMultiplexer? _redis;
    private const int RedisPort = 6379;
    protected const string StreamKey = "test-messages";

    protected IConnectionMultiplexer Redis => _redis ?? throw new InvalidOperationException("Redis not initialized");

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        // Start Redis container using Docker
        await StartRedisContainerAsync();

        // Wait a bit for Redis to start
        await Task.Delay(2000);

        // Connect to Redis
        var connectionString = $"localhost:{RedisPort},abortConnect=false";
        _redis = await ConnectionMultiplexer.ConnectAsync(connectionString);

        // Verify connection
        if (!_redis.IsConnected)
        {
            throw new InvalidOperationException("Failed to connect to Redis. Ensure Docker is running.");
        }
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

    private async Task StartRedisContainerAsync()
    {
        // Check if container already exists and remove it
        var checkProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = "ps -a --filter name=redisflow-test --format {{.ID}}",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        checkProcess.Start();
        var containerId = await checkProcess.StandardOutput.ReadToEndAsync();
        await checkProcess.WaitForExitAsync();

        if (!string.IsNullOrWhiteSpace(containerId))
        {
            // Remove existing container
            var removeProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "docker",
                    Arguments = $"rm -f {containerId.Trim()}",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            removeProcess.Start();
            await removeProcess.WaitForExitAsync();
        }

        // Start new Redis container
        _redisProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = $"run --name redisflow-test -p {RedisPort}:6379 -d redis:7-alpine",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        _redisProcess.Start();
        await _redisProcess.WaitForExitAsync();

        if (_redisProcess.ExitCode != 0)
        {
            var error = await _redisProcess.StandardError.ReadToEndAsync();
            throw new InvalidOperationException($"Failed to start Redis container: {error}");
        }
    }

    private async Task StopRedisContainerAsync()
    {
        if (_redisProcess == null)
            return;

        var stopProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = "stop redisflow-test",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        stopProcess.Start();
        await stopProcess.WaitForExitAsync();

        var removeProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = "rm redisflow-test",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        removeProcess.Start();
        await removeProcess.WaitForExitAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_redis != null)
        {
            await _redis.CloseAsync();
            _redis.Dispose();
            _redis = null;
        }

        await StopRedisContainerAsync();

        _redisProcess?.Dispose();
        _redisProcess = null;

        GC.SuppressFinalize(this);
    }
}
