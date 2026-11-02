using PostTxHub.Messaging;
using PostTxHub.Worker;
using RabbitMQ.Client;

var builder = Host.CreateApplicationBuilder(args);

var rabbitOptions = builder.Configuration
                        .GetSection(RabbitMqOptions.SectionName)
                        .Get<RabbitMqOptions>()
                    ?? throw new InvalidOperationException("Missing 'RabbitMq' configuration section.");

var factory = new ConnectionFactory
{
    HostName = rabbitOptions.HostName,
    Port = rabbitOptions.Port,
    UserName = rabbitOptions.UserName,
    Password = rabbitOptions.Password,
    ClientProvidedName = "posttxhub-worker"
};

var rabbitConnection = await factory.CreateConnectionAsync();
builder.Services.AddSingleton(rabbitConnection);

await using (var setupChannel = await rabbitConnection.CreateChannelAsync())
{
    await RabbitMqTopology.DeclareAsync(setupChannel);
}

builder.Services.AddHostedService<TransactionConsumer>();

await builder.Build().RunAsync();