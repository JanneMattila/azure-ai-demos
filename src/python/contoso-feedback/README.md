# Contoso Feedback

```powershell
cd src\python\contoso-feedback

uv sync
uv sync --prerelease=allow

uv run main.py
```

Set .env file with your Azure AI Foundry project endpoint, model deployment names, and agent name.

```plain
AZURE_AI_FOUNDRY_PROJECT_ENDPOINT=<your_project_endpoint>
MODEL_DEPLOYMENT_NAME=<your_model_deployment_name>
EMBEDDING_MODEL_DEPLOYMENT_NAME=<your_embedding_model_deployment_name>
AGENT_NAME=<your_agent_name>
```
