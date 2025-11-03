using System.Threading;
using System.Threading.Tasks;
using RedisFlow.Domain.ValueObjects;

namespace RedisFlow.Services.Contracts;

public interface IProducer
{
	/// <summary>
	/// Produces/sends a message asynchronously.
	/// </summary>
	/// <param name="message">The message to produce.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The Redis stream message ID.</returns>
	Task<string> ProduceAsync(Message message, CancellationToken cancellationToken = default);
}