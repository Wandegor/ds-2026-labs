using Microsoft.AspNetCore.DataProtection;
using RabbitMQ.Client;
using StackExchange.Redis;
using Valuator.Hubs;
using Valuator.Services;

namespace Valuator;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddRazorPages();
        var redisConnectionString = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
        var redis = ConnectionMultiplexer.Connect(redisConnectionString);
        
        // Antiforgery токен будет хранится и доставаться не где то на диске, а в бд
         // app1 генерит токен, сохраняет ключ в Redis (или использует уже существующий общий ключ).
         // app2 проверяет токен, обращается к  Redis, находит нужный ключ и расшифровывает токен.
        builder.Services.AddDataProtection().PersistKeysToStackExchangeRedis(redis, "DataProtection-Keys");
        builder.Services.AddSingleton<IConnectionMultiplexer>(redis);

        // RabbitMQ
        builder.Services.AddSingleton<ConnectionFactory>(sp => new ConnectionFactory
        {
            HostName = builder.Configuration.GetSection("RabbitMQ")["Host"] ?? "localhost" 
        });
        
        builder.Services.AddSingleton<IConnection>(sp =>
        {
            var factory = sp.GetRequiredService<ConnectionFactory>();
            return factory.CreateConnectionAsync().GetAwaiter().GetResult();
        });
        
        // builder.Services.AddHostedService<RankEventBackgroundService>();
        //
        // builder.Services.AddSignalR()
        //     .AddStackExchangeRedis(redisConnectionString, options =>
        //     {
        //         options.Configuration.ChannelPrefix = RedisChannel.Literal("ValuatorSignalR");
        //     });
        var app = builder.Build();

        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
        }
        app.UseStaticFiles();

        app.UseRouting();

        app.UseAuthorization();

        app.MapRazorPages();
        
        app.MapHub<RankNotificationHub>("/rankHub");

        // Закрытие Rabbit соединения
        var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
        var rabbitConnection = app.Services.GetRequiredService<IConnection>();
        lifetime.ApplicationStopping.Register(() =>
        {
            rabbitConnection.CloseAsync().Wait();
            rabbitConnection.Dispose();
        });
        
        app.Run();
    }
}
