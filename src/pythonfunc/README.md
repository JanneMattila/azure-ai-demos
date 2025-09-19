# MarkItDown

[MarkItDown](https://github.com/microsoft/markitdown)

[MarkItDown MCP](https://github.com/microsoft/mcp#-markitdown)

[Create a function in Azure from the command line](https://learn.microsoft.com/en-us/azure/azure-functions/how-to-create-function-azure-cli?pivots=programming-language-python&tabs=windows%2Cpowershell%2Cazure-cli)

```bash
# Create a virtual environment and install dependencies
python -m venv .venv
# Make sure you're using 3.12
# C:\Users\<username>\AppData\Local\Programs\Python\Python312\python.exe -m venv .venv

# Activate the virtual environment
.venv\scripts\activate

# Install dependencies
pip install -r requirements.txt

# Run the application
func start

# Deploy
$funcApp = "pythonfuncdemo..."
$accessToken = (Get-AzAccessToken).Token | ConvertFrom-SecureString -AsPlainText
func azure functionapp publish $funcApp --access-token $accessToken
```
