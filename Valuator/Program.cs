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
        
        // Environment Variables
        var mainConnString = builder.Configuration["DB_MAIN"] ?? "localhost:6379";
        var ruConnString   = builder.Configuration["DB_RU"]   ?? "localhost:6380";
        var euConnString   = builder.Configuration["DB_EU"]   ?? "localhost:6381";
        var asiaConnString = builder.Configuration["DB_ASIA"] ?? "localhost:6382";
        
        var connections = new Dictionary<string, IConnectionMultiplexer>
        {
            ["MAIN"] = ConnectionMultiplexer.Connect(mainConnString),
            ["RU"]   = ConnectionMultiplexer.Connect(ruConnString),
            ["EU"]   = ConnectionMultiplexer.Connect(euConnString),
            ["ASIA"] = ConnectionMultiplexer.Connect(asiaConnString)
        };
        
        // var redisConnectionString = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
        // var redis = ConnectionMultiplexer.Connect(redisConnectionString);
        
        // Словарь подключений
        builder.Services.AddSingleton<IDictionary<string, IConnectionMultiplexer>>(connections);
        
        var mainRedis = connections["MAIN"];
        
        // Antiforgery токен будет хранится и доставаться не где то на диске, а в бд
         // app1 генерит токен, сохраняет ключ в Redis (или использует уже существующий общий ключ).
         // app2 проверяет токен, обращается к  Redis, находит нужный ключ и расшифровывает токен.
        builder.Services.AddDataProtection().PersistKeysToStackExchangeRedis(mainRedis, "DataProtection-Keys");
        
        builder.Services.AddSignalR()
            .AddStackExchangeRedis(mainConnString, options =>
            {
                options.Configuration.ChannelPrefix = RedisChannel.Literal("ValuatorSignalR");
            });
        
        // RabbitMQ
        builder.Services.AddSingleton<ConnectionFactory>(sp => new ConnectionFactory
        {
            HostName = builder.Configuration["RabbitMQ_Host"] ?? "localhost"
        });
        
        builder.Services.AddSingleton<IConnection>(sp =>
        {
            // ожидание запуска Rabbit
            var factory = sp.GetRequiredService<ConnectionFactory>();
            IConnection? connection = null;
            while (connection == null)
            {
                try
                {
                    connection = factory.CreateConnectionAsync().GetAwaiter().GetResult();
                }
                catch
                {
                    Console.WriteLine("RabbitMQ not ready, waiting 5 seconds...");
                    Task.Delay(5000).Wait();
                }
            }
            return connection;
        });
        
        builder.Services.AddHostedService<RankEventBackgroundService>();
        
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
        
        app.Run();
    }
}
