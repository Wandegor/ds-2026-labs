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
    private readonly IDictionary<string, IConnectionMultiplexer> _redisConnections;

    public SummaryModel(
        ILogger<SummaryModel> logger,
        IDictionary<string, IConnectionMultiplexer> redisConnections)
    {
        _logger = logger;
        _redisConnections = redisConnections;
    }

    public string Id { get; set; }
    public double? Rank { get; set; }
    public double Similarity { get; set; }

    public void OnGet(string id)
    {
        Id = id;    
        _logger.LogDebug(id);

        IDatabase mainDb = _redisConnections["MAIN"].GetDatabase();
        
        string? region = mainDb.StringGet(id);
        
        if (string.IsNullOrEmpty(region))
        {
            _logger.LogWarning($"Region for ID {id} not found.");
            return;
        }
        Console.WriteLine($"LOOKUP: {id}, {region}");

        IDatabase shardDb = _redisConnections[region].GetDatabase();
            
        string? rankValue = shardDb.StringGet($"RANK-{id}");
        if (double.TryParse(rankValue, 
                NumberStyles.Float, 
                CultureInfo.InvariantCulture, 
                out double r))
            Rank = r;
        else
            Rank = null;
        
        string? similitaryValue =shardDb.StringGet($"SIMILARITY-{id}");
        if (double.TryParse(similitaryValue,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out double sim))
        {
            Similarity = sim;
        }
    }
}
