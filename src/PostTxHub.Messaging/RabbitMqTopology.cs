using RabbitMQ.Client;

namespace PostTxHub.Messaging;

public static class RabbitMqTopology
{
    public const string Exchange = "transactions";
    public const string ReceivedQueue = "transactions.received";
    public const string Retry1sQueue = "transactions.retry.1s";
    public const string Retry5sQueue = "transactions.retry.5s";
    public const string Retry30sQueue = "transactions.retry.30s";

    public static readonly string[] RetryQueues = [Retry1sQueue, Retry5sQueue, Retry30sQueue];
   
    

    public static async Task DeclareAsync(IChannel channel)
    {
        await channel.ExchangeDeclareAsync(Exchange, ExchangeType.Topic, durable:true);
        await channel.QueueDeclareAsync(ReceivedQueue, durable: true, exclusive: false, autoDelete: false);
        await channel.QueueBindAsync(ReceivedQueue,Exchange,routingKey: "transaction.#");
        
        await channel.QueueDeclareAsync(Retry1sQueue, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-message-ttl"] = 1000,
                ["x-dead-letter-exchange"] = "",
                ["x-dead-letter-routing-key"] = ReceivedQueue
            });
        await channel.QueueDeclareAsync(Retry5sQueue, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-message-ttl"] = 5000,
                ["x-dead-letter-exchange"] = "",
                ["x-dead-letter-routing-key"] = ReceivedQueue
            });
        await channel.QueueDeclareAsync(Retry30sQueue, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-message-ttl"] = 30000,
                ["x-dead-letter-exchange"] = "",
                ["x-dead-letter-routing-key"] = ReceivedQueue
            });
    }
}


