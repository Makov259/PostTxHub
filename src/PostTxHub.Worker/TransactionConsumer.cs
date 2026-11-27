using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PostTxHub.Contracts;
using PostTxHub.Messaging;
using PostTxHub.Worker.Persistence;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
namespace PostTxHub.Worker;

public sealed class TransactionConsumer(
    IConnection connection,
    IServiceScopeFactory scopeFactory,
    ILogger<TransactionConsumer> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        RespectRequiredConstructorParameters = true,
        RespectNullableAnnotations = true
    };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
        await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 10, global: false, stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, ea) =>
        {
            TransactionReceived? message = null;
            try
            {
                 message = JsonSerializer.Deserialize<TransactionReceived>(ea.Body.Span, JsonOptions)
                              ?? throw new JsonException("Message body was null.");
                 
                await using (var scope = scopeFactory.CreateAsyncScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<PostTxHubDbContext>();

                    var now = DateTimeOffset.UtcNow;
                    db.Transactions.Add(new TransactionEntity
                    {
                        Id = message.TransactionId,
                        ExternalId = message.ExternalId,
                        Type = message.Type,
                        Amount = message.Amount,
                        Currency = message.Currency,
                        AccountId = message.AccountId,
                        OccurredAt = message.OccurredAt.ToUniversalTime(), // Postgres only accepts UTC
                        ReceivedAt = message.ReceivedAt.ToUniversalTime(),
                        ProcessedAt = now
                    });

                    var processed = new TransactionProcessed(
                        message.TransactionId,
                        message.ExternalId,
                        message.Type,
                        message.Amount,
                        message.Currency,
                        now
                    );
                    
                    db.OutboxMessages.Add(new OutboxMessage
                    {
                        Id = Guid.NewGuid(),
                        Type = nameof(TransactionProcessed),
                        Payload = JsonSerializer.Serialize(processed, JsonOptions),
                        CreatedAt = now

                    });
                    
                    await db.SaveChangesAsync(stoppingToken);
                }
                logger.LogInformation("Saved {TransactionId} {Type} {Amount} {Currency}",
                    message.TransactionId, message.Type, message.Amount, message.Currency);

                // Ack only AFTER the row is committed. If we crash before this line, RabbitMQ redelivers.
                await channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
            }
            catch (JsonException ex)
            {
                // 1. Broken message: retrying never fixes it. Send to the DLQ
                logger.LogError(ex, "Unreadable message {MessageId} (delivery {DeliveryTag}). Sent to DLQ.",
                    ea.BasicProperties.MessageId, ea.DeliveryTag);

                await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false);
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException
                                               {
                                                   SqlState: PostgresErrorCodes.UniqueViolation,
                                                   ConstraintName: "PK_Transactions"
                                               })
            {
                // 2. Redelivered duplicate: this Id is already in the table, so the job is already done.
                logger.LogInformation("Duplicate delivery {MessageId}, already saved. Acking.",
                    ea.BasicProperties.MessageId);

                await channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException
                                               {
                                                   SqlState: PostgresErrorCodes.UniqueViolation,
                                                   ConstraintName: "IX_Transactions_ExternalId"
                                               })
            {
                //3. Client duplicate , same ExternalId
                
                await using var scope = scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<PostTxHubDbContext>();

                var existing = await db.Transactions
                    .AsNoTracking()
                    .SingleAsync(t => t.ExternalId == message!.ExternalId, stoppingToken);

                bool sameData = false;
                if (existing.Amount == message!.Amount && existing.Type == message.Type &&
                    existing.Currency == message.Currency && existing.AccountId == message.AccountId) sameData = true;
                if (sameData)
                {
                    logger.LogInformation("Client retry ,{ExternalId} already saved {MessageId}, acking.",message.ExternalId,ea.BasicProperties.MessageId);
                    await channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
                }
                else 
                {
                    logger.LogError(ex,"Conflict: ExternalId {ExternalId} already saved with different data. Message {MessageId} sent to DLQ.",message.ExternalId,ea.BasicProperties.MessageId);
                    await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false);
                }

            }
            catch (Exception ex) when (IsTransient(ex))
            {
                // 4. Temporary: database down, timeout, or app shutting down.Park it in a waiting room and try again later.
               var attempt = GetRetryCount(ea.BasicProperties);
               if( attempt < RabbitMqTopology.RetryQueues.Length)
               {
                   var retryQueue = RabbitMqTopology.RetryQueues[attempt];
                   var props = new BasicProperties
                       
                   {
                       Persistent = true,
                       ContentType = ea.BasicProperties.ContentType,
                       MessageId = ea.BasicProperties.MessageId,
                       Headers = new Dictionary<string, object?> { ["retry-count"] = attempt + 1 }
                   };
                   await channel.BasicPublishAsync(exchange: "", routingKey: retryQueue,
                       mandatory: false, basicProperties: props, body: ea.Body);
                   await channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
 
                   logger.LogWarning("Temporary failure for {MessageId}. Attempt {Attempt}, retrying via {RetryQueue}.",
                       ea.BasicProperties.MessageId, attempt + 1, retryQueue);
               }
               else
               {
                   logger.LogError(ex, "Out of retries for {MessageId} after {Attempt} attempts. Sent to DLQ.",
                       ea.BasicProperties.MessageId, attempt);
                   await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false);
               }
            }
            catch (Exception ex)
            {
                // 5. Unknown error: retrying probably won't help. Send to the DLQ.
                logger.LogError(ex, "Permanent failure for {MessageId}. Sent to DLQ.",
                    ea.BasicProperties.MessageId);

                await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false);
            }
        };
        await channel.BasicConsumeAsync(RabbitMqTopology.ReceivedQueue, autoAck: false, consumer, stoppingToken);

        // Keep running until the app is told to stop
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private static bool IsTransient(Exception ex)
    {
        // Walk the chain, because EF often wraps the real error inside another one.
        for (var e = ex; e is not null; e = e.InnerException)
        {
            if (e is TimeoutException or OperationCanceledException or NpgsqlException { IsTransient: true })
                return true;
        }
        return false;
    }

    private static int GetRetryCount(IReadOnlyBasicProperties props)
    {
        if(props.Headers is not null
           && props.Headers.TryGetValue("retry-count", out var value)
           && value is int count) return count;
        return 0;
    }
}
