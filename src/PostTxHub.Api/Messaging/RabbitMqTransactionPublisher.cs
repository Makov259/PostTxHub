using System.Text.Json;
using System.Text.Json.Serialization;
using PostTxHub.Api.Contracts;
using RabbitMQ.Client;

namespace PostTxHub.Api.Messaging;

public sealed class RabbitMqTransactionPublisher(IConnection connection) : ITransactionPublisher
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly CreateChannelOptions ChannelOptions = new(
        publisherConfirmationsEnabled: true,
        publisherConfirmationTrackingEnabled: true);

    public async Task PublishAsync(TransactionReceived message, CancellationToken cancellationToken)
    {
        await using var channel = await connection.CreateChannelAsync(ChannelOptions, cancellationToken);

        var body = JsonSerializer.SerializeToUtf8Bytes(message, JsonOptions);

        var properties = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json",
            MessageId = message.TransactionId.ToString()
        };

        var routingKey = $"transaction.{message.Type.ToString().ToLowerInvariant()}";

        await channel.BasicPublishAsync(
            exchange: RabbitMqTopology.Exchange,
            routingKey: routingKey,
            mandatory: true,
            basicProperties: properties,
            body: body,
            cancellationToken: cancellationToken);
    }
}