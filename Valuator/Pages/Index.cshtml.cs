using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using StackExchange.Redis;

namespace Valuator.Pages;

public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(ILogger<IndexModel> logger)
    {
        _logger = logger;
    }

    public void OnGet()
    {

    }

    public IActionResult OnPost(string text)
    {
        _logger.LogDebug(text);
        
        ConnectionMultiplexer redis = ConnectionMultiplexer.Connect("localhost:6379");
        IDatabase db = redis.GetDatabase();
        
        string id = Guid.NewGuid().ToString();

        string textKey = "TEXT-" + id;
        db.StringSet(textKey, text);
        
        string rankKey = "RANK-" + id;
        
        // TODO: (pa1) посчитать rank и сохранить в БД (Redis) по ключу rankKey
        double rank = 0;
        foreach (char ch in text)
        {
            if (!char.IsLetter(ch))
            {
                rank++;
            }
        }
        rank /= text.Length;
        
        db.StringSet(rankKey, rank);

        
        string similarityKey = "SIMILARITY-" + id;
        // TODO: (pa1) посчитать similarity и сохранить в БД (Redis) по ключу similarityKey
        

        return Redirect($"summary?id={id}");
    }
}
