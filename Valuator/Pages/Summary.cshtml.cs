using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using Valuator.Models;

namespace Valuator.Pages;

// [Authorize] смотрит HttpContext.User
// пусто - редирект /Login, иначе распознал ClaimsPrincipal
[Authorize]
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

    public IActionResult OnGet(string id)
    {
        Id = id;    
        _logger.LogDebug(id);

        IDatabase mainDb = _redisConnections["MAIN"].GetDatabase();
        
        string? jsonMetaData = mainDb.StringGet(id);
        
        if (string.IsNullOrEmpty(jsonMetaData))
        {
            _logger.LogWarning($"Metadata for ID {id} not found.");
            return RedirectToPage("/Index");
        }
        
        DocumentMetadata? metadata = JsonSerializer.Deserialize<DocumentMetadata>(jsonMetaData);
        if (metadata == null) return RedirectToPage("/Index");
        
        Console.WriteLine($"LOOKUP: {id}, {metadata.DbRegion}");
        
        // User.Identity.Name - имя из куки
        if (metadata.Author != User.Identity?.Name)
        {
            return Forbid(); // 403
        }
        IDatabase shardDb = _redisConnections[metadata.DbRegion].GetDatabase();
            
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
        
        return Page(); // рендер
    }
}
