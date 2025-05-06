namespace WebSystemDesign_F;

public interface IMessageQueue
{
    public Task SendServerStatisticsMessage<T>(T serverStatisticsMessage);
}