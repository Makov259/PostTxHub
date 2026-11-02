using System.Text.Json.Serialization;
using PostTxHub.Api.Messaging;
using RabbitMQ.Client;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi();

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
    ClientProvidedName = "posttxhub-api"
};

var rabbitConnection = await factory.CreateConnectionAsync();
builder.Services.AddSingleton(rabbitConnection);
builder.Services.AddSingleton<ITransactionPublisher, RabbitMqTransactionPublisher>();

await using (var setupChannel = await rabbitConnection.CreateChannelAsync())
{await RabbitMqTopology.DeclareAsync(setupChannel);}

var app = builder.Build();

if (app.Environment.IsDevelopment())
{app.MapOpenApi(); }

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

await app.RunAsync();
