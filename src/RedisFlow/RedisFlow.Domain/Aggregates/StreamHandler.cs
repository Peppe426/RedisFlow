using RedisFlow.Domain.Base;
using RedisFlow.Domain.Exceptions;
using RedisFlow.Domain.ValueObjects;
using StackExchange.Redis;

namespace RedisFlow.Domain.Aggregates;

public sealed class EntryOptions
{
    public RedisValue? MessageId { get; init; } = "*"; // using null or * default to auto-generated ID, preferred
    public long? MaxLength { get; init; } = null;
    public bool ApproximateTrimming { get; init; } = true;
    public long? Limit { get; init; } = null;
    public StreamTrimMode TrimMode { get; init; } = StreamTrimMode.KeepReferences;
    public CommandFlags Flags { get; init; } = CommandFlags.None;
}

public sealed class RedisConnectionOptions
{
    public bool AbortOnConnectFail { get; init; } = false;
    public int ConnectTimeout { get; init; } = 5000;
    public int SyncTimeout { get; init; } = 10000;
    public int AsyncTimeout { get; init; } = 10000;
    public int ReconnectRetryInitial { get; init; } = 500;
    public int ReconnectRetryMax { get; init; } = 10000;
    public bool? Ssl { get; init; } = null; // If null, use default logic
    public string? ClientName { get; init; } = null;
}

public sealed record StreamHandler : AggregateRoot, IDisposable
{
    public Connection Connection
    {
        get;
    }
    public string StreamName
    {
        get;
    }

    private ConnectionMultiplexer? _muxer;
    private IDatabase? _db;
    private readonly object _connectLock = new();

    public EntryOptions EntryOptions
    {
        get; private set;
    } = new();

    public RedisConnectionOptions RedisConnectionOptions
    {
        get; private set;
    } = new();

    public StreamHandler(string host, int port, string streamName, string? password = null, bool connectOnInit = false, RedisConnectionOptions? redisConnectionOptions = null)
    {
        ValidateConstructorArguments(host, port, streamName);

        Connection = new Connection(host, port, password);
        StreamName = streamName;
        RedisConnectionOptions = redisConnectionOptions ?? new RedisConnectionOptions();

        if (connectOnInit) Connect();
    }

    private static void ValidateConstructorArguments(string host, int port, string streamName)
    {
        StreamHandlerException.ThrowIfNullOrWhiteSpace(host);
        if (port <= 0 || port > 65535)
            throw new StreamHandlerException($"Can not use port {port}.", new ArgumentOutOfRangeException(nameof(port), port, "Port must be between 1 and 65535."));
        StreamHandlerException.ThrowIfNullOrWhiteSpace(streamName);
    }

    public StreamHandler Connect(bool forceReconnect = false)
    {
        if (!forceReconnect && _muxer?.IsConnected == true) return this;

        lock (_connectLock)
        {
            if (!forceReconnect && _muxer?.IsConnected == true) return this;

            try
            {
                Disconnect();
                var options = new ConfigurationOptions
                {
                    EndPoints = { { Connection.Host, Connection.Port } },
                    Password = Connection.Password,
                    AbortOnConnectFail = RedisConnectionOptions.AbortOnConnectFail,
                    ConnectTimeout = RedisConnectionOptions.ConnectTimeout,
                    SyncTimeout = RedisConnectionOptions.SyncTimeout,
                    AsyncTimeout = RedisConnectionOptions.AsyncTimeout,
                    ReconnectRetryPolicy = new ExponentialRetry(RedisConnectionOptions.ReconnectRetryInitial, RedisConnectionOptions.ReconnectRetryMax),
                    Ssl = RedisConnectionOptions.Ssl ?? (Connection.Port == 6380 || Connection.Host.Contains("redis.cache")),//todo make sure we can use ssl properly
                    ClientName = RedisConnectionOptions.ClientName ?? $"StreamHandler-{StreamName}"
                };

                _muxer = ConnectionMultiplexer.Connect(options);
                _db = _muxer.GetDatabase();
            }
            catch (Exception ex)
            {
                throw new StreamHandlerException($"Failed to connect to Redis at {Connection.Host}:{Connection.Port}", ex);
            }
        }
        return this;
    }

    public void Disconnect()
    {
        lock (_connectLock)
        {
            if (_muxer is not null)
            {
                try { _muxer.Close(); } catch { }
                _muxer.Dispose();
                _muxer = null;
                _db = null;
            }
        }
    }

    public bool IsConnected => _muxer?.IsConnected == true;

    private void EnsureConnected()
    {
        if (_db is null || !IsConnected)
            Connect(forceReconnect: true);
    }

    public async Task<RedisValue> AddAsync(NameValueEntry[] entries, EntryOptions? options = null)
    {
        if (entries is null || entries.Length == 0)
            throw new StreamHandlerException("Will not add empty entries.", new ArgumentException("Entries cannot be null or empty.", nameof(entries)));

        EnsureConnected();
        options ??= EntryOptions;

        // Validate MessageId
        if (options.MessageId is not null)
        {
            var id = options.MessageId.ToString();
            // Valid IDs: null, "*", or a valid Redis stream ID (e.g., "0-0", "1689342342342-0")
            if (string.IsNullOrWhiteSpace(id) || !(id == "*" || System.Text.RegularExpressions.Regex.IsMatch(id, @"^\d+-\d+$")))
            {
                throw new StreamHandlerException($"Invalid MessageId for Redis stream: '{id}'. Must be null, '*', or a valid stream ID (e.g., '0-0').");
            }
        }

        try
        {
            return await _db!.StreamAddAsync(
                key: StreamName,
                streamPairs: entries,
                messageId: options.MessageId,
                maxLength: options.MaxLength,
                useApproximateMaxLength: options.ApproximateTrimming,
                limit: options.Limit,
                trimMode: options.TrimMode,
                flags: options.Flags).ConfigureAwait(false);
        }
        catch (RedisTimeoutException ex)
        {
            throw new StreamHandlerException($"Timeout while adding message to stream '{StreamName}'", ex);
        }
        catch (RedisServerException ex) when (ex.Message.Contains("READONLY"))
        {
            throw new StreamHandlerException($"Redis is read-only – cannot add to stream '{StreamName}'", ex);
        }
        catch (RedisServerException ex) when (ex.Message.Contains("unknown command") || ex.Message.Contains("trimMode"))
        {
            throw new StreamHandlerException(
                $"Redis server does not support the requested trim mode '{options.TrimMode}'. Requires Redis 8.2+ for DeleteReferences/Acknowledged.", ex);
        }
        catch (Exception ex)
        {
            throw new StreamHandlerException($"Failed to add message to stream '{StreamName}'", ex);
        }
    }

    public async Task<StreamEntry[]> ReadAsync(
        string? groupName = null,
        string? consumerName = null,
        RedisValue position = default,
        int? count = 100,
        CommandFlags flags = CommandFlags.None)
    {
        EnsureConnected();

        try
        {
            if (string.IsNullOrWhiteSpace(groupName))
            {
                var pos = position.IsNullOrEmpty ? (RedisValue)"$" : position;
                var result = await _db!.StreamReadAsync(StreamName, pos, count, flags).ConfigureAwait(false);
                return result ?? Array.Empty<StreamEntry>();
            }

            if (string.IsNullOrWhiteSpace(consumerName))
                throw new ArgumentException("consumerName is required when groupName is provided.", nameof(consumerName));

            var groupResult = await _db!.StreamReadGroupAsync(
                key: StreamName,
                groupName: groupName,
                consumerName: consumerName,
                position: position.IsNullOrEmpty ? ">" : position,
                count: count,
                flags: flags).ConfigureAwait(false);

            return groupResult ?? Array.Empty<StreamEntry>();
        }
        catch (RedisServerException ex) when (ex.Message.Contains("NOGROUP"))
        {
            throw new StreamHandlerException($"Consumer group '{groupName}' does not exist on stream '{StreamName}'. Create it first.", ex);
        }
        catch (Exception ex)
        {
            throw new StreamHandlerException($"Failed to read from stream '{StreamName}'", ex);
        }
    }

    public async Task CreateConsumerGroupAsync(string groupName, RedisValue startId = default, bool makeStream = true)
    {
        StreamHandlerException.ThrowIfNullOrWhiteSpace(groupName);

        EnsureConnected();
        if (startId.IsNullOrEmpty) startId = "0-0";

        try
        {
            await _db!.StreamCreateConsumerGroupAsync(StreamName, groupName, startId, makeStream).ConfigureAwait(false);
        }
        catch (RedisServerException ex) when (ex.Message.Contains("BUSYGROUP"))
        {
            // group already exists, ignore
        }
        catch (Exception ex)
        {
            throw new StreamHandlerException($"Failed to create consumer group '{groupName}' on stream '{StreamName}'", ex);
        }
    }

    public async Task<long> AcknowledgeAsync(string groupName, params RedisValue[] messageIds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupName);
        if (messageIds is null || messageIds.Length == 0) return 0;

        EnsureConnected();

        try
        {
            return await _db!.StreamAcknowledgeAsync(StreamName, groupName, messageIds).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new StreamHandlerException($"Failed to acknowledge messages in group '{groupName}'", ex);
        }
    }

    public Task<StreamInfo> GetInfoAsync()
        => Safe(() => _db!.StreamInfoAsync(StreamName));

    public Task<StreamPendingInfo> GetPendingInfoAsync(string groupName)
        => Safe(() => _db!.StreamPendingAsync(StreamName, groupName));

    private async Task<T> Safe<T>(Func<Task<T>> operation)
    {
        EnsureConnected();
        try
        {
            return await operation().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new StreamHandlerException($"Redis operation failed on stream '{StreamName}'", ex);
        }
    }

    private bool _disposed;
    private void Dispose(bool disposing)
    {
        if (!_disposed && disposing) Disconnect();
        _disposed = true;
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    ~StreamHandler() => Dispose(disposing: false);
}