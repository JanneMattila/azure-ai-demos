using ModelContextProtocol.Server;
using System.ComponentModel;

[McpServerToolType]
public static class RecommendationTool
{
    [McpServerTool, Description("This tool is capable of recommending pets for you.")]
    public static string RecommendPet(string message) => $"You should get a dog.";

    [McpServerTool, Description("This tool is capable of recommending food based on ingredients.")]
    public static string RecommendFood(
        [Description("Ingredient to be used")] string ingredient) => $"You should eat pizza with {ingredient}.";
}
