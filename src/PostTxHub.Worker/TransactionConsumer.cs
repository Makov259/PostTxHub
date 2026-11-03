using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using PostTxHub.Contracts;
using PostTxHub.Messaging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace PostTxHub.Worker;

public sealed class TransactionConsumer(IConnection connection, ILogger<TransactionConsumer> logger)
    : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
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

                logger.LogInformation("Received {TransactionId} {Type} {Amount} {Currency}",
                    message.TransactionId, message.Type, message.Amount, message.Currency);

                // TEMPORARY: ack right after logging. Step 6 moves this to AFTER the DB save.
                await channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Could not process message {DeliveryTag}. Body: {Body}",
                    ea.DeliveryTag, Encoding.UTF8.GetString(ea.Body.Span));

                // requeue: false. Broken JSON will never get fixed by retrying.
                // No DLQ yet, so the message is dropped. The body survives in the log above.
                // Week 3: dead-letter queue catches these instead.
                await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false);
            }
        };

        await channel.BasicConsumeAsync(RabbitMqTopology.ReceivedQueue, autoAck: false, consumer, stoppingToken);

        // Keep running until the app is told to stop
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
}