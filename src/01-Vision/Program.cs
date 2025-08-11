using Azure;
using Azure.AI.Vision.ImageAnalysis;
using Azure.Identity;
using Microsoft.Extensions.Configuration;

var builder = new ConfigurationBuilder()
    .AddEnvironmentVariables()
    .AddUserSecrets<Program>();

IConfiguration configuration = builder.Build();

var endpoint = new Uri(configuration["ENDPOINT"] ?? "https://<your-endpoint>.cognitiveservices.azure.com/");
var image = configuration["IMAGE_PATH"] ?? "/temp/vision/image1.jpg";

ImageAnalysisClient client = new ImageAnalysisClient(
    endpoint,
    new DefaultAzureCredential());


ImageAnalysisResult result = client.Analyze(
    BinaryData.FromBytes(File.ReadAllBytes(image)),
    VisualFeatures.People | VisualFeatures.Tags,
    new ImageAnalysisOptions
    { 
        GenderNeutralCaption = false
    });

Console.WriteLine("Image Analysis Result:");
Console.WriteLine(result.Caption?.Text ?? "No caption found.");
Console.WriteLine("Tags:");
foreach (var tag in result.Tags.Values)
{
    Console.WriteLine($"- {tag.Name} (Confidence: {tag.Confidence:P2})");
}

Console.WriteLine("People Detected:");
foreach (var person in result.People.Values)
{
    Console.WriteLine($"- {person.BoundingBox} (Confidence: {person.Confidence:P2})");
}

