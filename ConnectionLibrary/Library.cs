using StackExchange.Redis;

namespace ClassLibrary1;

public class Library
{
    private static readonly Dictionary<string, IConnectionMultiplexer> _redisConnections = new();

    public static async Task InitDataBases()
    {
        string mainAddr = Environment.GetEnvironmentVariable("DB_MAIN") ?? "localhost:6379";
        string ruAddr   = Environment.GetEnvironmentVariable("DB_RU")   ?? "localhost:6380";
        string euAddr   = Environment.GetEnvironmentVariable("DB_EU")   ?? "localhost:6381";
        string asiaAddr = Environment.GetEnvironmentVariable("DB_ASIA") ?? "localhost:6382";
        
        _redisConnections["MAIN"] = await ConnectionMultiplexer.ConnectAsync(mainAddr);
        _redisConnections["RU"]   = await ConnectionMultiplexer.ConnectAsync(ruAddr);
        _redisConnections["EU"]   = await ConnectionMultiplexer.ConnectAsync(euAddr);
        _redisConnections["ASIA"] = await ConnectionMultiplexer.ConnectAsync(asiaAddr);
    }
    
    public static IDatabase GetDatabase(string region)
    {
        if (!_redisConnections.TryGetValue(region, out var db))
        {
            throw new ArgumentException($"No Redis connection for region '{region}'");
        }
        return db.GetDatabase();
    }
}