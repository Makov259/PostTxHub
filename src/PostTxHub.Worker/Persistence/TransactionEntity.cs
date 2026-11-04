using PostTxHub.Contracts;

namespace PostTxHub.Worker.Persistence;

public sealed class TransactionEntity
{
    public Guid Id { get; set; }
    public string ExternalId { get; set; } = string.Empty;
    public TransactionType Type  { get; set; }
    public decimal Amount { get; set; }
    public string Currency  { get; set; } = string.Empty;
    public string AccountId { get; set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
    public DateTimeOffset ProcessedAt { get; set; }
    
}