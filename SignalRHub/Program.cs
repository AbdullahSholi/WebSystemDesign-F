using SignalRHub;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()
            .SetIsOriginAllowed(_ => true);
    });
});

builder.Services.AddSingleton<RabbitMqListener>();
var app = builder.Build();

app.UseRouting();
app.UseCors();
app.MapControllers();
app.MapHub<AlertHub>("/alertHub");

var rabbitListener = app.Services.GetRequiredService<RabbitMqListener>();
rabbitListener.Start();

app.Run();