using Azure;
using Azure.AI.Agents.Persistent;
using Azure.Identity;
using Microsoft.Extensions.Configuration;

var builder = new ConfigurationBuilder()
    .AddEnvironmentVariables()
    .AddUserSecrets<Program>();

IConfiguration configuration = builder.Build();

var endpoint = configuration["AZURE_AI_FOUNDRY_PROJECT_ENDPOINT"] ?? "https://<your-endpoint>.openai.azure.com/api/projects/project01";
var agentID = configuration["AGENT_ID"] ?? "asst_1234567890";

PersistentAgentsClient agentsClient = new(endpoint, new DefaultAzureCredential());
PersistentAgent agent = agentsClient.Administration.GetAgent(agentID);

PersistentAgentThread thread = agentsClient.Threads.CreateThread();

Console.WriteLine($"Type your message into thread, {thread.Id}. Ctrl + C to exit");

while (true)
{
    Console.Write("> ");
    var input = Console.ReadLine();
    PersistentThreadMessage messageResponse = agentsClient.Messages.CreateMessage(
        thread.Id,
        MessageRole.User,
        input);

    Console.WriteLine("Response: ");
    ThreadRun run = agentsClient.Runs.CreateRun(thread.Id, agent.Id);

    do
    {
        await Task.Delay(TimeSpan.FromMilliseconds(500));
        run = agentsClient.Runs.GetRun(thread.Id, run.Id);
    }
    while (run.Status == RunStatus.Queued || run.Status == RunStatus.InProgress);

    if (run.Status != RunStatus.Completed)
    {
        throw new InvalidOperationException($"Run failed or was canceled: {run.LastError?.Message}");
    }

    Pageable<PersistentThreadMessage> messages = agentsClient.Messages.GetMessages(thread.Id, runId: run.Id, order: ListSortOrder.Ascending);

    foreach (PersistentThreadMessage threadMessage in messages)
    {
        foreach (MessageContent contentItem in threadMessage.ContentItems)
        {
            if (contentItem is MessageTextContent textItem)
            {
                Console.Write(textItem.Text);
            }
            else if (contentItem is MessageImageFileContent imageFileItem)
            {
                Console.Write($"<image from ID: {imageFileItem.FileId}");
            }
            Console.WriteLine();
        }
    }

    Console.WriteLine();
}
