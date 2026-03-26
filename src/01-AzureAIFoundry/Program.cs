using Azure.AI.Extensions.OpenAI;
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using OpenAI.Responses;
using System.ClientModel.Primitives;
using System.Diagnostics;
using System.Text.Json;

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

    responseItems.Add(ResponseItem.CreateUserMessageItem(input));

    bool hasUserInputRequests = true;
    string chatResponse = string.Empty;

    while (hasUserInputRequests)
    {
        hasUserInputRequests = false;
        var approvalRequests = new List<McpToolCallApprovalRequestItem>();
        var oauthConsentLinks = new List<string>();
        IList<ResponseItem>? completedOutputItems = null;
        chatResponse = string.Empty;

        await foreach (StreamingResponseUpdate response in responseClient.CreateResponseStreamingAsync(responseItems))
        {
            Console.WriteLine($"DEBUG: Received response update of type {response.GetType().Name}");
            if (response is StreamingResponseOutputTextDeltaUpdate textDelta)
            {
                chatResponse += textDelta.Delta;
                Console.Write(textDelta.Delta);
            }
            else if (response is StreamingResponseOutputItemDoneUpdate outputItemDone)
            {
                if (outputItemDone.Item is McpToolCallApprovalRequestItem approvalRequest)
                {
                    approvalRequests.Add(approvalRequest);
                }
                else
                {
                    var rawJson = ModelReaderWriter.Write(outputItemDone.Item, ModelReaderWriterOptions.Json);
                    using var jsonDoc = JsonDocument.Parse(rawJson.ToMemory());
                    var root = jsonDoc.RootElement;

                    if (root.TryGetProperty("type", out var typeProperty)
                        && typeProperty.GetString() == "oauth_consent_request"
                        && root.TryGetProperty("consent_link", out var consentLinkProperty))
                    {
                        var consentLink = consentLinkProperty.GetString()!;
                        oauthConsentLinks.Add(consentLink);
                    }
                    else
                    {
                        Console.WriteLine($"DEBUG: Output item done - Type: {outputItemDone.Item.GetType().Name}, ID: {outputItemDone.Item.Id}");
                    }
                }
            }
            else if (response is StreamingResponseMcpListToolsFailedUpdate)
            {
                Console.WriteLine("DEBUG: MCP List Tools Failed (may require OAuth consent)");
            }
            else if (response is StreamingResponseCompletedUpdate completed)
            {
                Console.WriteLine($"DEBUG: Response completed - Status: {completed.Response.Status}");
                completedOutputItems = completed.Response.OutputItems;
            }
        }

        if (oauthConsentLinks.Count > 0)
        {
            hasUserInputRequests = true;
            foreach (var consentLink in oauthConsentLinks)
            {
                Console.WriteLine();
                Console.WriteLine("OAuth consent required. Opening browser...");
                Console.WriteLine($"  URL: {consentLink}");
                Process.Start(new ProcessStartInfo(consentLink) { UseShellExecute = true });
            }

            Console.WriteLine();
            Console.Write("Press Enter after completing OAuth consent in the browser...");
            Console.ReadLine();
        }
        else if (approvalRequests.Count > 0)
        {
            hasUserInputRequests = true;

            // Add the model's output items so the next request has context
            // about the tool calls being approved
            if (completedOutputItems is not null)
            {
                responseItems.AddRange(completedOutputItems);
            }

            foreach (var approvalRequest in approvalRequests)
            {
                Console.WriteLine();
                Console.WriteLine($"User Input Request:");
                Console.WriteLine($"  ID: {approvalRequest.Id}");
                Console.WriteLine($"  -> Auto approving");
                Console.WriteLine();

                responseItems.Add(ResponseItem.CreateMcpApprovalResponseItem(approvalRequest.Id, approved: true));
            }
        }
    }

    if (!string.IsNullOrEmpty(chatResponse))
    {
        responseItems.Add(ResponseItem.CreateAssistantMessageItem(chatResponse));
    }

    Console.WriteLine();
    Console.WriteLine();
}
