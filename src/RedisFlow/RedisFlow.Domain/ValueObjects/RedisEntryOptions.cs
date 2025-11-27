using StackExchange.Redis;

namespace RedisFlow.Domain.ValueObjects;

public sealed class RedisEntryOptions
{
    public RedisValue? MessageId { get; init; } = "*"; // using null or * default to auto-generated ID, preferred
    public long? MaxLength { get; init; } = null;
    public bool ApproximateTrimming { get; init; } = true;
    public long? Limit { get; init; } = null;
    public StreamTrimMode TrimMode { get; init; } = StreamTrimMode.KeepReferences;
    public CommandFlags Flags { get; init; } = CommandFlags.None;
}