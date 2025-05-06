using System.Management;
using Microsoft.Extensions.Configuration;
using WebSystemDesign_F.Models;
using WebSystemDesign_F.RabbitMQ.Producer;

namespace WebSystemDesign_F;

public class Collector
{
    public static async Task Main(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", true, true)
            .AddEnvironmentVariables()
            .Build();

        IRabbitMqProducer producer = new RabbitMqProducer(configuration);

        Thread.Sleep(1000);

        while (true)
        {
            var cpuQuery =
                new ObjectQuery(
                    "SELECT PercentProcessorTime FROM Win32_PerfFormattedData_PerfOS_Processor WHERE Name='_Total'");
            var cpuSearcher = new ManagementObjectSearcher(cpuQuery);
            var cpuResult = cpuSearcher.Get().Cast<ManagementObject>().FirstOrDefault();
            var rawCpuValue = cpuResult["PercentProcessorTime"];
            var cpuUsage = Convert.ToUInt32(rawCpuValue);

            var memoryQuery =
                new ObjectQuery("SELECT TotalVisibleMemorySize, FreePhysicalMemory FROM Win32_OperatingSystem");
            var memorySearcher = new ManagementObjectSearcher(memoryQuery);
            var memoryResult = memorySearcher.Get().Cast<ManagementObject>().FirstOrDefault();

            if (memoryResult != null)
            {
                var totalMemoryKB = (ulong)memoryResult["TotalVisibleMemorySize"];
                var freeMemoryKB = (ulong)memoryResult["FreePhysicalMemory"];
                var usedMemoryKB = totalMemoryKB - freeMemoryKB;

                Console.WriteLine($"CPU Usage: {cpuUsage}%");
                Console.WriteLine($"Total Memory: {totalMemoryKB / 1024} MB");
                Console.WriteLine($"Available Memory: {freeMemoryKB / 1024} MB");
                Console.WriteLine($"Used Memory: {usedMemoryKB / 1024} MB");

                var serverStatistics = new ServerStatistics
                {
                    AvailableMemory = freeMemoryKB,
                    CpuUsage = cpuUsage,
                    MemoryUsage = usedMemoryKB,
                    Timestamp = DateTime.Now
                };
                try
                {
                    await producer.SendServerStatisticsMessage(serverStatistics);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error sending message: {ex}");
                }
            }

            Thread.Sleep(1000);
        }
    }
}