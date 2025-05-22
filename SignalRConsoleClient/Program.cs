using Microsoft.AspNetCore.SignalR.Client;
using SignalRConsoleClient;

var alertHubUrl = "http://signalr-server:8080/alerthub";
var connection = new HubConnectionBuilder()
    .WithUrl(alertHubUrl)
    .WithAutomaticReconnect()
    .Build();

connection.On<object>(CustomMessages.MethodName, alert =>
{
    Console.WriteLine(CustomMessages.AlertReceive);
    Console.WriteLine(alert);
});

try
{
    await connection.StartAsync();
    Console.WriteLine(CustomMessages.ConnectedToHubServer);
}
catch (Exception ex)
{
    Console.WriteLine($"{CustomMessages.FailedToConnect} {ex.Message}");
}

Console.WriteLine(CustomMessages.ListeningForAlerts);
await Task.Delay(Timeout.Infinite);
await connection.StopAsync();