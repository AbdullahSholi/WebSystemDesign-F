namespace WebSystemDesign_F.Producer;

public interface IMessageQueue
{
    public Task SendServerStatisticsMessage<T>(T serverStatisticsMessage);
}