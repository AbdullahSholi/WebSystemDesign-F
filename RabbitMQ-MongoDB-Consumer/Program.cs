using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using RabbitMQ_MongoDB_Consumer;
using SignalRHub;

var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", true, true)
    .AddEnvironmentVariables()
    .Build();

var services = new ServiceCollection();

services.AddLogging(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Debug);
});

services.AddSignalR();

services.AddSingleton<ServerStatisticsConsumer>();

var serviceProvider = services.BuildServiceProvider();

var hubContext = serviceProvider.GetRequiredService<IHubContext<AlertHub>>();

var connectionString = Environment.GetEnvironmentVariable("SERVER_STATISTICS_MONGODB_CONNECTION_STRING");

var mongoClient = new MongoClient(connectionString);

var consumer = new ServerStatisticsConsumer(hubContext, configuration, mongoClient);

await consumer.StartConsumingAsync();