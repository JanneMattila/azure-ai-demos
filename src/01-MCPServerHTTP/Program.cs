using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;

var builder = WebApplication.CreateBuilder(args);

// Load user secrets in development
builder.Configuration.AddUserSecrets<Program>();

// Add authentication services
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));

builder.Services.AddAuthorization();

// Add HttpContextAccessor to access user information in tools
builder.Services.AddHttpContextAccessor();

// Configure MCP Server (HTTP transport is automatic with ASP.NET Core)
builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly()
    .WithTools(
    [
        typeof(RandomNumberTools),
        typeof(RecommendationTool),
        typeof(UserInfoTool)
    ]);

var app = builder.Build();

// Enable authentication and authorization middleware
app.UseAuthentication();
app.UseAuthorization();

// Protect MCP endpoints with authorization
app.MapMcp().RequireAuthorization();

await app.RunAsync();

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