using ClassLibrary1;
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
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

        builder.Services.AddRazorPages();
        
        // Environment Variables из Docker(так-то из всех источников конфигурации, но именно здесь из Docker)
        string mainConnString = builder.Configuration["DB_MAIN"] ?? "localhost:6379";
        string ruConnString   = builder.Configuration["DB_RU"]   ?? "localhost:6380";
        string euConnString   = builder.Configuration["DB_EU"]   ?? "localhost:6381";
        string asiaConnString = builder.Configuration["DB_ASIA"] ?? "localhost:6382";

        Library.InitDataBases();
        
        // Cловарь подключений (постоянные TCP-сессии)
        Dictionary<string, IConnectionMultiplexer> connections = new()
        {
            ["MAIN"] = ConnectionMultiplexer.Connect(mainConnString),
            ["RU"]   = ConnectionMultiplexer.Connect(ruConnString),
            ["EU"]   = ConnectionMultiplexer.Connect(euConnString),
            ["ASIA"] = ConnectionMultiplexer.Connect(asiaConnString)
        };
        
        // var redisConnectionString = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
        // var redis = ConnectionMultiplexer.Connect(redisConnectionString);
        
        // Словарь подключений регестрируется как Singlton
        builder.Services.AddSingleton<IDictionary<string, IConnectionMultiplexer>>(connections);
        
        IConnectionMultiplexer mainRedis = connections["MAIN"];
        
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
            ConnectionFactory factory = sp.GetRequiredService<ConnectionFactory>();
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
        
        WebApplication app = builder.Build();

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
