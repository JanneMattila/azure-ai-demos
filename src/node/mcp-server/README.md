# MyServer MCP server (stdio + DefaultAzureCredential)

A minimal [Model Context Protocol](https://modelcontextprotocol.io/) server
in plain Node.js. It runs as a **stdio** child process of the MCP client
(GitHub Copilot in VS Code, Claude Desktop, etc.) and authenticates as the
**currently signed-in user** using
[`DefaultAzureCredential`](https://learn.microsoft.com/azure/developer/javascript/sdk/authentication/credential-chains)
from `@azure/identity`.

Implementation notes:

- Built on the official [`@modelcontextprotocol/sdk`](https://www.npmjs.com/package/@modelcontextprotocol/sdk)
  (`McpServer` + `StdioServerTransport`). Tool input shapes are declared
  with [`zod`](https://www.npmjs.com/package/zod).
- `.env` is parsed with a tiny inline loader — no `dotenv` dependency.
- No app registration of your own is required for local use. The
  credential chain falls back to whatever you already have configured
  (Azure CLI, Azure PowerShell, VS Code Azure account, environment
  variables, or a managed identity when running in Azure).

## How auth works

`DefaultAzureCredential` tries these sources in order until one succeeds:

1. Environment variables (`AZURE_CLIENT_ID` + secret/cert, etc.)
2. Workload Identity (Kubernetes)
3. Managed Identity (when running in Azure)
4. Azure CLI (`az login`)
5. Azure PowerShell (`Connect-AzAccount`)
6. Azure Developer CLI (`azd auth login`)

For local dev the easiest path is:

```powershell
az login
az account set --subscription "<subscription name or id>"   # optional
```

The MCP server then requests tokens for whatever scope a tool needs
(e.g. `https://graph.microsoft.com/.default`) using that signed-in
identity.

## Project layout

| File | Purpose |
| --- | --- |
| [server.js](server.js) | Stdio MCP server + tool definitions |
| [package.json](package.json) | Dependencies: `@modelcontextprotocol/sdk`, `@azure/identity`, `zod` |
| [.env.example](.env.example) | Optional environment overrides |

Client-side configuration is in [.mcp.json](../../../.mcp.json) at the
repository root.

## Tools

| Tool | Description |
| --- | --- |
| `whoami` | Calls Microsoft Graph `/me` and returns the signed-in user's display name, UPN, mail, object id and job title. |
| `token-info` | Acquires a token for a given scope and returns its non-sensitive claims (`tid`, `oid`, `upn`, `aud`, `scp`, ...). **Never returns the raw token.** |
| `list-my-groups` | Lists the Entra ID groups the signed-in user is a member of (Graph `/me/memberOf`). |

## Install & run

```powershell
cd src\node\mcp-server
npm install

az login

# Optional overrides:
copy .env.example .env

# Quick local sanity check (Ctrl+C to exit; speaks JSON-RPC on stdio):
node server.js
```

You normally do not run the server directly — the MCP client launches
it.

## Use it from GitHub Copilot in VS Code

The repo already includes [.mcp.json](../../../.mcp.json):

```json
{
  "mcpServers": {
    "myserver": {
      "type": "stdio",
      "command": "node",
      "args": [
        "./src/node/mcp-server/server.js"
      ]
    }
  }
}
```

Steps:

1. Open this folder in VS Code.
2. Make sure you have run `az login` in a terminal whose environment
   VS Code can see (typically: launch VS Code after `az login`, or
   restart it once).
3. Command Palette → **MCP: List Servers** → start `myserver`.
4. Open Copilot Chat in **Agent** mode and try:
   - "Use the myserver `whoami` tool"
   - "List my Entra groups via myserver"
   - "Call myserver `token-info` with scope `https://graph.microsoft.com/.default`"

## Configuration (all optional)

| Variable | Default | Purpose |
| --- | --- | --- |
| `AZURE_TENANT_ID` | (from credential) | Forces `DefaultAzureCredential` to use a specific tenant. |
| `GRAPH_SCOPE` | `https://graph.microsoft.com/.default` | Scope used for Graph calls. |
| `DEFAULT_TOKEN_SCOPE` | `https://management.azure.com/.default` | Default scope for the `token-info` tool when no scope is provided. |

`.env` is `.gitignore`d — never commit it. Real secrets (client
secrets, certificates) belong in environment variables consumed by
`DefaultAzureCredential`, not in committed files.

## Maintaining the server

- **Add a tool**: call `server.registerTool(name, { title, description, inputSchema }, handler)`
  in [server.js](server.js). Use `zod` shapes in `inputSchema` and return
  `{ content: [{ type: "text", text: "..." }] }` from the handler. Use
  `getToken(scope)` to call any Azure or Graph API on behalf of the
  signed-in user.
- **Restrict access**: inside a handler, inspect the token's claims
  (tenant id, group membership, app roles) via `decodeJwtPayload` and
  throw to surface the error to the caller.
- **Switch credentials in CI / production**: set `AZURE_CLIENT_ID` +
  `AZURE_CLIENT_SECRET` (or federated-credential env vars) and
  `DefaultAzureCredential` will pick them up automatically.

## Troubleshooting

| Symptom | Fix |
| --- | --- |
| `CredentialUnavailableError: DefaultAzureCredential failed to retrieve a token` | Run `az login`, or set `AZURE_*` env vars. |
| Graph call returns `403 Insufficient privileges` | The signed-in user (or the credential's app) doesn't have the required Graph permission. Grant `User.Read` / `GroupMember.Read.All`. |
| Tools work in terminal but not in VS Code | VS Code may have inherited an older environment. Close and reopen VS Code from a terminal where `az login` is active. |
| Wrong tenant | Set `AZURE_TENANT_ID` in `.env` or run `az login --tenant <tenant-id>`. |
