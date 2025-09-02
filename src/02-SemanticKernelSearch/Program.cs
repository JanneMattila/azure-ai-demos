using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using Azure.Search.Documents.Indexes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Identity.Client;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Data;
using Microsoft.SemanticKernel.PromptTemplates.Handlebars;

var builder = new ConfigurationBuilder()
    .AddEnvironmentVariables()
    .AddUserSecrets<Program>();

IConfiguration configuration = builder.Build();

var endpoint = new Uri(configuration["ENDPOINT"] ?? "https://<your-endpoint>.openai.azure.com/");
var deploymentName = configuration["DEPLOYMENT_NAME"] ?? "gpt-4o-mini";
var aiSearchEndpoint = new Uri(configuration["AI_SEARCH_ENDPOINT"] ?? "https://<your-endpoint>.search.windows.net");
var aiSearchIndex = configuration["AI_SEARCH_INDEX"] ?? "index1";

var kernelBuilder = Kernel.CreateBuilder();
var azureClient = new AzureOpenAIClient(endpoint, new DefaultAzureCredential());
kernelBuilder.AddAzureOpenAIChatCompletion(deploymentName, azureClient);

kernelBuilder.AddVectorStoreTextSearch<SearchDocument>();
kernelBuilder.Services.AddAzureAISearchCollection<SearchDocument>(aiSearchIndex, aiSearchEndpoint, new DefaultAzureCredential());

var kernel = kernelBuilder.Build();

#pragma warning disable SKEXP0001
var textSearch = kernel.Services.GetRequiredService<VectorStoreTextSearch<SearchDocument>>();
#pragma warning restore SKEXP0001

kernel.Plugins.Add(textSearch.CreateWithGetTextSearchResults("SearchPlugin"));

Console.WriteLine("Type your message. Ctrl + C to exit");

var chatMessages = new List<ChatMessageContent>();

while (true)
{
    Console.Write("> ");
    var input = Console.ReadLine();
    chatMessages.Add(new ChatMessageContent(AuthorRole.User, input));

    Console.WriteLine("Response: ");
    string chatResponse = string.Empty;
    var response = kernel.InvokePromptStreamingAsync(
        promptTemplate: """
                    Please use this information to answer the question:

                    {{#with (SearchPlugin-GetTextSearchResults question)}}  
                      {{#each this}}  
                        Name: {{Name}}
                        Value: {{Value}}
                        Link: {{Link}}
                        -----------------
                      {{/each}}
                    {{/with}}

                    Include citations to the relevant information where it is referenced in the response.

                    Question: {{question}}

                    Add table of citations at the end of your answer.
                    """,
        arguments: new KernelArguments()
        {
            { "question", input }
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
