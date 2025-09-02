
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Data;
using System.Text.Json.Serialization;

// Based from:
// https://github.com/microsoft/semantic-kernel/blob/main/dotnet/samples/Demos/VectorStoreRAG/TextSnippet.cs
public sealed class SearchDocument
{
    [VectorStoreKey]
    [JsonPropertyName("chunk_id")]
    public string ID { get; init; } = string.Empty;

    [TextSearchResultName]
    [VectorStoreData]
    [JsonPropertyName("title")]
    public string Name { get; set; } = string.Empty;

    [TextSearchResultValue]
    [VectorStoreData]
    [JsonPropertyName("chunk")]
    public string Text { get; set; } = string.Empty;

    [TextSearchResultLink]
    [VectorStoreData]
    [JsonPropertyName("path")]
    public string? Link { get; set; }

    [VectorStoreVector(1536)]
    [JsonPropertyName("text_vector")]
    public float[] Embedding { get; set; } = [];
}