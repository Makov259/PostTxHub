using PostTxHub.Contracts;

namespace PostTxHub.Api.Publishing;

public interface ITransactionPublisher
{
    Task PublishAsync(TransactionReceived message, CancellationToken cancellationToken);
}