#!/usr/bin/env node
// Stdio MCP server using the official @modelcontextprotocol/sdk.
// Authenticates the current user via DefaultAzureCredential from @azure/identity.

import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { DefaultAzureCredential } from "@azure/identity";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { z } from "zod";

// ---------- tiny .env loader ----------
const here = dirname(fileURLToPath(import.meta.url));
try {
    const envText = readFileSync(join(here, ".env"), "utf8");
    for (const raw of envText.split(/\r?\n/)) {
        const line = raw.trim();
        if (!line || line.startsWith("#")) continue;
        const eq = line.indexOf("=");
        if (eq === -1) continue;
        const key = line.slice(0, eq).trim();
        let val = line.slice(eq + 1).trim();
        if ((val.startsWith('"') && val.endsWith('"')) ||
            (val.startsWith("'") && val.endsWith("'"))) {
            val = val.slice(1, -1);
        }
        if (!(key in process.env)) process.env[key] = val;
    }
} catch {
    // .env is optional
}

const GRAPH_SCOPE = process.env.GRAPH_SCOPE || "https://graph.microsoft.com/.default";
const DEFAULT_TOKEN_SCOPE =
    process.env.DEFAULT_TOKEN_SCOPE || "https://management.azure.com/.default";

const credential = new DefaultAzureCredential({
    tenantId: process.env.AZURE_TENANT_ID,
});

async function getToken(scope) {
    const token = await credential.getToken(scope);
    if (!token) throw new Error(`Could not acquire token for scope: ${scope}`);
    return token;
}

function decodeJwtPayload(jwt) {
    const parts = jwt.split(".");
    if (parts.length < 2) return null;
    let b = parts[1].replace(/-/g, "+").replace(/_/g, "/");
    if (b.length % 4) b += "=".repeat(4 - (b.length % 4));
    try {
        return JSON.parse(Buffer.from(b, "base64").toString("utf8"));
    } catch {
        return null;
    }
}

async function graphGet(path) {
    const { token } = await getToken(GRAPH_SCOPE);
    const res = await fetch(`https://graph.microsoft.com/v1.0${path}`, {
        headers: { Authorization: `Bearer ${token}` },
    });
    if (!res.ok) {
        throw new Error(`Graph ${path} -> ${res.status}: ${await res.text()}`);
    }
    return res.json();
}

function text(s) {
    return { content: [{ type: "text", text: String(s) }] };
}

// ---------- MCP server ----------
const server = new McpServer({ name: "myserver", version: "1.0.0" });

server.registerTool(
    "whoami",
    {
        title: "Who am I",
        description:
            "Returns the signed-in user (from Microsoft Graph /me) using DefaultAzureCredential.",
        inputSchema: {},
    },
    async () => {
        const me = await graphGet("/me");
        return text(
            [
                `Display name: ${me.displayName}`,
                `UPN: ${me.userPrincipalName}`,
                `Mail: ${me.mail ?? "(none)"}`,
                `Object id: ${me.id}`,
                `Job title: ${me.jobTitle ?? "(none)"}`,
            ].join("\n")
        );
    }
);

server.registerTool(
    "token-info",
    {
        title: "Token info",
        description:
            "Acquires an access token for the given scope and returns the non-sensitive claims from it. Never returns the raw token.",
        inputSchema: {
            scope: z
                .string()
                .optional()
                .describe(
                    `OAuth scope, e.g. "https://graph.microsoft.com/.default". Defaults to ${DEFAULT_TOKEN_SCOPE}.`
                ),
        },
    },
    async ({ scope }) => {
        const { token, expiresOnTimestamp } = await getToken(scope || DEFAULT_TOKEN_SCOPE);
        const c = decodeJwtPayload(token) || {};
        return text(
            JSON.stringify(
                {
                    tid: c.tid,
                    oid: c.oid,
                    sub: c.sub,
                    upn: c.upn,
                    preferred_username: c.preferred_username,
                    name: c.name,
                    aud: c.aud,
                    iss: c.iss,
                    scp: c.scp,
                    roles: c.roles,
                    expires_at: new Date(expiresOnTimestamp).toISOString(),
                },
                null,
                2
            )
        );
    }
);

server.registerTool(
    "list-my-groups",
    {
        title: "List my Entra ID groups",
        description: "Lists the Entra ID groups the signed-in user is a member of.",
        inputSchema: {
            top: z
                .number()
                .int()
                .min(1)
                .max(100)
                .optional()
                .describe("Max groups to return (default 25)."),
        },
    },
    async ({ top = 25 }) => {
        const data = await graphGet(`/me/memberOf?$top=${top}`);
        const groups = (data.value || [])
            .filter((g) => g["@odata.type"]?.endsWith("group"))
            .map((g) => `- ${g.displayName} (${g.id})`)
            .join("\n");
        return text(groups || "No groups returned.");
    }
);

const transport = new StdioServerTransport();
await server.connect(transport);
