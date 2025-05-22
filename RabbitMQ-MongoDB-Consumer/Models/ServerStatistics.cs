namespace RabbitMQ_MongoDB_Consumer.Models;

public class ServerStatistics
{
    public string ServerIdentifier { get; set; }
    public double MemoryUsage { get; set; }
    public double AvailableMemory { get; set; }
    public double CpuUsage { get; set; }
    public DateTime Timestamp { get; set; }
}