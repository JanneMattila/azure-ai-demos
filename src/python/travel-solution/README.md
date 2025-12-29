# Travel solution

```powershell
cd src\python\travel-solution

uv sync
uv sync --prerelease=allow

# Set variables
$env:AZURE_AI_FOUNDRY_PROJECT_ENDPOINT = "https://<your-endpoint>.openai.azure.com/api/projects/project01"
$env:MODEL_DEPLOYMENT_NAME = "gpt-4o-mini"

uv run uvicorn main:app

uv run uvicorn main:app --reload --reload-exclude ".venv"
```

[Azure AI Projects client library for Python](https://pypi.org/project/azure-ai-projects/)
