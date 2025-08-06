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

ChatCompletion response = await chatClient.CompleteChatAsync(new UserChatMessage("Hello!"));
Console.WriteLine($"Response: {response.Content[0].Text}");
