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

var endpoint = new Uri(configuration["ENDPOINT"] ?? "https://<your-endpoint>.openai.azure.com/");
var deploymentName = configuration["DEPLOYMENT_NAME"] ?? "gpt-35-turbo";

var kernelBuilder = Kernel.CreateBuilder();

var azureClient = new AzureOpenAIClient(endpoint, new DefaultAzureCredential());
kernelBuilder.AddAzureOpenAIChatCompletion(deploymentName, azureClient);

var kernel = kernelBuilder.Build();

var agent = new ChatCompletionAgent
{
    Kernel = kernel,
    Instructions = @"You're a helpful assistant."
};

Console.WriteLine("Type your message. Ctrl + C to exit");

var chatMessages = new List<ChatMessageContent>();

//var initialMessage = await File.ReadAllTextAsync("chat1.txt");
//
// This model's maximum context length is 16385 tokens.
// However, your messages resulted in 25515 tokens.
// Please reduce the length of the messages.
//
var initialMessage = await File.ReadAllTextAsync("chat2.txt");
// Your childhood dog's name is SemanticDog.

var isInitialMessage = true;

while (true)
{
    string input;
    if (isInitialMessage)
    {
        Console.WriteLine($"> {initialMessage}");
        isInitialMessage = false;
        input = initialMessage;
    }
    else
    {
        Console.Write("> ");
        input = Console.ReadLine() ?? string.Empty;
    }

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
