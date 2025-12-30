using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;
using Microsoft.IdentityModel.Logging;

#if DEBUG
IdentityModelEventSource.ShowPII = true;
IdentityModelEventSource.LogCompleteSecurityArtifact = true;
#endif

var builder = WebApplication.CreateBuilder(args);

// Load user secrets in development
builder.Configuration.AddUserSecrets<Program>();

// Add authentication services
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(options =>
    {
        builder.Configuration.GetSection("AzureAd").Bind(options);
        
        options.TokenValidationParameters.ValidateIssuer = true;
        options.TokenValidationParameters.ValidAudiences = new[]
        {
            "00000002-0000-0000-c000-000000000000", // Copilot Studio?
            builder.Configuration["AzureAd:ClientId"]!,
            $"api://{builder.Configuration["AzureAd:ClientId"]}"
        };
        
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                TrapOnMessageReceived(context);
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                TrapOnTokenValidated(context);
                return Task.CompletedTask;
            },
            OnAuthenticationFailed = context =>
            {
                TrapOnAuthenticationFailed(context);
                return Task.CompletedTask;
            },
            OnChallenge = context =>
            {
                TrapOnChallenge(context);
                return Task.CompletedTask;
            }
        };
    }, options =>
    {
        builder.Configuration.GetSection("AzureAd").Bind(options);
    });

builder.Services.Configure<MicrosoftIdentityOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
{
    // Disable the automatic scope/roles validation
    options.Events.OnTokenValidated = async context =>
    {
        // Allow the original event to run if it exists
        if (context.Options.Events?.OnTokenValidated != null)
        {
            await context.Options.Events.OnTokenValidated(context);
        }

        // Skip the default Microsoft.Identity.Web scope/roles validation
        // by not calling the default implementation
    };
});

// Add authorization policies to handle both user and app-only scenarios
builder.Services.AddAuthorization(options =>
{
    // Default policy that accepts both user and app tokens
    options.AddPolicy("RequireAuthenticatedUserOrApp", policy =>
    {
        policy.RequireAuthenticatedUser();
    });

    // Policy for app-only tokens (has idp and appid claims)
    //options.AddPolicy("RequireAppOnly", policy =>
    //{
    //    policy.RequireClaim("idp");
    //    policy.RequireClaim("appid");
    //});

    // Policy for user tokens
    //options.AddPolicy("RequireUser", policy =>
    //{
    //    policy.RequireClaim("preferred_username");
    //});
});

// Add HttpContextAccessor to access user information in tools
builder.Services.AddHttpContextAccessor();

// Configure MCP Server (HTTP transport is automatic with ASP.NET Core)
builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly()
    .WithTools(
    [
        typeof(OrderTroubleshootingTool),
        typeof(RandomNumberTools),
        typeof(RecommendationTool),
        typeof(UserInfoTool)
    ]);

var app = builder.Build();

// Enable authentication and authorization middleware
app.UseAuthentication();
app.UseAuthorization();

// Map MCP endpoint to /mcp
app.MapMcp("/mcp")
    .RequireAuthorization("RequireAuthenticatedUserOrApp");

app.MapFallback(async context =>
{
    context.Response.StatusCode = 404;
    context.Response.ContentType = "plain/text";
    await context.Response.WriteAsync($"Not Found: {context.Request.Path.Value}");
});

await app.RunAsync();

// Trap methods for debugging JWT bearer token validation
// Set breakpoints in these methods to inspect the authentication flow
static void TrapOnMessageReceived(MessageReceivedContext context)
{
    // Breakpoint here: Token has been received from the request
    var token = context.Token;
    var request = context.Request;
    var authHeader = request.Headers.Authorization.ToString();
}

static void TrapOnTokenValidated(TokenValidatedContext context)
{
    // Breakpoint here: Token has been successfully validated
    var claimsPrincipal = context.Principal;
    var securityToken = context.SecurityToken;
    var claims = claimsPrincipal?.Claims;
    
    // Check if this is an app-only token or user token
    var appId = claims?.FirstOrDefault(c => c.Type == "appid")?.Value;
    var oid = claims?.FirstOrDefault(c => c.Type == "oid")?.Value;
    var idp = claims?.FirstOrDefault(c => c.Type == "idp")?.Value;
    var preferredUsername = claims?.FirstOrDefault(c => c.Type == "preferred_username")?.Value;
    
    var isAppOnlyToken = idp != null && preferredUsername == null;
}

static void TrapOnAuthenticationFailed(AuthenticationFailedContext context)
{
    // Breakpoint here: Authentication has failed
    var exception = context.Exception;
    var errorMessage = exception.Message;
    var innerException = exception.InnerException;
}

static void TrapOnChallenge(JwtBearerChallengeContext context)
{
    // Breakpoint here: Authentication is being challenged (e.g., missing or invalid token)
    var error = context.Error;
    var errorDescription = context.ErrorDescription;
    var errorUri = context.ErrorUri;
}

/*
Configuration needed in appsettings.json:

{
  "AzureAd": {
    "Instance": "https://login.microsoftonline.com/",
    "TenantId": "<your-tenant-id>",
    "ClientId": "<your-api-client-id>",
    "Audience": "<your-api-client-id>"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "Urls": "https://localhost:7155"
}

OR store sensitive values in user secrets:

dotnet user-secrets set "AzureAd:TenantId" "your-tenant-id"
dotnet user-secrets set "AzureAd:ClientId" "your-client-id"

Client configuration in .mcp/server.json:

{
  "servers": {
    "local-recommend-mcp-server": {
      "url": "https://localhost:7155/",
      "type": "http",
      "headers": {
        "Authorization": "Bearer <your-access-token>"
      }
    }
  },
  "inputs": []
}

To get an access token, use:
az login
az account get-access-token --resource <your-api-client-id> --query accessToken -o tsv
*/