using System.ComponentModel.DataAnnotations;
using PostTxHub.Contracts;

namespace PostTxHub.Api.Contracts;

public sealed record CreateTransactionRequest
{ 
    
   [Required, StringLength(64, MinimumLength = 1)]
   public required string  ExternalId { get; init; }
   
   [Required]
   public required TransactionType Type { get; init; }
   
   [Range(typeof(decimal), "0.01", "1000000000",
       ParseLimitsInInvariantCulture = true,
       ConvertValueInInvariantCulture = true)]
   public required decimal Amount { get; init; }

   [Required, RegularExpression("^[A-Z]{3}$")]
   public required string Currency { get; init; }
   
   [Required, StringLength(34, MinimumLength = 1)]
   public required string AccountId { get; init; }
   
   public DateTimeOffset? OccurredAt { get; init; }
   

}