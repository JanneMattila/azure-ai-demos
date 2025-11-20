using Azure.AI.OpenAI;
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.Configuration;
using Microsoft.Identity.Client.Platforms.Features.DesktopOs.Kerberos;
using OpenAI.Chat;
using System.ClientModel.Primitives;

var builder = new ConfigurationBuilder()
    .AddEnvironmentVariables()
    .AddUserSecrets<Program>();

IConfiguration configuration = builder.Build();

var endpoint = configuration["AZURE_AI_FOUNDRY_PROJECT_ENDPOINT"] ?? "https://<your-endpoint>.openai.azure.com/api/projects/project01";
var deploymentName = configuration["DEPLOYMENT_NAME"] ?? "gpt-4o";

var credential = new DefaultAzureCredential();
AIProjectClient foundryClient = new(new Uri(endpoint), credential);
ClientConnection connection = foundryClient.GetConnection(typeof(AzureOpenAIClient).FullName!);

if (!connection.TryGetLocatorAsUri(out Uri uri) || uri is null)
{
    throw new InvalidOperationException("Invalid URI.");
}
uri = new Uri($"https://{uri.Host}");

AzureOpenAIClient azureOpenAIClient = new AzureOpenAIClient(uri, credential);
ChatClient chatClient = azureOpenAIClient.GetChatClient(deploymentName: deploymentName);

var chatConfiguration = new ChatConfiguration(chatClient.AsIChatClient());

var datasetId = string.Empty;
Console.WriteLine("Available datasets:");

var datasetName = "dataset1";
var datasetVersion = "3";

await foreach(var data in foundryClient.Datasets.GetDatasetVersionsAsync())
{
    Console.WriteLine($"{data.Id} {data.Name}");
    if (data.Name == datasetName && data.Version == datasetVersion)
    {
        datasetId = data.Id;
    }
}

if (string.IsNullOrEmpty(datasetId))
{
    Console.WriteLine("Uploading dataset...");
    var dataset = foundryClient.Datasets.UploadFile(datasetName, datasetVersion, "dataset1.jsonl");
    datasetId = dataset.Value.Id;
}
else
{
    Console.WriteLine($"Using existing dataset with ID: {datasetId}");
}

// https://learn.microsoft.com/en-us/azure/ai-foundry/how-to/develop/evaluate-sdk#evaluator-parameter-format
// https://learn.microsoft.com/en-us/azure/ai-foundry/how-to/develop/cloud-evaluation
var violenceEvaluator = new EvaluatorConfiguration(EvaluatorIDs.Violence);
violenceEvaluator.InitParams.Add("azure_ai_project", BinaryData.FromObjectAsJson(endpoint));

var evaluation = new Evaluation(
    new InputDataset(datasetId),
    new Dictionary<string, EvaluatorConfiguration>
    {
        ["violence"] = violenceEvaluator,
        ["bleu_score"] = new EvaluatorConfiguration(EvaluatorIDs.BleuScore)
    })
    {
        DisplayName = $"My evaluation {DateTime.Now}",
        Description = "Evaluation created from .NET SDK",
    };
evaluation.Tags.Add("build", "v1.0.0");
evaluation.Tags.Add("createdBy", "pipeline");

Console.WriteLine("Creating evaluation...");
var evaluationRun = await foundryClient.Evaluations.CreateAsync(evaluation);
Console.WriteLine(evaluation.DisplayName);
Console.WriteLine(evaluation.Status);
