using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.AI.Foundry.Local;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using ModelContextProtocol.Client;

var builder = new ConfigurationBuilder()
    .AddEnvironmentVariables()
    .AddUserSecrets<Program>();

IConfiguration configuration = builder.Build();

var endpoint = new Uri(configuration["ENDPOINT"] ?? "https://<your-endpoint>.openai.azure.com/");
var deploymentName = configuration["DEPLOYMENT_NAME"] ?? "gpt-4o-mini";
var useLocalModel = Convert.ToBoolean(configuration["USE_LOCAL_MODEL"] ?? "false");

var randomNumberFunction = KernelFunctionFactory.CreateFromMethod(
    method: () => Random.Shared.Next(1, 100),
    functionName: "get_random_number",
    description: "Generates a random number between 1 and 99");

var troubleshootOrderFunction = KernelFunctionFactory.CreateFromMethod(
    method: (string orderID) => $"Order {orderID} has been successfully processed.",
    functionName: "troubleshoot_order",
    description: @"
        Troubleshoots order status. 
        You need to have orderID in format 'ORD<order_number>'.");

// Create an MCPClient for the Microsoft Learn MCP endpoint
await using IMcpClient mcpClient = await McpClientFactory.CreateAsync(new SseClientTransport(new()
{
    Name = "Microsoft Learn",
    Endpoint = new Uri("https://learn.microsoft.com/api/mcp")
}));

// Retrieve the list of tools available on the MCP server
var tools = await mcpClient.ListToolsAsync();
foreach (var tool in tools)
{
    Console.WriteLine($"{tool.Name}: {tool.Description}");
}

var kernelBuilder = Kernel.CreateBuilder();
kernelBuilder.Plugins.AddFromFunctions("RandomNumber", "Random number generation", [randomNumberFunction]);
kernelBuilder
    .Plugins
    .AddFromFunctions(
        "TroubleshootOrder", "Order troubleshooting", 
        [troubleshootOrderFunction]);
kernelBuilder.Plugins.AddFromFunctions("MCPLearn", tools.Select(aiFunction => aiFunction.AsKernelFunction()));

if (useLocalModel)
{
    // https://learn.microsoft.com/en-us/azure/ai-foundry/foundry-local/get-started
    // winget install Microsoft.FoundryLocal
    // foundry model run phi-3.5-mini
    var manager = await FoundryLocalManager.StartModelAsync(aliasOrModelId: deploymentName);
    var model = await manager.GetModelInfoAsync(aliasOrModelId: deploymentName);
    ArgumentNullException.ThrowIfNull(model, $"Model {deploymentName} not found. Ensure the model is available in Foundry Local.");
    kernelBuilder.AddOpenAIChatCompletion(model.ModelId, manager.Endpoint, manager.ApiKey);
}
else
{
    var azureClient = new AzureOpenAIClient(endpoint, new DefaultAzureCredential());
    kernelBuilder.AddAzureOpenAIChatCompletion(deploymentName, azureClient);
}

var kernel = kernelBuilder.Build();

var agent = new ChatCompletionAgent
{
    Kernel = kernel,
    Instructions = @"
        You are chat agent teaching user about semantic kernel.

        Start by telling a small 'did you know' thing about Semantic Kernel.",
    Arguments = new KernelArguments(new PromptExecutionSettings()
    {
      FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()  
    })
};

Console.WriteLine("Type your message. Ctrl + C to exit");

var chatMessages = new List<ChatMessageContent>();

while (true)
{
    Console.Write("> ");
    var input = Console.ReadLine();
    chatMessages.Add(new ChatMessageContent(AuthorRole.User, input));

    Console.WriteLine("Response: ");

    string chatResponse = string.Empty;

    await foreach (AgentResponseItem<StreamingChatMessageContent> response in agent.InvokeStreamingAsync(chatMessages))
    {
        chatResponse += response.Message;
        Console.Write(response.Message);
    }

    chatMessages.Add(new ChatMessageContent(AuthorRole.Assistant, chatResponse));

    Console.WriteLine();
    Console.WriteLine();
}
