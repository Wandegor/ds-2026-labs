using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using StackExchange.Redis;

namespace Valuator.Pages;

public class RegisterModel : PageModel
{
    private readonly IDatabase _mainDb;
    
    [BindProperty]
    public string Username { get; set; } = string.Empty;

    [BindProperty]
    public string Password { get; set; } = string.Empty;
    
    public RegisterModel(IDictionary<string, IConnectionMultiplexer> redisConnections)
    {
        _mainDb = redisConnections["MAIN"].GetDatabase();
    }
    
    public void OnGet()
    {
        
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
        {
            Console.WriteLine("Login or Password can not be null.");
            return Page();
        }
        
        string userKey = $"USER:{Username.Trim()}";

        if (await _mainDb.KeyExistsAsync(userKey))
        {
            Console.WriteLine("Username with that login already exists.");
            return Page();
        }
        
        // Хеширование пароля
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(Password));
        string passwordHash = Convert.ToBase64String(bytes);

        await _mainDb.StringSetAsync(userKey, passwordHash);
        
        return RedirectToPage("/Login");
    }
}