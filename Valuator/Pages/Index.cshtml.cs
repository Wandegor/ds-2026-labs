using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RabbitMQ.Client;
using StackExchange.Redis;

namespace Valuator.Pages;

public class IndexModel : PageModel
{
    private const string ExchangeName = "valuator.processing.rank";
    private const string QueueName = "valuator.processing.rank";
    
    private readonly ILogger<IndexModel> _logger;
    private readonly IConnectionMultiplexer _redis;
    private readonly ConnectionFactory _rabbitFactory;

    public IndexModel(ILogger<IndexModel> logger,  IConnectionMultiplexer redis, ConnectionFactory rabbitFactory)
    {
        _logger = logger;
        _redis = redis;
        _rabbitFactory = rabbitFactory;
    }

    public void OnGet()
    {

    }

    private static string ComputeHash(string input)
    {
        using var sha256 = SHA256.Create();
        byte[] bytes = Encoding.UTF8.GetBytes(input);
        byte[] hashBytes = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hashBytes);
    }
    
    public async Task<IActionResult> OnPostAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Page();
        }
        
        _logger.LogDebug(text);
        
        IDatabase db = _redis.GetDatabase();
        string id = Guid.NewGuid().ToString();
        
        // вернул сохранение самого текста
        db.StringSet($"TEXT-{id}", text);
        
        // (pa1) посчитать similarity и сохранить в БД (Redis) по ключу similarityKey
        double similarity = 0.0f;
        string setName = "SetWithTexts";
        if (!db.SetAdd(setName, ComputeHash(text))) // повторка
        {
            similarity = 1.0;
        }
        
        string similarityKey = "SIMILARITY-" + id;
        db.StringSet(similarityKey, similarity);
        
        // (pa3) посчитать rank в RankCalculator при помощи RabbitMQ
        await PublishRankTaskAsync(id);
        
        // string rankKey = "RANK-" + id;
        // double rank = 0;
        // foreach (char ch in text)
        // {
        //     if (!char.IsLetter(ch))
        //     {
        //         rank++;
        //     }
        // }
        // rank /= text.Length;
        // string rankString = Math.Round(rank, 4)
        //     .ToString(System.Globalization.CultureInfo.InvariantCulture);
        //
        // db.StringSet(rankKey, rankString);

        return Redirect($"summary?id={id}");
    }
    
    private async Task PublishRankTaskAsync(string id)
    {
        await using var connection = await _rabbitFactory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();
        
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
}
