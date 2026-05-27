using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ClassLibrary1;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RabbitMQ.Client;
using StackExchange.Redis;

namespace Valuator.Pages;

public class IndexModel : PageModel
{
    private const string SimilarityExchange = "events.similarity.calculated"; 
    
    private const string ExchangeName = "valuator.processing.rank";
    private const string QueueName = "valuator.processing.rank";
    
    private readonly ILogger<IndexModel> _logger;
    // private readonly IDictionary<string, IConnectionMultiplexer> _redisConnections;
    private readonly IConnection _rabbitConnection;
    
    [BindProperty]
    public string Region { get; set; }

    public IndexModel(
        ILogger<IndexModel> logger,  
        IDictionary<string, IConnectionMultiplexer> redisConnections, 
        IConnection rabbitConnection)
    {
        _logger = logger;
        // _redisConnections = redisConnections;
        _rabbitConnection = rabbitConnection;
    }

    public void OnGet()
    {

    }
    
    public async Task<IActionResult> OnPostAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrEmpty(Region))
        {
            return Page();
        }
        
        _logger.LogDebug(text);

        IDatabase mainDb = Library.GetDatabase( "MAIN" );
        string id = Guid.NewGuid().ToString();
        
        Console.WriteLine($"LOOKUP: {id}, {Region}");
        
        // Id - Region
        mainDb.StringSet(id, Region);
        
        // Сам текст в в шард по ShardKey(Region)
        IDatabase shardDb = Library.GetDatabase(Region);
        shardDb.StringSet($"TEXT-{id}", text);
        
        // (pa1) посчитать similarity и сохранить в БД (Redis) по ключу similarityKey
        double similarity = 0.0f;
        string setName = "SetWithTexts";
        if (!shardDb.SetAdd(setName, ComputeHash(text))) // повторка
        {
            similarity = 1.0;
        }
        
        string similarityKey = "SIMILARITY-" + id;
        shardDb.StringSet(similarityKey, similarity);
        
        // (pa4) событие SimilarityCalculated
        await PublishSimilarityEventAsync(id, similarity);
        
        // (pa3) посчитать rank в RankCalculator при помощи RabbitMQ
        await PublishRankTaskAsync(id);

        return Redirect($"summary?id={id}");
    }
    
    private async Task PublishRankTaskAsync(string id)
    {
        await using var channel = await _rabbitConnection.CreateChannelAsync();
        
        await channel.ExchangeDeclareAsync(ExchangeName, ExchangeType.Direct);
        await channel.QueueDeclareAsync(QueueName, durable: true, exclusive: false, autoDelete: false);
        await channel.QueueBindAsync(QueueName, ExchangeName, routingKey: "");

        byte[] messageData = Encoding.UTF8.GetBytes(id);
        await channel.BasicPublishAsync(
            exchange: ExchangeName,
            routingKey: "",
            body: messageData
        );
    }
    
    private async Task PublishSimilarityEventAsync(string id, double similarity)
    {
        await using var channel = await _rabbitConnection.CreateChannelAsync();
    
        // Fanout exchange — сообщение получат все подписанные очереди
        await channel.ExchangeDeclareAsync(SimilarityExchange, ExchangeType.Fanout, durable: true);
    
        var eventData = new { Id = id, Similarity = similarity };
        string json = JsonSerializer.Serialize(eventData);
        byte[] messageData = Encoding.UTF8.GetBytes(json);
    
        await channel.BasicPublishAsync(
            exchange: SimilarityExchange,
            routingKey: "",
            body: messageData
        );
    }
    
    private static string ComputeHash(string input)
    {
        using var sha256 = SHA256.Create();
        byte[] bytes = Encoding.UTF8.GetBytes(input);
        byte[] hashBytes = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hashBytes);
    }
}
