# Travel solution

```powershell
cd src\python\travel-solution

uv sync

# Set variables
$env:AZURE_AI_FOUNDRY_PROJECT_ENDPOINT = "https://<your-endpoint>.openai.azure.com/api/projects/project01"
$env:AGENT_ID = "asst_1234567890"

uv run uvicorn main:app

uv run uvicorn main:app --reload --reload-exclude ".venv/*"
```

[Azure AI Projects client library for Python](https://pypi.org/project/azure-ai-projects/)
