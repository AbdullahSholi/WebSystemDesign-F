namespace RabbitMQ_MongoDB_Consumer;

public interface IMessageQueue
{
    public Task StartConsumingAsync();
}