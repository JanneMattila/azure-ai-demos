using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;

var builder = new ConfigurationBuilder()
    .AddEnvironmentVariables()
    .AddUserSecrets<Program>();

IConfiguration configuration = builder.Build();

var endpoint = new Uri(configuration["AZURE_OPENAI_ENDPOINT"] ?? "https://<your-endpoint>.openai.azure.com/");
var deploymentName = configuration["AZURE_OPENAI_DEPLOYMENT_NAME"] ?? "gpt-4o-mini";

var azureClient = new AzureOpenAIClient(endpoint, new DefaultAzureCredential());

var kernelBuilder = Kernel.CreateBuilder();
kernelBuilder.AddAzureOpenAIChatCompletion(deploymentName, azureClient);
var kernel = kernelBuilder.Build();

var agent = new ChatCompletionAgent
{
    Kernel = kernel,
    Instructions = @"
        You are chat agent teaching user about semantic kernel.
        Start by telling small 'did you know' thing about semantic kernel."
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
