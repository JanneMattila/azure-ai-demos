using Azure.AI.Vision.ImageAnalysis;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Processing;
using System.Diagnostics;

var builder = new ConfigurationBuilder()
    .AddEnvironmentVariables()
    .AddUserSecrets<Program>();

IConfiguration configuration = builder.Build();

var endpoint = new Uri(configuration["ENDPOINT"] ?? "https://<your-endpoint>.cognitiveservices.azure.com/");
var image = configuration["IMAGE_PATH"] ?? "/temp/vision/image1.jpg";

ImageAnalysisClient client = new(endpoint, new DefaultAzureCredential());


ImageAnalysisResult result = client.Analyze(
    BinaryData.FromBytes(File.ReadAllBytes(image)),
    VisualFeatures.People | VisualFeatures.Tags | VisualFeatures.Objects,
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

Console.WriteLine("Objects Detected:");
foreach (var obj in result.Objects.Values)
{
    Console.WriteLine($"- {obj.BoundingBox}");
    foreach (var tag in obj.Tags)
    {
        Console.WriteLine($"  - {tag.Name} (Confidence: {tag.Confidence:P2})");
    }
}

string outputImagePath = Path.Combine(Path.GetDirectoryName(image) ?? string.Empty, "output_image.png");
using (var imageStream = new FileStream(image, FileMode.Open, FileAccess.Read))
{
    using var outputImage = Image.Load(imageStream);
    foreach (var obj in result.Objects.Values)
    {
        var rect = new Rectangle(
            (int)obj.BoundingBox.X,
            (int)obj.BoundingBox.Y,
            (int)obj.BoundingBox.Width,
            (int)obj.BoundingBox.Height);
        outputImage.Mutate(x => x.Draw(Color.Red, 2, rect));
    }
    outputImage.SaveAsPng(outputImagePath);
}
