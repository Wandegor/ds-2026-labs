using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using StackExchange.Redis;

namespace Valuator.Pages;

public class LoginModel : PageModel
{
    private readonly IDatabase _mainDb;
    
    [BindProperty]
    public string Username { get; set; } = string.Empty;

    [BindProperty]
    public string Password { get; set; } = string.Empty;
    
    public LoginModel(IDictionary<string, IConnectionMultiplexer> redisConnections)
    {
        _mainDb = redisConnections["MAIN"].GetDatabase();
    }
    
    public void OnGet()
    {
        
    }

    public async Task<IActionResult> OnPostAsync()
    {
        string userKey = $"USER:{Username.Trim()}";
        string? storedPassHash = await _mainDb.StringGetAsync(userKey);

        if (storedPassHash == null)
        {
            Console.WriteLine("No password for this login.");
            return Page();
        }

        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(Password));
        string inputPassHash = Convert.ToBase64String(bytes);

        if (inputPassHash != storedPassHash)
        {
            Console.WriteLine("Password is not correct");
            return Page();
        }
        
        List<Claim> claims = [new(ClaimTypes.Name, Username.Trim())];
        ClaimsIdentity claimsIdentity = new(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        
        // Выписываем шифрованную Куку пользователю
        await HttpContext.SignInAsync(new ClaimsPrincipal(claimsIdentity));

        return RedirectToPage("/Index");
    }
}