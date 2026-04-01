using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Valuator.Pages;
public class SummaryModel : PageModel
{
    private readonly ILogger<SummaryModel> _logger;
    private readonly IConnectionMultiplexer _redis;

    public SummaryModel(ILogger<SummaryModel> logger, IConnectionMultiplexer redis)
    {
        _logger = logger;
        _redis = redis;
    }

    public double Rank { get; set; }
    public double Similarity { get; set; }

    public void OnGet(string id)
    {
        _logger.LogDebug(id);

        IDatabase db = _redis.GetDatabase();
        // (pa1) проинициализировать свойства Rank и Similarity значениями из БД (Redis)
        
        string? rankValue = db.StringGet($"RANK-{id}");
        if (!string.IsNullOrEmpty(rankValue))
        {
            if (double.TryParse(rankValue,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out double rank))
            {
                Rank = rank;
            }
        }
        else
        {
            ViewData["Status"] = "Оценка содержания не завершена";
        }
        
        string? similitaryValue = db.StringGet($"SIMILARITY-{id}");
        if (!string.IsNullOrEmpty(similitaryValue))
        {
            if (double.TryParse(similitaryValue,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out double similitary))
            {
                Similarity = similitary;
            }
        }
    }
}
