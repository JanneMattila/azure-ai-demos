# Cooking AI Agent

An interactive console AI application for recipe search and ingredient extraction, built with Microsoft Agent Framework and Microsoft Foundry models.

## Features

- 🔍 **Recipe Search** - Find recipes by ingredients, cuisine, or dish name
- 📖 **Recipe Details** - Get full ingredients list and step-by-step instructions
- 🥘 **Ingredient Extraction** - Extract ingredients from recipe descriptions
- 🔄 **Ingredient Substitutes** - Find alternatives for dietary restrictions

## Prerequisites

- Python 3.9 or later
- Azure CLI installed and logged in (`az login`)
- Microsoft Foundry project with a deployed model (e.g., gpt-4o)

## Setup

1. **Create and activate virtual environment:**

```powershell
# Create virtual environment
python -m venv venv

# Activate virtual environment (Windows PowerShell)
.\venv\Scripts\Activate.ps1

# Or on Linux/macOS
# source venv/bin/activate
```

2. **Install dependencies:**

```powershell
# Note: --pre flag is required while Agent Framework is in preview
pip install -r requirements.txt --pre
```

3. **Set environment variables:**

```powershell
# Set your Microsoft Foundry project endpoint
$env:AZURE_AI_FOUNDRY_PROJECT_ENDPOINT = "https://<your-resource>.services.ai.azure.com/api/projects/<your-project>"

# Set your model deployment name
$env:MODEL_DEPLOYMENT_NAME = "gpt-4o"
```

4. **Run the application:**

```powershell
python main.py
```

## Usage Examples

Once the app is running, try these prompts:

```
You: Find me some pasta recipes
You: Show me the recipe for Spaghetti Carbonara
You: What can I substitute for eggs in baking?
You: I have chicken, garlic, and tomatoes. What can I make?
```

## Architecture

This app uses:
- **Microsoft Agent Framework** - For building the AI agent with tool calling
- **Microsoft Foundry** - For hosting the language model (gpt-4o)
- **DefaultAzureCredential** - For secure authentication

## Tools

The agent has access to these tools:

| Tool | Description |
|------|-------------|
| `search_recipes` | Search for recipes by ingredients or cuisine |
| `get_recipe_details` | Get detailed recipe with ingredients and instructions |
| `extract_ingredients` | Extract ingredients from recipe text |
| `suggest_substitutes` | Find ingredient alternatives |

## License

MIT
