namespace PostTxHub.Contracts;

public sealed record TransactionReceived(
    Guid TransactionId,
    string ExternalId,
    TransactionType Type,
    decimal Amount,
    string Currency,
    string AccountId,
    DateTimeOffset OccurredAt,
    DateTimeOffset ReceivedAt
    );