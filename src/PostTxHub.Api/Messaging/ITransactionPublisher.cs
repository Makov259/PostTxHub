using PostTxHub.Api.Contracts;
namespace PostTxHub.Api.Messaging;

public interface ITransactionPublisher
{
    Task PublishAsync(TransactionReceived message, CancellationToken cancellationToken);
}