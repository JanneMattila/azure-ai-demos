using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.AddConsole(consoleLogOptions =>
{
    // Configure all logs to go to stderr
    consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
});

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly()
    .WithTools(
    [
        typeof(RandomNumberTools),
        typeof(RecommendationTool)
    ]);

var app = builder.Build();

await app.RunAsync();

/*
.vscode/mcp.json:
.mcp/server.json:

{
	"servers": {
		"local-recommend-mcp-server": {
			"url": "https://localhost:7155/",
			"type": "http"
		}
	},
	"inputs": []
}

OR

{
  "servers": {
    "local-recommend-mcp-server": {
      "type": "stdio",
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "./src/01-MCPServer/01-MCPServer.csproj"
      ]
    }
  }
}

 */