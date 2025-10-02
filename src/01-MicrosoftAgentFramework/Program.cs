using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;
using System.ComponentModel;

var builder = new ConfigurationBuilder()
    .AddEnvironmentVariables()
    .AddUserSecrets<Program>();

IConfiguration configuration = builder.Build();

var endpoint = new Uri(configuration["ENDPOINT"] ?? "https://<your-endpoint>.openai.azure.com/");
var deploymentName = configuration["DEPLOYMENT_NAME"] ?? "gpt-4o-mini";

AIAgent agent = new AzureOpenAIClient(endpoint, new AzureCliCredential())
    .GetChatClient(deploymentName)
    .CreateAIAgent(
        instructions: "You are helpful assistant.",
        tools: [AIFunctionFactory.Create(GetRandomNumber)]);

Console.WriteLine("Type your message. Ctrl + C to exit");

var chatMessages = new List<ChatMessage>();

while (true)
{
    Console.Write("> ");
    var input = Console.ReadLine();
    chatMessages.Add(new ChatMessage(ChatRole.User, input));

    Console.WriteLine("Response: ");

    string chatResponse = string.Empty;

    await foreach (var response in agent.RunStreamingAsync(chatMessages))
    {
        chatResponse += response.Text;
        Console.Write(response.Text);
    }

    chatMessages.Add(new ChatMessage(ChatRole.Assistant, chatResponse));

    Console.WriteLine();
    Console.WriteLine();
}

[Description("Get random number")]
static int GetRandomNumber(
    [Description("The lower bound for the random number")] int lowerBound = 0, 
    [Description("The upper bound for the random number")] int upperBound = 100)
    => Random.Shared.Next(lowerBound, upperBound);