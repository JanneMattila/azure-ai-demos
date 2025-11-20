# MCP Server OAuth Setup Guide

## Prerequisites

1. **Azure AD App Registration** (API)
   - Go to Azure Portal ? Azure Active Directory ? App registrations
   - Create a new registration for your MCP Server API
   - Note the `Application (client) ID` and `Directory (tenant) ID`
   - Under "Expose an API", add a scope (e.g., `api://your-client-id/mcp.access`)
   - To use e.g., Azure CLI as client, add `04b07795-8ddb-461a-bbee-02f9e1bf7b46` as authorized client application

2. **Azure AD App Registration** (Client - optional for testing)
   - Create another app registration for testing clients
   - Add API permissions to access your MCP Server API
   - Grant admin consent for the permissions

## Configuration Steps

### 1. Update appsettings.json

Replace the placeholders in `appsettings.json`:

```json
{
  "AzureAd": {
    "Instance": "https://login.microsoftonline.com/",
    "TenantId": "YOUR_TENANT_ID",
    "ClientId": "YOUR_API_CLIENT_ID",
    "Audience": "YOUR_API_CLIENT_ID"
  }
}
```

### 2. Run the MCP Server

```bash
cd src/01-MCPServer
dotnet run
```

The server will start on `https://localhost:7155`

### 3. Get an Access Token

#### Option A: Using Azure CLI
```bash
az login
az account get-access-token --resource api://YOUR_API_CLIENT_ID --query accessToken -o tsv
```

#### Option B: Using Client Credentials Flow (for service-to-service)
```bash
curl -X POST https://login.microsoftonline.com/YOUR_TENANT_ID/oauth2/v2.0/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "client_id=YOUR_CLIENT_APP_ID" \
  -d "scope=api://YOUR_API_CLIENT_ID/.default" \
  -d "client_secret=YOUR_CLIENT_SECRET" \
  -d "grant_type=client_credentials"
```

### 4. Configure MCP Client

Update your `.mcp/server.json` or `.vscode/mcp.json`:

```json
{
  "servers": {
    "local-recommend-mcp-server": {
      "url": "https://localhost:7155/",
      "type": "http",
      "headers": {
        "Authorization": "Bearer YOUR_ACCESS_TOKEN"
      }
    }
  }
}
```

## Testing

### Test with curl
```bash
# Without token (should fail with 401)
curl -k https://localhost:7155/mcp

# With token (should succeed)
curl -k -H "Authorization: Bearer YOUR_ACCESS_TOKEN" https://localhost:7155/mcp
```

## Security Considerations

1. **Use HTTPS in production** - The example uses localhost with a development certificate
2. **Store secrets securely** - Use Azure Key Vault or user secrets for sensitive data
3. **Validate scopes** - Add scope-based authorization for fine-grained access control
4. **Token expiration** - Implement token refresh logic in your clients
5. **Audience validation** - Ensure the token audience matches your API

## Advanced: Scope-Based Authorization

To add scope-based authorization, modify `Program.cs`:

```csharp
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireMcpAccess", policy =>
        policy.RequireClaim("scp", "mcp.access"));
});

// Then use:
app.MapMcp().RequireAuthorization("RequireMcpAccess");
```

## Troubleshooting

- **401 Unauthorized**: Check token validity and audience
- **Certificate errors**: Trust the development certificate with `dotnet dev-certs https --trust`
- **CORS issues**: Add CORS policy if accessing from web applications
- **Token validation fails**: Verify tenant ID and client ID match your app registration
