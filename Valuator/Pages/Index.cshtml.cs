using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using StackExchange.Redis;

namespace Valuator.Pages;

public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly IConnectionMultiplexer _redis;

    public IndexModel(ILogger<IndexModel> logger,  IConnectionMultiplexer redis)
    {
        _logger = logger;
        _redis = redis;
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
    
    public IActionResult OnPost(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Page();
        }
        
        // TODO: масиив с уникальными текстами
        _logger.LogDebug(text);
        
        IDatabase db = _redis.GetDatabase();
        string id = Guid.NewGuid().ToString();
        
        
        
        // TODO: (pa1) посчитать similarity и сохранить в БД (Redis) по ключу similarityKey
        
        double similarity = 0.0f;
        string setName = "SetWithTexts";
        if (!db.SetAdd(setName, ComputeHash(text))) // повторка
        {
            similarity = 1.0;
        }
        
        string similarityKey = "SIMILARITY-" + id;
        db.StringSet(similarityKey, similarity);
        
        // TODO: (pa1) посчитать rank и сохранить в БД (Redis) по ключу rankKey
        string rankKey = "RANK-" + id;
        double rank = 0;
        foreach (char ch in text)
        {
            if (!char.IsLetter(ch))
            {
                rank++;
            }
        }
        rank /= text.Length;
        string rankString = Math.Round(rank, 4)
            .ToString(System.Globalization.CultureInfo.InvariantCulture);
        
        db.StringSet(rankKey, rankString);

        return Redirect($"summary?id={id}");
    }
}
