using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

var builder = new ConfigurationBuilder()
    .AddEnvironmentVariables()
    .AddUserSecrets<Program>();

IConfiguration configuration = builder.Build();

var endpoint = configuration["AZURE_AI_FOUNDRY_PROJECT_ENDPOINT"] ?? "https://<your-endpoint>.openai.azure.com/api/projects/project01";
var deploymentName = configuration["DEPLOYMENT_NAME"] ?? "gpt-4.1-nano";

AIProjectClient foundryClient = new(new Uri(endpoint), new DefaultAzureCredential());
ChatClient chatClient = foundryClient.GetAzureOpenAIChatClient(deploymentName: deploymentName);

var chatMessages = new List<ChatMessage>();

while (true)
{
    Console.Write("> ");
    var input = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(input))
    {
        Console.WriteLine("Exiting...");
        break;
    }

    Console.WriteLine("Response: ");

    string chatResponse = string.Empty;
    chatMessages.Add(new UserChatMessage(input));
    await foreach (StreamingChatCompletionUpdate response in chatClient.CompleteChatStreamingAsync(chatMessages))
    {
        if (response.ContentUpdate.Count == 0)
        {
            continue; // Skip if no content update
        }

        foreach (var contentUpdate in response.ContentUpdate)
        {
            if (contentUpdate.Kind == ChatMessageContentPartKind.Text)
            {
                chatResponse += contentUpdate.Text;
                Console.Write(contentUpdate.Text);
            }
        }
    }

    chatMessages.Add(new AssistantChatMessage(chatResponse));

    Console.WriteLine();
    Console.WriteLine();
}
