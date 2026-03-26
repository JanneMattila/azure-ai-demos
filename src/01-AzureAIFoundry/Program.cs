using Azure.AI.Extensions.OpenAI;
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using OpenAI.Responses;

#pragma warning disable OPENAI001

var builder = new ConfigurationBuilder()
    .AddEnvironmentVariables()
    .AddUserSecrets<Program>();

IConfiguration configuration = builder.Build();

var endpoint = configuration["AZURE_AI_FOUNDRY_PROJECT_ENDPOINT"] ?? "https://<your-endpoint>.openai.azure.com/api/projects/project01";
var agentName = configuration["AGENT_NAME"] ?? "blank-agent";

var credential = new DefaultAzureCredential();
AIProjectClient projectClient = new(new Uri(endpoint), credential);
ProjectResponsesClient responseClient = projectClient.OpenAI.GetProjectResponsesClientForAgent(agentName);

var responseItems = new List<ResponseItem>();

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
    responseItems.Add(ResponseItem.CreateUserMessageItem(input));
    await foreach (StreamingResponseUpdate response in responseClient.CreateResponseStreamingAsync(responseItems))
    {
        Console.WriteLine($"DEBUG: Received response update of type {response.GetType().Name}");
        if (response is StreamingResponseOutputTextDeltaUpdate streamingResponseOutputTextDeltaUpdate)
        {
            chatResponse += streamingResponseOutputTextDeltaUpdate.Delta;
            Console.Write(streamingResponseOutputTextDeltaUpdate.Delta);
        }
    }

    responseItems.Add(ResponseItem.CreateAssistantMessageItem(chatResponse));

    Console.WriteLine();
    Console.WriteLine();
}
