using System.Text.Json.Serialization;
using PostTxHub.Api.Messaging;
using RabbitMQ.Client;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var rabbitOptions = builder.Configuration
                        .GetSection(RabbitMqOptions.SectionName)
                        .Get<RabbitMqOptions>()
                    ?? throw new InvalidOperationException("Missing 'RabbitMq' configuration section.");


var factory = new ConnectionFactory
{
    HostName = rabbitOptions.HostName,
    Port = rabbitOptions.Port,
    UserName = rabbitOptions.Username,
    Password = rabbitOptions.Password,
    ClientProvidedName = "posttxhub-api"
};

var rabbitConnection = await factory.CreateConnectionAsync();
builder.Services.AddSingleton(rabbitConnection);

await using (var setupChannel = await rabbitConnection.CreateChannelAsync())
{
    await RabbitMqTopology.DeclareAsync(setupChannel);
}

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

await app.RunAsync();
