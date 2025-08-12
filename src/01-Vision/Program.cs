using Azure.AI.Vision.Face;
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
if (!File.Exists(image))
{
    Console.WriteLine($"Image file not found: {image}");
    return;
}
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
using var imageStream = new FileStream(image, FileMode.Open, FileAccess.Read);
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

FaceClient faceClient = new(endpoint, new DefaultAzureCredential());

// Specify facial features to be retrieved
FaceAttributeType[] features =
[
    FaceAttributeType.Detection01.HeadPose,
    FaceAttributeType.Detection01.Occlusion,
    FaceAttributeType.Detection01.Accessories,
    FaceAttributeType.Detection01.Glasses
];

// Use client to detect faces in an image
using var imageData = File.OpenRead(image);
var response = await faceClient.DetectAsync(
    BinaryData.FromStream(imageData),
    FaceDetectionModel.Detection01,
    FaceRecognitionModel.Recognition01,
    returnFaceId: true,
    returnFaceAttributes: features);
IReadOnlyList<FaceDetectionResult> detectedFaces = response.Value;

Console.WriteLine($"Detected {detectedFaces.Count} faces in the image.");

foreach (var detectedFace in detectedFaces)
{
    Console.WriteLine($"- {detectedFace.FaceId} at {detectedFace.FaceRectangle}");
    Console.WriteLine($"  {detectedFace.FaceAttributes.FacialHair}");
    Console.WriteLine($"  {detectedFace.FaceAttributes.Age}");
    Console.WriteLine($"  {detectedFace.FaceAttributes.Mask}");
}