using Microsoft.AspNetCore.Mvc;
using PostTxHub.Api.Messaging;
using PostTxHub.Api.Requests;
using PostTxHub.Contracts;

namespace PostTxHub.Api.Controllers;

[ApiController]
[Route("transactions")]
public sealed class TransactionsController(ITransactionPublisher publisher) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create( [FromBody] CreateTransactionRequest request, CancellationToken cancellationToken)
    { 
        var receivedAt = DateTimeOffset.UtcNow;

        var message = new TransactionReceived
        (
            TransactionId: Guid.NewGuid(),
            ExternalId: request.ExternalId,
            Type: request.Type,
            Amount: request.Amount,
            Currency: request.Currency,
            AccountId: request.AccountId,
            OccurredAt: request.OccurredAt ?? receivedAt,
            ReceivedAt: receivedAt
        );

        await publisher.PublishAsync(message, cancellationToken);

        return Accepted(new { transactionId = message.TransactionId });
    }
}