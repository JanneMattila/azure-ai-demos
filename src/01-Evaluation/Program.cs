using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.AI.Evaluation.Quality;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

// Minified example of these examples:
// https://github.com/dotnet/ai-samples/tree/main/src/microsoft-extensions-ai-evaluation

var builder = new ConfigurationBuilder()
    .AddEnvironmentVariables()
    .AddUserSecrets<Program>();

IConfiguration configuration = builder.Build();

var endpoint = configuration["ENDPOINT"] ?? "https://<your-endpoint>.cognitiveservices.azure.com/";
var deploymentName = configuration["DEPLOYMENT_NAME"] ?? "gpt-4o";

IChatClient client =
    new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential())
        .GetChatClient(deploymentName)
        .AsIChatClient();

/// Enable function invocation support.
client = client.AsBuilder().UseFunctionInvocation().Build();
var chatConfiguration = new ChatConfiguration(client);

var chatOptions =
    new ChatOptions
    {
        Temperature = 0.0f,
        ResponseFormat = ChatResponseFormat.Text
    };

var messages = new List<ChatMessage>([
        new ChatMessage(
            ChatRole.System,
            """
            You are an AI assistant that can answer questions related to astronomy.
            Keep your responses concise staying under 100 words as much as possible.
            Use the imperial measurement system for all measurements in your response.
            """),
        new ChatMessage(
            ChatRole.User,
            "How far is the planet Venus from the Earth at its closest and furthest points?")
    ]);

var response = await client.GetResponseAsync(messages, chatOptions);

IEvaluator coherenceEvaluator = new CoherenceEvaluator();
IEvaluator relevanceEvaluator = new RelevanceEvaluator();
IEvaluator compositeEvaluator = new CompositeEvaluator(coherenceEvaluator, relevanceEvaluator);

EvaluationResult result = await compositeEvaluator.EvaluateAsync(messages, response, chatConfiguration);

ShowResults(result, CoherenceEvaluator.CoherenceMetricName);
ShowResults(result, RelevanceEvaluator.RelevanceMetricName);

static void ShowResults(EvaluationResult result, string metricName)
{
    NumericMetric metric = result.Get<NumericMetric>(metricName);
    Console.WriteLine($"{metricName} - Rating: {metric.Interpretation?.Rating}, Failed: {metric.Interpretation?.Failed}, Reason: {metric.Interpretation?.Reason}");
    if (metric.Diagnostics is not null)
    {
        foreach (var item in metric.Diagnostics)
        {
            Console.WriteLine($"{item}");
        }
    }
    Console.WriteLine();
}