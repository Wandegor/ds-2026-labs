using Microsoft.AspNetCore.DataProtection;
using StackExchange.Redis;

namespace Valuator;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddRazorPages();
        var redisConnectionString = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
        var redis = ConnectionMultiplexer.Connect(redisConnectionString);
        
        // Antiforgery токен будет хранится и доставаться не где то на диске, а в бд
         // app1 генерит токен, сохраняет ключ в Redis (или использует уже существующий общий ключ).
         // app2 проверяет токен, обращается к  Redis, находит нужный ключ и расшифровывает токен.
        builder.Services.AddDataProtection().PersistKeysToStackExchangeRedis(redis, "DataProtection-Keys");
        builder.Services.AddSingleton<IConnectionMultiplexer>(redis);

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
        }
        app.UseStaticFiles();

        app.UseRouting();

        app.UseAuthorization();

        app.MapRazorPages();

        app.Run();
    }
}
