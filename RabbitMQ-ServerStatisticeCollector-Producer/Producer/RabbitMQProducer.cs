using System.Text;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using RabbitMQ.Client;

namespace WebSystemDesign_F.Producer;

public class RabbitMqProducer : IRabbitMqProducer
{
    private readonly string _exchangeName;
    private readonly string _hostName;
    private readonly string _password;
    private readonly int _port;
    private readonly bool _queryAutoDelete;
    private readonly bool _queryDurable;
    private readonly bool _queryExclusive;
    private readonly string _queueName;
    private readonly string _serverIdentifier;
    private readonly string _userName;

    public RabbitMqProducer(IConfiguration configuration)
    {
        _queueName = configuration["ServerStatisticsConfig:QueueName"] ?? "ServerStatistics";
        _exchangeName = configuration["ServerStatisticsConfig:ExchangeName"] ?? "server_statistics_topic_exchange";
        _serverIdentifier = configuration["ServerStatisticsConfig:ServerIdentifier"] ?? "linux1";
        _hostName = configuration["RabbitMQ:HostName"] ?? "localhost";
        _userName = configuration["RabbitMQ:UserName"] ?? "guest";
        _password = configuration["RabbitMQ:Password"] ?? "guest";
        _port = int.Parse(configuration["RabbitMQ:Port"] ?? "5672");
        _queryDurable = bool.Parse(configuration["RabbitMQ:QueryDurable"] ?? "true");
        _queryAutoDelete = bool.Parse(configuration["RabbitMQ:QueryAutoDelete"] ?? "false");
        _queryExclusive = bool.Parse(configuration["RabbitMQ:QueryExclusive"] ?? "false");
    }

    public async Task SendServerStatisticsMessage<T>(T serverStatisticsMessage)
    {
        try
        {
            var factory = new ConnectionFactory
            {
                HostName = _hostName,
                UserName = _userName,
                Password = _password,
                Port = _port
            };

            await using var connection = await factory.CreateConnectionAsync();

            IChannel channel;

            try
            {
                channel = await connection.CreateChannelAsync();
                await channel.ExchangeDeclarePassiveAsync(_exchangeName);
            }
            catch
            {
                channel = await connection.CreateChannelAsync();
                await channel.ExchangeDeclareAsync(
                    _exchangeName,
                    ExchangeType.Topic,
                    _queryDurable,
                    _queryAutoDelete);
            }

            await channel.QueueDeclareAsync(_queueName, _queryDurable, _queryExclusive, _queryAutoDelete);

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