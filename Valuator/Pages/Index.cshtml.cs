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

    public IActionResult OnPost(string text)
    {
        _logger.LogDebug(text);
        
        IDatabase db = _redis.GetDatabase();
        string id = Guid.NewGuid().ToString();
        
        // Для поиска
        // INDEX-text:попытка номер три → {id1, id2, ...} 
        string textIndex = "INDEX-text:" + text;
        RedisValue[] matches = db.SetMembers(textIndex);
        
        // TODO: (pa1) посчитать similarity и сохранить в БД (Redis) по ключу similarityKey
        string similarityKey = "SIMILARITY-" + id;
        double similarity = matches.Length > 0 ? 1.0 : 0.0;
        db.StringSet(similarityKey, similarity);
        
        string textKey = "TEXT-" + id;
        db.StringSet(textKey, text);
        db.SetAdd(textIndex, id);
        
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
