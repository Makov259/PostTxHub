using System.Text.Json;
using System.Text.Json.Serialization;
using PostTxHub.Contracts;
using PostTxHub.Messaging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using PostTxHub.Worker.Persistence;

namespace PostTxHub.Worker;

public sealed class TransactionConsumer(IConnection connection, ILogger<TransactionConsumer> logger,IServiceScopeFactory scopeFactory)
    : BackgroundService
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
                // Permanent failure: the message itself is broken. Retrying never fixes it.
                logger.LogError(ex, "Unreadable message {MessageId} (delivery {DeliveryTag}). Dropping it.",
                    ea.BasicProperties.MessageId, ea.DeliveryTag);

                await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false);
            }
            catch (Exception ex)
            {
                // Maybe temporary (e.g. database down). Put it back so it gets retried.
                logger.LogWarning(ex, "Failed to process {MessageId} (delivery {DeliveryTag}). Requeueing.",
                    ea.BasicProperties.MessageId, ea.DeliveryTag);

                await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true);
            }
        };

        await channel.BasicConsumeAsync(RabbitMqTopology.ReceivedQueue, autoAck: false, consumer, stoppingToken);

        // Keep running until the app is told to stop
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
}