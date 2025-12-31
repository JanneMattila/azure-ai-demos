# Contoso Agent

```powershell
cd src\python\contoso-agent

uv sync
uv sync --prerelease=allow

# Set variables
$env:AZURE_AI_FOUNDRY_PROJECT_ENDPOINT = "https://<your-endpoint>.openai.azure.com/api/projects/project01"
$env:MODEL_DEPLOYMENT_NAME = "gpt-4o-mini"
$env:AGENT_NAME = "ContosoAgent"

uv run main.py
```

[Azure AI Projects client library for Python](https://pypi.org/project/azure-ai-projects/)
