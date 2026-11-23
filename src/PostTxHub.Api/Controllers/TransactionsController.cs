using Microsoft.AspNetCore.Mvc;
using PostTxHub.Api.Publishing;
using PostTxHub.Api.Requests;
using PostTxHub.Contracts;
namespace PostTxHub.Api.Controllers;

[ApiController]
[Route("transactions")]
public sealed class TransactionsController(ITransactionPublisher publisher,IServiceScopeFactory scopeFactory) : ControllerBase
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

    /*
    [HttpGet]
    public async Task<IActionResult> Get(Guid transactionId, CancellationToken cancellationToken)
    {
        TransactionReceived? tx = null;
    
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PostTxHubDbContext>()
        } 
    }*/
}