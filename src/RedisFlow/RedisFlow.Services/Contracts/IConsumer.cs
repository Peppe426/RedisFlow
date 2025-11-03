using System;
using System.Threading;
using System.Threading.Tasks;
using RedisFlow.Domain.ValueObjects;

namespace RedisFlow.Services.Contracts;

public interface IConsumer
{
	/// <summary>
	/// Begins consuming messages and invokes the provided handler for each message.
	/// Implementations should call the handler for each received message until cancelled.
	/// </summary>
	/// <param name="handler">Async handler invoked for each message. The handler receives the message and a CancellationToken.</param>
	/// <param name="cancellationToken">Cancellation token to stop consuming.</param>
	Task ConsumeAsync(Func<Message, CancellationToken, Task> handler, CancellationToken cancellationToken = default);
}