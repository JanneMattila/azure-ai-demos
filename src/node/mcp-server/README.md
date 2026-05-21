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

## Using your own app registration

By default the server uses the Azure CLI's public client id
(`04b07795-8ddb-461a-bbee-02f9e1bf7b46`) for the interactive browser
sign-in, so no app registration is required. If you want to use your
own app instead — for example to scope consent, brand the consent
screen, or pre-grant Graph permissions — set `INTERACTIVE_CLIENT_ID`
(and optionally `AZURE_TENANT_ID`) in `.env`:

```ini
AZURE_TENANT_ID=<your-tenant-id>
INTERACTIVE_CLIENT_ID=<your-app-client-id>
FORCE_INTERACTIVE_AUTH=true
```

### Register the app correctly

`InteractiveBrowserCredential` uses the OAuth 2.0 **authorization code
flow with PKCE** and a loopback redirect URI. That means your app must
be configured as a **public client** — not a confidential web app. If
the redirect URI is registered under the **Web** platform, AAD treats
the app as confidential and rejects the token request with:

> AADSTS7000218: The request body must contain the following parameter:
> `client_assertion` or `client_secret`.

To register the app properly:

1. **Microsoft Entra admin center → App registrations → New
   registration**. Give it a name; leave "Supported account types" as
   single-tenant (or whatever fits).
2. **Authentication → Add a platform → Mobile and desktop
   applications** (NOT "Web"). Add the redirect URI:

   ```text
   http://localhost
   ```

   If you accidentally added it under **Web**, delete that Web platform
   entry — leaving it there will keep AAD treating the app as
   confidential even when the desktop platform is also configured.
3. On the same **Authentication** blade, scroll to **Advanced
   settings** → set **Allow public client flows** to **Yes**.
4. **API permissions** → add the delegated Microsoft Graph permissions
   the tools need: `User.Read`, `GroupMember.Read.All`. Click **Grant
   admin consent** if your tenant requires it.
5. Copy **Application (client) ID** and **Directory (tenant) ID** from
   **Overview** into `.env` as shown above.
6. Do **not** create a client secret — public clients must not send
   one. If a secret exists it is simply unused for this flow.

### Force a fresh sign-in after changing the app

The credential caches tokens per `clientId`, so after switching to your
own app id, clear the cache so the next request triggers a browser
sign-in against the new app:

```powershell
Remove-Item "$env:LOCALAPPDATA\.IdentityService\myserver-mcp*" -ErrorAction SilentlyContinue
```

Then restart the MCP server.

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

## Publishing to a registry

The package is already wired for distribution as a CLI:

- [package.json](package.json) declares `"bin": { "myserver-mcp": "server.js" }`.
- [server.js](server.js) starts with `#!/usr/bin/env node`, so once installed it
  is directly executable.

That means consumers can launch it with `npx` (no clone required) and point
their MCP client at the published package.

### One-time prep

1. Open [package.json](package.json) and adjust fields for publishing:
   - Remove `"private": true` (or set it to `false`). It is currently
     `true` to prevent accidental publishes.
   - Pick a unique `name`. For the public npm registry use a scoped name
     you own, e.g. `"@your-scope/myserver-mcp"`. For an internal registry,
     follow your org's naming convention (often a scope mapped to the
     internal registry).
   - Bump `version` using [SemVer](https://semver.org/) for each release.
   - Optional but recommended: add `"files": ["server.js"]` so only the
     runtime file is shipped (no `.env`, no tests, no local junk), and
     add `"repository"`, `"license"`, and `"engines": { "node": ">=18" }`.

2. Verify what will actually be packed before publishing:

   ```powershell
   cd src\node\mcp-server
   npm pack --dry-run
   ```

   Make sure `.env` and any secrets are **not** in the file list. They
   are excluded by `.gitignore`, but `npm` uses `.npmignore`/`files`
   rules — use the `files` field above to be safe.

### Publish to the public npm registry

```powershell
cd src\node\mcp-server
npm login                  # one-time, uses https://registry.npmjs.org
npm publish --access public  # required for first publish of a scoped package
```

Subsequent releases: bump `version` in [package.json](package.json), then
`npm publish`.

### Publish to an internal / private registry

Typical options are Azure Artifacts, GitHub Packages, Verdaccio, JFrog
Artifactory, Nexus, etc. The flow is the same — only the registry URL
and auth differ. Create a project-local `.npmrc` (do **not** commit
tokens) so the right registry is used for this package only:

```ini
# src/node/mcp-server/.npmrc
@your-scope:registry=https://pkgs.your-company.example/npm/registry/
//pkgs.your-company.example/npm/registry/:_authToken=${NPM_TOKEN}
always-auth=true
```

Then publish:

```powershell
$env:NPM_TOKEN = "<token-from-your-registry>"
npm publish
```

Registry-specific helpers:

- **Azure Artifacts**: run `npx vsts-npm-auth -config .npmrc` (Windows)
  to populate credentials, or use a PAT with `Packaging: Read & write`.
- **GitHub Packages**: registry URL is
  `https://npm.pkg.github.com`, scope must match the GitHub org/user,
  and the token needs `write:packages`.

## Consume the published server from `.mcp.json`

Once the package is on a registry your users can reach, they do **not**
need to clone this repo. Update [.mcp.json](../../../.mcp.json) (or the
user's own MCP client config) to launch it via `npx`:

```json
{
  "mcpServers": {
    "myserver": {
      "type": "stdio",
      "command": "npx",
      "args": [
        "-y",
        "@your-scope/myserver-mcp"
      ],
      "env": {
        "FORCE_INTERACTIVE_AUTH": "false"
      }
    }
  }
}
```

Notes:

- `-y` auto-accepts the `npx` install prompt the first time.
- Pin a specific version for reproducibility, e.g.
  `"@your-scope/myserver-mcp@1.2.3"`.
- For an internal registry, users also need an `.npmrc` that points
  `@your-scope` at that registry (same pattern as the publish step
  above) so `npx` can resolve the package.
- On Windows, if `npx` is not on PATH for the MCP client, use the full
  path or `"command": "npx.cmd"`.

Users can still override behavior with environment variables:

```json
{
  "mcpServers": {
    "myserver": {
      "type": "stdio",
      "command": "npx",
      "args": ["-y", "@your-scope/myserver-mcp@^1"],
      "env": {
        "AZURE_TENANT_ID": "<tenant-id>",
        "INTERACTIVE_CLIENT_ID": "<app-client-id>",
        "FORCE_INTERACTIVE_AUTH": "true"
      }
    }
  }
}
```

## Troubleshooting

| Symptom | Fix |
| --- | --- |
| `CredentialUnavailableError: DefaultAzureCredential failed to retrieve a token` | Run `az login`, or set `AZURE_*` env vars. |
| `AADSTS7000218: The request body must contain ... 'client_assertion' or 'client_secret'` | Your `INTERACTIVE_CLIENT_ID` app is registered as a confidential client. Remove the `http://localhost` redirect URI from the **Web** platform and add it under **Mobile and desktop applications** instead, then set **Allow public client flows = Yes**. See [Using your own app registration](#using-your-own-app-registration). |
| Graph call returns `403 Insufficient privileges` | The signed-in user (or the credential's app) doesn't have the required Graph permission. Grant `User.Read` / `GroupMember.Read.All`. |
| Tools work in terminal but not in VS Code | VS Code may have inherited an older environment. Close and reopen VS Code from a terminal where `az login` is active. |
| Wrong tenant | Set `AZURE_TENANT_ID` in `.env` or run `az login --tenant <tenant-id>`. |
| Interactive browser popup doesn't reappear / want to force a fresh sign-in | Delete the persisted token cache and restart the server. On Windows: `Remove-Item "$env:LOCALAPPDATA\.IdentityService\myserver-mcp*"` (macOS: `~/Library/Keychains` entries named `myserver-mcp`; Linux: `~/.IdentityService/myserver-mcp*`). The cache name is controlled by `tokenCachePersistenceOptions.name` in `server.js`. |
