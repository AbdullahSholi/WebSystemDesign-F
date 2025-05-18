using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
using RabbitMQ_MongoDB_Consumer.Models;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using SignalRHub;

namespace RabbitMQ_MongoDB_Consumer;

public class RabbitMQConsumer : IRabbitMqConsumer
{
    private readonly IMongoCollection<ServerStatistics> _collection;

    private readonly double _cpuUsageAnomalyThresholdPercentage;
    private readonly double _cpuUsageThresholdPercentage;
    private readonly string _exchangeName;

    private readonly ConnectionFactory _factory;
    private readonly IHubContext<AlertHub> _hubContext;
    private readonly double _memoryAnomalyThreshold;
    private readonly double _memoryUsageThresholdPercentage;
    private readonly bool _queueAutoAck;
    private readonly bool _queueAutoDelete;

    private readonly bool _queueDurable;
    private readonly bool _queueExclusive;
    private readonly string _queueName;
    private readonly string _routingKey;
    private double _previousCpuUsage;
    private double _previousMemoryUsage;


    public RabbitMQConsumer(IHubContext<AlertHub> hubContext, IConfiguration configuration,
        IMongoClient mongoClient)
    {
        var databaseName = configuration["ServerStatisticsConfig:DatabaseName"];
        var collectionName = configuration["ServerStatisticsConfig:CollectionName"];
        var database = mongoClient.GetDatabase(databaseName);
        _collection = database.GetCollection<ServerStatistics>(collectionName);
        _hubContext = hubContext;

        _queueName = configuration["ServerStatisticsConfig:QueueName"] ?? "ServerStatistics";
        _exchangeName = configuration["ServerStatisticsConfig:ExchangeName"] ?? "server_statistics_topic_exchange";
        _routingKey = configuration["ServerStatisticsConfig:RoutingKey"] ?? "";
        _memoryAnomalyThreshold =
            Convert.ToDouble(configuration["AnomalyDetectionConfig:MemoryUsageAnomalyThresholdPercentage"]);
        _cpuUsageAnomalyThresholdPercentage =
            Convert.ToDouble(configuration["AnomalyDetectionConfig:CpuUsageAnomalyThresholdPercentage"]);
        _memoryUsageThresholdPercentage =
            Convert.ToDouble(configuration["AnomalyDetectionConfig:MemoryUsageThresholdPercentage"]);
        _cpuUsageThresholdPercentage =
            Convert.ToDouble(configuration["AnomalyDetectionConfig:CpuUsageThresholdPercentage"]);

        _queueDurable = bool.Parse(configuration["RabbitMQ:QueueDurable"] ?? "true");
        _queueExclusive = bool.Parse(configuration["RabbitMQ:QueueExclusive"] ?? "false");
        _queueAutoDelete = bool.Parse(configuration["RabbitMQ:QueueAutoDelete"] ?? "false");
        _queueAutoAck = bool.Parse(configuration["RabbitMQ:QueueAutoAck"] ?? "true");
        var hostName = configuration["RabbitMQ:HostName"] ?? "localhost";
        var userName = configuration["RabbitMQ:UserName"] ?? "guest";
        var password = configuration["RabbitMQ:Password"] ?? "guest";
        var port = int.Parse(configuration["RabbitMQ:Port"] ?? "5672");

        _factory = new ConnectionFactory
        {
            HostName = hostName,
            UserName = userName,
            Password = password,
            Port = port
        };
    }

    public async Task StartConsumingAsync()
    {
        var connection = await _factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();

        try
        {
            await channel.ExchangeDeclarePassiveAsync(_exchangeName);
        }
        catch
        {
            await channel.ExchangeDeclareAsync(
                _exchangeName,
                ExchangeType.Topic,
                _queueDurable,
                _queueAutoDelete
            );
        }

        await channel.QueueDeclareAsync(_queueName, _queueDurable, _queueExclusive, _queueAutoDelete);
        await channel.QueueBindAsync(_queueName, _exchangeName, _routingKey);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (model, eventArgs) =>
        {
            var body = eventArgs.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);
            var routingKey = eventArgs.RoutingKey;
            var parts = routingKey.Split('.');

            var statistics = JsonSerializer.Deserialize<ServerStatistics>(message);
            statistics.ServerIdentifier = parts[1];

            Console.WriteLine($"ServerStatistics message received: {statistics}");

            var currentMemoryUsage = statistics.MemoryUsage;
            var currentCpuUsage = statistics.CpuUsage;

            await MemoryUsageAnomalyAlert(currentMemoryUsage, statistics);
            await CpuUsageAnomalyAlert(currentCpuUsage, statistics);
            await MemoryHighUsageAlert(currentMemoryUsage, statistics);
            await CpuHighUsageAlert(currentCpuUsage, statistics);

            await _collection.InsertOneAsync(statistics);
        };

        await channel.BasicConsumeAsync(_queueName, _queueAutoAck, consumer);
        Console.WriteLine(CustomMessages.ExitMessage);
        await Task.Delay(Timeout.Infinite);
    }

    private async Task MemoryUsageAnomalyAlert(double currentMemoryUsage, ServerStatistics statistics)
    {
        if (currentMemoryUsage > _previousMemoryUsage
            * (1 + _memoryAnomalyThreshold))
        {
            Console.WriteLine(CustomMessages.MemoryAnomalyDetected);
            await _hubContext.Clients.All.SendAsync(CustomMessages.MethodName, new
            {
                Type = CustomMessages.MemoryUsageAnomaly,
                Server = statistics.ServerIdentifier,
                Current = currentMemoryUsage,
                Previous = _previousMemoryUsage,
                Threshold = _memoryAnomalyThreshold
            });
        }

        _previousMemoryUsage = currentMemoryUsage;
    }

    private async Task CpuUsageAnomalyAlert(double currentCpuUsage, ServerStatistics statistics)
    {
        if (currentCpuUsage > _previousCpuUsage * (1 + _cpuUsageAnomalyThresholdPercentage))
        {
            Console.WriteLine(CustomMessages.CpuAnomalyDetected);
            await _hubContext.Clients.All.SendAsync(CustomMessages.MethodName, new
            {
                Type = CustomMessages.CpuUsageAnomaly,
                Server = statistics.ServerIdentifier,
                Current = currentCpuUsage,
                Previous = _previousCpuUsage,
                Threshold = _cpuUsageAnomalyThresholdPercentage
            });
        }

        _previousCpuUsage = currentCpuUsage;
    }

    private async Task MemoryHighUsageAlert(double currentMemoryUsage, ServerStatistics statistics)
    {
        if (currentMemoryUsage / (currentMemoryUsage +
                                  statistics.AvailableMemory) > _memoryUsageThresholdPercentage)
        {
            Console.WriteLine(CustomMessages.MemoryHighUsageDetected);
            await _hubContext.Clients.All.SendAsync(CustomMessages.MethodName, new
            {
                Type = CustomMessages.MemoryHighUsage,
                Server = statistics.ServerIdentifier,
                MemoryUsage = currentMemoryUsage,
                statistics.AvailableMemory,
                Threshold = _memoryUsageThresholdPercentage
            });
        }
    }

    private async Task CpuHighUsageAlert(double currentCpuUsage, ServerStatistics statistics)
    {
        if (currentCpuUsage > _cpuUsageThresholdPercentage)
        {
            Console.WriteLine(CustomMessages.CpuHighUsageDetected);
            await _hubContext.Clients.All.SendAsync(CustomMessages.MethodName, new
            {
                Type = CustomMessages.CpuHighUsage,
                Server = statistics.ServerIdentifier,
                CpuUsage = currentCpuUsage,
                Threshold = _cpuUsageThresholdPercentage
            });
        }
    }
}