using RabbitMQ.Client;

namespace PostTxHub.Api.Messaging;

public static class RabbitMqTopology
{
    public const string Exchange = "transactions";
    public const string ReceivedQueue = "transactions.received";

    public static async Task DeclareAsync(IChannel channel)
    {
        await channel.ExchangeDeclareAsync(Exchange, ExchangeType.Topic, durable:true);
        await channel.QueueDeclareAsync(ReceivedQueue, durable: true, exclusive: false, autoDelete: false);
        await channel.QueueBindAsync(ReceivedQueue,Exchange,routingKey: "transaction.#");
    }
}