namespace PostTxHub.Contracts;

public record TransactionProcessed(
    
        Guid TransactionId,
        string ExternalId,
        TransactionType Type,
        decimal Amount,
        string Currency,
        DateTimeOffset ProcessedAt
    );