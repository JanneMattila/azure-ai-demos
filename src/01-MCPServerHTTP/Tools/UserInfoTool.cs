using Microsoft.Graph;
using Microsoft.Identity.Web;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Security.Claims;

[McpServerToolType]
public class UserInfoTool
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<UserInfoTool> _logger;
    private readonly GraphServiceClient _graphServiceClient;
    private readonly ITokenAcquisition _tokenAcquisition;

    public UserInfoTool(
        IHttpContextAccessor httpContextAccessor,
        ILogger<UserInfoTool> logger,
        GraphServiceClient graphServiceClient,
        ITokenAcquisition tokenAcquisition)
    {
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
        _graphServiceClient = graphServiceClient;
        _tokenAcquisition = tokenAcquisition;
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

    [McpServerTool]
    [Description("Gets the current user's department from Microsoft Graph (uses On-Behalf-Of flow)")]
    public async Task<string> GetCurrentUserDepartment()
    {
        var user = _httpContextAccessor.HttpContext?.User;

        if (user?.Identity?.IsAuthenticated != true)
        {
            return "No authenticated user";
        }

        try
        {
            // --- On-Behalf-Of (OBO) flow ---
            // Microsoft.Identity.Web exchanges the incoming user assertion (the inbound JWT
            // bearer token presented to this API) for a new access token that is valid for
            // Microsoft Graph. This is the OAuth 2.0 On-Behalf-Of flow (RFC 7521 / RFC 8693).
            //
            // Equivalent raw HTTP REST call (what ITokenAcquisition performs under the hood):
            //
            //   POST https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/token
            //   Content-Type: application/x-www-form-urlencoded
            //
            //   grant_type=urn:ietf:params:oauth:grant-type:jwt-bearer
            //   &client_id={api-client-id}
            //   &client_secret={api-client-secret}            (or client_assertion for cert/MSI)
            //   &assertion={incoming-user-access-token}
            //   &scope=https://graph.microsoft.com/User.Read
            //   &requested_token_use=on_behalf_of
            //
            // Example response payload:
            // {
            //   "token_type": "Bearer",
            //   "scope": "User.Read",
            //   "expires_in": 3599,
            //   "ext_expires_in": 3599,
            //   "access_token": "eyJ0eXAiOiJKV1QiLC...",
            //   "refresh_token": "OAQABAAAAAA..."
            // }
            var graphAccessToken = await _tokenAcquisition.GetAccessTokenForUserAsync(
                new[] { "https://graph.microsoft.com/User.Read" });

            _logger.LogInformation("Acquired Microsoft Graph access token via OBO for user {User}",
                user.Identity.Name);

            // --- Microsoft Graph: get the signed-in user's profile ---
            // The GraphServiceClient call below is equivalent to this REST call:
            //
            //   GET https://graph.microsoft.com/v1.0/me?$select=displayName,department,jobTitle,mail,userPrincipalName
            //   Authorization: Bearer {graphAccessToken}
            //
            // Example response payload:
            // {
            //   "@odata.context": "https://graph.microsoft.com/v1.0/$metadata#users(displayName,department,jobTitle,mail,userPrincipalName)/$entity",
            //   "displayName": "Adele Vance",
            //   "department": "Retail",
            //   "jobTitle": "Retail Manager",
            //   "mail": "adelev@contoso.com",
            //   "userPrincipalName": "adelev@contoso.com"
            // }
            var me = await _graphServiceClient.Me
                .Request()
                .Select("displayName,department,jobTitle,mail,userPrincipalName")
                .GetAsync();

            if (me is null)
            {
                return "Microsoft Graph returned no user profile.";
            }

            var department = string.IsNullOrWhiteSpace(me.Department)
                ? "(not set)"
                : me.Department;

            return $"Department for {me.DisplayName} ({me.UserPrincipalName}): {department}";
        }
        catch (MicrosoftIdentityWebChallengeUserException ex)
        {
            // Thrown when the user must perform an interactive step (e.g. MFA / consent)
            // before an OBO token can be issued. The client should then re-authenticate
            // requesting the missing scopes (claims challenge).
            _logger.LogWarning(ex, "Interactive auth required to call Microsoft Graph.");
            return "Additional consent or MFA is required to read your department from Microsoft Graph.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve user department from Microsoft Graph.");
            return $"Failed to retrieve department: {ex.Message}";
        }
    }
}
