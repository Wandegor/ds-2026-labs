using System;
using System.Collections.Generic;
using System.Globalization;
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

    public string Id { get; set; }
    public double? Rank { get; set; }
    public double Similarity { get; set; }

    public void OnGet(string id)
    {
        Id = id;    
        _logger.LogDebug(id);

        IDatabase db = _redis.GetDatabase();
        // (pa1) проинициализировать свойства Rank и Similarity значениями из БД (Redis)
        
        string? rankValue = db.StringGet($"RANK-{id}");
        if (double.TryParse(rankValue, 
                NumberStyles.Float, 
                CultureInfo.InvariantCulture, 
                out double r))
            Rank = r;
        else
            Rank = null;
        
        string? similitaryValue = db.StringGet($"SIMILARITY-{id}");
        if (double.TryParse(similitaryValue,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out double sim))
        {
            Similarity = sim;
        }
    }
}
