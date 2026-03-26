using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Security.Claims;

[McpServerToolType]
public class RandomNumberTools
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<RandomNumberTools> _logger;

    public RandomNumberTools(IHttpContextAccessor httpContextAccessor, ILogger<RandomNumberTools> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    [McpServerTool]
    [Description("Generates a random number between the specified minimum and maximum values.")]
    public string GetRandomNumber(
        [Description("Minimum value (inclusive)")] int min = 0,
        [Description("Maximum value (exclusive)")] int max = 100)
    {
        var randomNumber = Random.Shared.Next(min, max);
        
        // Access user information from the authenticated request
        var httpContext = _httpContextAccessor.HttpContext;
        var user = httpContext?.User;
        
        if (user?.Identity?.IsAuthenticated == true)
        {
            var userName = user.Identity.Name ?? "Unknown";
            var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                ?? user.FindFirst("oid")?.Value  // Object ID from Azure AD
                ?? user.FindFirst("sub")?.Value; // Subject claim
            
            var email = user.FindFirst(ClaimTypes.Email)?.Value 
                ?? user.FindFirst("preferred_username")?.Value;
            
            // You can also get all claims
            var allClaims = string.Join(", ", user.Claims.Select(c => $"{c.Type}={c.Value}"));

            _logger.LogInformation("Generated random number {RandomNumber} for user {UserName} (ID: {UserId}, Email: {Email})",
                randomNumber, userName, userId, email);
            _logger.LogInformation("User claims: {AllClaims}", allClaims);

            return $"Random number: {randomNumber}\n" +
                   $"Generated for user: {userName}\n" +
                   $"User ID: {userId}\n" +
                   $"Email: {email}";
        }
        else
        {
            _logger.LogInformation("Generated random number {RandomNumber} for unauthenticated user",
                randomNumber);
        }

        return $"Random number: {randomNumber} (Anonymous user)";
    }
}
