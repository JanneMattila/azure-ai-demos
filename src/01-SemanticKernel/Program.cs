using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.AI.Foundry.Local;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;

var builder = new ConfigurationBuilder()
    .AddEnvironmentVariables()
    .AddUserSecrets<Program>();

IConfiguration configuration = builder.Build();

var endpoint = new Uri(configuration["ENDPOINT"] ?? "https://<your-endpoint>.openai.azure.com/");
var deploymentName = configuration["DEPLOYMENT_NAME"] ?? "gpt-4o-mini";
var useLocalModel = Convert.ToBoolean(configuration["USE_LOCAL_MODEL"] ?? "false");

var randomNumberFunction = KernelFunctionFactory.CreateFromMethod(
    method: () => Random.Shared.Next(1, 100),
    functionName: "GetRandomNumber",
    description: "Generates a random number between 1 and 99");

var kernelBuilder = Kernel.CreateBuilder();
kernelBuilder.Plugins.AddFromFunctions("RandomPlugin", "Random number generation", [randomNumberFunction]);

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
        Start by telling small 'did you know' thing about semantic kernel.

        Start by telling a small 'did you know' thing about Semantic Kernel.
        
        You have access to the following functions:
        - GetRandomNumber: Generates a random number between 1 and 99
        
        When users ask for random numbers, use the GetRandomNumber function.",
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
