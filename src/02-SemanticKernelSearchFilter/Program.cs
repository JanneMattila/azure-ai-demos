using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Identity.Client;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Data;
using Microsoft.SemanticKernel.PromptTemplates.Handlebars;
using System.Text;

var builder = new ConfigurationBuilder()
    .AddEnvironmentVariables()
    .AddUserSecrets<Program>();

IConfiguration configuration = builder.Build();

var endpoint = new Uri(configuration["ENDPOINT"] ?? "https://<your-endpoint>.openai.azure.com/");
var deploymentName = configuration["DEPLOYMENT_NAME"] ?? "gpt-4o-mini";
var aiSearchEndpoint = new Uri(configuration["AI_SEARCH_ENDPOINT"] ?? "https://<your-endpoint>.search.windows.net");
var aiSearchIndex = configuration["AI_SEARCH_INDEX"] ?? "index1";
var author = configuration["AUTHOR"] ?? "Reviewer";

var kernelBuilder = Kernel.CreateBuilder();
var azureClient = new AzureOpenAIClient(endpoint, new DefaultAzureCredential());
kernelBuilder.AddAzureOpenAIChatCompletion(deploymentName, azureClient);

var searchClient = new SearchClient(aiSearchEndpoint, aiSearchIndex, new DefaultAzureCredential());

var kernel = kernelBuilder.Build();

Console.WriteLine("Type your message. Ctrl + C to exit");

var chatMessages = new List<ChatMessageContent>();

while (true)
{
    Console.Write("> ");
    var input = Console.ReadLine();
    chatMessages.Add(new ChatMessageContent(AuthorRole.User, input));

    SearchResults<Document> responseWithFilter = await searchClient.SearchAsync<Document>(
    new SearchOptions
    {
        VectorSearch = new()
        {
            Queries =
            { 
                new VectorizableTextQuery(input)
                { 
                    Fields = { "text_vector" }
                } 
            }
        },
        Filter = $"author eq '{author}'",
        Select = { "title", "author", "date", "path", "modified", "chunk" }
    });

    Console.WriteLine($"Single Vector Search With Filter Results:");
    var results = new StringBuilder();
    await foreach (SearchResult<Document> result in responseWithFilter.GetResultsAsync())
    {
        Document doc = result.Document;
        results.AppendLine($"Title: {doc.Title}, Author: {doc.Author}, Date: {doc.Date}, Modified: {doc.Modified}, Path: {doc.Path}, Text: {doc.Text}");
        Console.WriteLine($"Search result: Score: {result.Score}, Title: {doc.Title}, Author: {doc.Author}, Date: {doc.Date}, Modified: {doc.Modified}, Path: {doc.Path}");
    }

    Console.WriteLine("Response: ");
    string chatResponse = string.Empty;
    var response = kernel.InvokePromptStreamingAsync(
        promptTemplate: """
                    Please use this information to answer the question between lines marked with --- below:

                    ---
                    {{results}}  
                    ---

                    Include citations to the relevant information where it is referenced in the response.
                    If no information is found, then please politely say to the user that you could not find any
                    relevant information and that can they clarify their request a bit.

                    Question: {{question}}

                    Add table of citations at the end of your answer only if you used any citations in your answer.
                    """,
        arguments: new KernelArguments()
        {
            { "question", input },
            { "results", results.ToString() },
        },
        templateFormat: "handlebars",
        promptTemplateFactory: new HandlebarsPromptTemplateFactory());

    await foreach (var message in response.ConfigureAwait(false))
    {
        chatResponse += message;
        Console.Write(message);
    }

    chatMessages.Add(new ChatMessageContent(AuthorRole.Assistant, chatResponse));

    Console.WriteLine();
    Console.WriteLine();
}
