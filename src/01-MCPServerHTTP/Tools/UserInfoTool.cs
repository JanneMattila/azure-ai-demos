using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Security.Claims;

[McpServerToolType]
public class UserInfoTool
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<UserInfoTool> _logger;

    public UserInfoTool(IHttpContextAccessor httpContextAccessor, ILogger<UserInfoTool> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    [McpServerTool]
    [Description("Gets information about the currently authenticated user")]
    public string GetCurrentUserInfo()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        
        if (user?.Identity?.IsAuthenticated != true)
        {
            return "No authenticated user";
        }

        var userInfo = new Dictionary<string, string?>
        {
            ["Authentication Type"] = user.Identity.AuthenticationType,
            ["Name"] = user.Identity.Name,
            ["User Principal Name"] = user.FindFirst("upn")?.Value,
            ["Email"] = user.FindFirst(ClaimTypes.Email)?.Value ?? user.FindFirst("preferred_username")?.Value,
            ["Object ID (oid)"] = user.FindFirst("oid")?.Value,
            ["Subject (sub)"] = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst("sub")?.Value,
            ["Tenant ID (tid)"] = user.FindFirst("tid")?.Value,
            ["App ID (appid)"] = user.FindFirst("appid")?.Value,
            ["Roles"] = string.Join(", ", user.FindAll(ClaimTypes.Role).Select(c => c.Value)),
            ["Scopes"] = user.FindFirst("scp")?.Value
        };

        return string.Join("\n", userInfo.Where(kvp => !string.IsNullOrEmpty(kvp.Value))
            .Select(kvp => $"{kvp.Key}: {kvp.Value}"));
    }

    [McpServerTool]
    [Description("Checks if the current user has a specific role")]
    public bool HasRole([Description("Role name to check")] string roleName)
    {
        var user = _httpContextAccessor.HttpContext?.User;
        
        if (user?.Identity?.IsAuthenticated != true)
        {
            _logger.LogWarning("Unauthenticated user attempting to check role: {RoleName}", roleName);
            return false;
        }

        var hasRole = user.IsInRole(roleName);
        var userId = user.FindFirst("oid")?.Value ?? user.Identity.Name ?? "Unknown";
        
        _logger.LogInformation("User {UserId} role check for '{RoleName}': {HasRole}", 
            userId, roleName, hasRole);
        
        return hasRole;
    }

    [McpServerTool]
    [Description("Gets all claims for the current user (for debugging)")]
    public string GetAllClaims()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        
        if (user?.Identity?.IsAuthenticated != true)
        {
            return "No authenticated user";
        }

        var claims = user.Claims
            .Select(c => $"{c.Type}: {c.Value}")
            .OrderBy(c => c);

        return string.Join("\n", claims);
    }

    [McpServerTool]
    [Description("Performs an action with audit logging")]
    public string PerformAuditedAction(
        [Description("Action description")] string action)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        var user = httpContext?.User;
        
        if (user?.Identity?.IsAuthenticated != true)
        {
            _logger.LogWarning("Unauthenticated attempt to perform action: {Action}", action);
            return "Authentication required";
        }

        var userId = user.FindFirst("oid")?.Value ?? user.Identity.Name ?? "Unknown";
        var userEmail = user.FindFirst("preferred_username")?.Value ?? "No email";
        var ipAddress = httpContext?.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        var timestamp = DateTimeOffset.UtcNow;

        // Log the action with user context
        _logger.LogInformation(
            "User {UserId} ({Email}) performed action '{Action}' from IP {IpAddress} at {Timestamp}",
            userId, userEmail, action, ipAddress, timestamp);

        return $"Action '{action}' performed successfully by {userEmail} at {timestamp:yyyy-MM-dd HH:mm:ss} UTC";
    }
}
