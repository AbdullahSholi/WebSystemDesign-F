using System.Management;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Configuration;
using WebSystemDesign_F.Models;
using WebSystemDesign_F.Producer;

namespace WebSystemDesign_F;

public class Collector
{
    private static ulong _prevIdleTime = 0;
    private static ulong _prevTotalTime = 0;

    public static async Task Main(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", true, true)
            .AddEnvironmentVariables()
            .Build();

        IRabbitMqProducer producer = new RabbitMqProducer(configuration);

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            _ = GetCpuUsageLinux();
            await Task.Delay(1000);
        }

        while (true)
        {
            ServerStatistics serverStatistics;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                serverStatistics = GetStatisticsWindows();
            else
                serverStatistics = GetStatisticsLinux();

            Console.WriteLine($"CPU Usage: {serverStatistics.CpuUsage:F2}%");
            Console.WriteLine($"Available Memory: {serverStatistics.AvailableMemory / 1024} MB");
            Console.WriteLine($"Used Memory: {serverStatistics.MemoryUsage / 1024} MB");
            Console.WriteLine($"Timestamp: {serverStatistics.Timestamp}");

            try
            {
                await producer.SendServerStatisticsMessage(serverStatistics);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending message: {ex}");
            }

            await Task.Delay(1000);
        }
    }

    private static ServerStatistics GetStatisticsWindows()
    {
        var cpuQuery = new ObjectQuery(
            "SELECT PercentProcessorTime FROM Win32_PerfFormattedData_PerfOS_Processor WHERE Name='_Total'");
        var cpuSearcher = new ManagementObjectSearcher(cpuQuery);
        var cpuResult = cpuSearcher.Get().Cast<ManagementObject>().FirstOrDefault();
        var rawCpuValue = cpuResult?["PercentProcessorTime"];
        var cpuUsage = Convert.ToUInt32(rawCpuValue ?? 0);

        var memoryQuery =
            new ObjectQuery("SELECT FreePhysicalMemory, TotalVisibleMemorySize FROM Win32_OperatingSystem");
        var memorySearcher = new ManagementObjectSearcher(memoryQuery);
        var memoryResult = memorySearcher.Get().Cast<ManagementObject>().FirstOrDefault();

        ulong freeMemoryKb = 0;
        ulong usedMemoryKb = 0;

        if (memoryResult != null)
        {
            freeMemoryKb = (ulong)memoryResult["FreePhysicalMemory"];
            var totalMemoryKb = (ulong)memoryResult["TotalVisibleMemorySize"];
            usedMemoryKb = totalMemoryKb - freeMemoryKb;
        }
        
        return new ServerStatistics
        {
            CpuUsage = cpuUsage,
            AvailableMemory = freeMemoryKb,
            MemoryUsage = usedMemoryKb,
            Timestamp = DateTime.Now
        };
    }

    private static ServerStatistics GetStatisticsLinux()
    {
        var cpuUsage = GetCpuUsageLinux();

        var (memTotal, memAvailable) = GetMemoryInfoLinux();
        var memUsed = memTotal - memAvailable;

        return new ServerStatistics
        {
            CpuUsage = cpuUsage,
            AvailableMemory = memAvailable,
            MemoryUsage = memUsed,
            Timestamp = DateTime.Now
        };
    }

    private static float GetCpuUsageLinux()
    {
        string[] cpuLines = File.ReadAllLines("/proc/stat");
        var cpuLine = cpuLines[0];

        var parts = cpuLine.Split([' '], StringSplitOptions.RemoveEmptyEntries);

        var user = ulong.Parse(parts[1]);
        var nice = ulong.Parse(parts[2]);
        var system = ulong.Parse(parts[3]);
        var idle = ulong.Parse(parts[4]);
        var iowait = ulong.Parse(parts[5]);
        var irq = ulong.Parse(parts[6]);
        var softirq = ulong.Parse(parts[7]);
        var steal = ulong.Parse(parts[8]);

        var idleTime = idle + iowait;
        var totalTime = user + nice + system + idle + iowait + irq + softirq + steal;

        var diffIdle = idleTime - _prevIdleTime;
        var diffTotal = totalTime - _prevTotalTime;

        float cpuUsage = 0;
        if (diffTotal != 0) cpuUsage = (float)(diffTotal - diffIdle) * 100 / diffTotal;

        _prevIdleTime = idleTime;
        _prevTotalTime = totalTime;

        return cpuUsage;
    }

    private static (ulong total, ulong available) GetMemoryInfoLinux()
    {
        string[] memoryInfoLines = File.ReadAllLines("/proc/meminfo");

        ulong memTotal = 0;
        ulong memAvailable = 0;

        foreach (var line in memoryInfoLines)
            if (line.StartsWith("MemTotal:"))
                memTotal = ParseMemoryInfoLine(line);
            else if (line.StartsWith("MemAvailable:")) memAvailable = ParseMemoryInfoLine(line);

        return (memTotal, memAvailable);
    }

    private static ulong ParseMemoryInfoLine(string line)
    {
        var parts = line.Split([' '], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2 && ulong.TryParse(parts[1], out var valueKb)) return valueKb;
        return 0;
    }
}