# AI Foundry Agent Service and Function App

```powershell
# Create virtual environment
python -m venv venv

# Activate virtual environment
.\venv\Scripts\Activate.ps1

# Install dependencies
pip install -r requirements.txt

# Set variables
$env:AZURE_AI_FOUNDRY_PROJECT_ENDPOINT = "https://<your-endpoint>.openai.azure.com/api/projects/project01"
$env:AGENT_ID = "asst_1234567890"

python 01_main.py
python 02_main_streaming.py
```

[Azure AI Projects client library for Python](https://pypi.org/project/azure-ai-projects/)
