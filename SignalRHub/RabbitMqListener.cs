using System.Text;
using Microsoft.AspNetCore.SignalR;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using SignalRHub;

public class RabbitMqListener
{
    private readonly string _hostName;
    private readonly IHubContext<AlertHub> _hubContext;
    private readonly string _methodName;
    private readonly string _password;
    private readonly bool _queueAutoAck;
    private readonly bool _queueAutoDelete;
    private readonly bool _queueDurable;
    private readonly bool _queueExclusive;
    private readonly string _queueName;
    private readonly string _userName;
    private IChannel _channel;
    private IConnection _connection;

    public RabbitMqListener(IHubContext<AlertHub> hubContext, IConfiguration configuration)
    {
        _hubContext = hubContext;
        _hostName = configuration["RabbitMQ:HostName"] ?? "rabbitmq";
        _userName = configuration["RabbitMQ:UserName"] ?? "guest";
        _password = configuration["RabbitMQ:Password"] ?? "guest";
        _queueName = configuration["RabbitMQ:QueueName"] ?? "ServerStatistics";
        _queueDurable = bool.Parse(configuration["RabbitMQ:QueueDurable"] ?? "true");
        _queueExclusive = bool.Parse(configuration["RabbitMQ:QueueExclusive"] ?? "false");
        _queueAutoDelete = bool.Parse(configuration["RabbitMQ:QueueAutoDelete"] ?? "false");
        _queueAutoAck = bool.Parse(configuration["RabbitMQ:QueueAutoAck"] ?? "true");
        _methodName = configuration["RabbitMQ:MethodName"] ?? "ReceiveAlert";
        InitializeRabbitMqListener().GetAwaiter().GetResult();
    }

    public void Start()
    {
        var consumer = new AsyncEventingBasicConsumer(_channel);

        consumer.ReceivedAsync += async (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);
            await _hubContext.Clients.All.SendAsync(_methodName, message);
        };

        _channel.BasicConsumeAsync(_queueName,
            _queueAutoAck,
            consumer);
    }

    private async Task InitializeRabbitMqListener()
    {
        var factory = new ConnectionFactory
        {
            HostName = _hostName,
            UserName = _userName,
            Password = _password
        };

        _connection = await factory.CreateConnectionAsync();
        _channel = await _connection.CreateChannelAsync();

        await _channel.QueueDeclareAsync(_queueName,
            _queueDurable,
            _queueExclusive,
            _queueAutoDelete);
    }
}