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

        // Take at most 10 unacked messages at a time
        await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 10, global: false, stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, ea) =>
        {
            try
            {
                var message = JsonSerializer.Deserialize<TransactionReceived>(ea.Body.Span, JsonOptions)
                              ?? throw new JsonException("Message body was null.");

                await using (var scope = scopeFactory.CreateAsyncScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<PostTxHubDbContext>();

                    db.Transactions.Add(new TransactionEntity
                    {
                        Id = message.TransactionId,
                        ExternalId = message.ExternalId,
                        Type = message.Type,
                        Amount = message.Amount,
                        Currency = message.Currency,
                        AccountId = message.AccountId,
                        OccurredAt = message.OccurredAt.ToUniversalTime(),   // Postgres only accepts UTC
                        ReceivedAt = message.ReceivedAt.ToUniversalTime(),
                        ProcessedAt = DateTimeOffset.UtcNow
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
                // 1. Broken message: retrying never fixes it. Drop it.
                logger.LogError(ex, "Unreadable message {MessageId} (delivery {DeliveryTag}). Dropping it.",
                    ea.BasicProperties.MessageId, ea.DeliveryTag);

                await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false);
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                // 2. Redelivered duplicate: this Id is already in the table, so the job is already done.
                logger.LogInformation("Duplicate delivery {MessageId}, already saved. Acking.",
                    ea.BasicProperties.MessageId);

                await channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
            }
            catch (Exception ex) when (IsTransient(ex))
            {
                // 3. Temporary: database down, timeout, or app shutting down. Retry later.
                logger.LogWarning(ex, "Temporary failure for {MessageId}. Requeueing in 2s.",
                    ea.BasicProperties.MessageId);

                await Task.Delay(TimeSpan.FromSeconds(2));   // crude brake against a tight loop. Week 3: real retries.
                await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true);
            }
            catch (Exception ex)
            {
                // 4. Unknown error: retrying probably won't help. Week 3: send to the DLQ instead.
                logger.LogError(ex, "Permanent failure for {MessageId}. Dropping it.",
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
}