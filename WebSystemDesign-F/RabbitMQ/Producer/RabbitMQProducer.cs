using System.Text;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using RabbitMQ.Client;

namespace WebSystemDesign_F.RabbitMQ.Producer;

public class RabbitMqProducer : IRabbitMqProducer
{
    private readonly string _exchangeName;
    private readonly string _queueName;
    private readonly string _serverIdentifier;

    public RabbitMqProducer(IConfiguration configuration)
    {
        _queueName = configuration["ServerStatisticsConfig:QueueName"] ?? "ServerStatistics";
        _exchangeName = configuration["ServerStatisticsConfig:ExchangeName"] ?? "server_statistics_topic_exchange";
        _serverIdentifier = configuration["ServerStatisticsConfig:ServerIdentifier"] ?? "linux1";
    }

    public async Task SendServerStatisticsMessage<T>(T serverStatisticsMessage)
    {
        try
        {
            var factory = new ConnectionFactory
            {
                HostName = "localhost",
                UserName = "guest",
                Password = "guest",
                Port = 5672
            };

            using var connection = await factory.CreateConnectionAsync();

            using var channel = await connection.CreateChannelAsync();

            await channel.ExchangeDeclareAsync(_exchangeName, durable: true, autoDelete: false,
                type: ExchangeType.Topic);

            await channel.QueueDeclareAsync(_queueName, true, false, false);

            var routingKey = $"ServerStatistics.{_serverIdentifier}";
            await channel.QueueBindAsync(_queueName, _exchangeName, routingKey);
            var json = JsonConvert.SerializeObject(serverStatisticsMessage);
            var body = Encoding.UTF8.GetBytes(json);
            await channel.BasicPublishAsync(_exchangeName, routingKey, body);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in SendServerStatisticsMessage: {ex}");
        }
    }
}