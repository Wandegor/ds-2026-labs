using ClassLibrary1;
using Microsoft.AspNetCore.DataProtection;
using RabbitMQ.Client;
using StackExchange.Redis;
using Valuator.Hubs;
using Valuator.Services;            

namespace Valuator;

public class Program
{
    public static async Task Main(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

        builder.Services.AddRazorPages();
        
        await Library.InitDataBases();
        
        // Cловарь подключений (постоянные TCP-сессии)
        Dictionary<string, IConnectionMultiplexer> connections = new()
        {
            ["MAIN"] = Library.GetConnection("MAIN"),
            ["RU"]   = Library.GetConnection("RU"),
            ["EU"]   = Library.GetConnection("EU"),
            ["ASIA"] = Library.GetConnection("ASIA")
        };
        
        // Словарь подключений регестрируется как Singlton
        builder.Services.AddSingleton<IDictionary<string, IConnectionMultiplexer>>(connections);
        
        IConnectionMultiplexer mainRedis = connections["MAIN"];
        
        // Antiforgery токен будет хранится и доставаться не где то на диске, а в бд
         // app1 генерит токен, сохраняет ключ в Redis (или использует уже существующий общий ключ).
         // app2 проверяет токен, обращается к  Redis, находит нужный ключ и расшифровывает токен.
        builder.Services.AddDataProtection().PersistKeysToStackExchangeRedis(mainRedis, "DataProtection-Keys");
        
        string mainConnString = builder.Configuration["DB_MAIN"] ?? "localhost:6379";
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
