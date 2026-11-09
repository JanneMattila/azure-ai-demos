
$accessToken = (Get-AzAccessToken -ResourceUrl "https://ai.azure.com").Token
# Or Azure CLI
$accessToken = az account get-access-token --resource "https://ai.azure.com" --query accessToken -o tsv | ConvertTo-SecureString -AsPlainText -Force

$foundry = "<YOUR_FOUNDRY>"
$project = "proj-default"

$azureAIAgentsEndpoint = "https://$foundry.services.ai.azure.com/api/projects/$project"

#
# Microsoft Foundry REST API examples
# https://ai.azure.com/api-reference
#
$apiVersion = "v1"
$modelDeployment = "model-router"

$agents = Invoke-RestMethod `
    -Method GET `
    -Uri "$azureAIAgentsEndpoint/agents?api-version=$apiVersion&limit=100" `
    -Token $accessToken `
    -Authentication Bearer

#
# Update model router deployment (routing mode and model subset)
# via Azure Resource Manager REST API
#
$subscriptionId = (az account show --query id -o tsv)
$resourceGroup = "rg-ai"
$armApiVersion = "2025-10-01-preview"

$armAccessToken = (Get-AzAccessToken -ResourceUrl "https://management.azure.com").Token
# Or Azure CLI
$armAccessToken = az account get-access-token --resource "https://management.azure.com" --query accessToken -o tsv | ConvertTo-SecureString -AsPlainText -Force

$deploymentUri = "https://management.azure.com/subscriptions/$subscriptionId/resourceGroups/$resourceGroup/providers/Microsoft.CognitiveServices/accounts/$foundry/deployments/$($modelDeployment)?api-version=$armApiVersion"

# Current deployment configuration
$currentDeployment = Invoke-RestMethod `
    -Method GET `
    -Uri $deploymentUri `
    -Token $armAccessToken `
    -Authentication Bearer
$currentDeployment | ConvertTo-Json -Depth 10

$modelRouterBody = @{
    sku        = @{
        name     = "GlobalStandard"
        capacity = 10
    }
    properties = @{
        model   = @{
            format  = "OpenAI"
            name    = "model-router"
            version = "2025-11-18"
        }
        routing = @{
            # balanced, quality or cost
            mode   = "balanced"
            models = @(
                @{ format = "OpenAI"; name = "gpt-5-mini"; version = "2025-08-07" },
                @{ format = "OpenAI"; name = "gpt-5.6-sol"; version = "2026-07-09" },
                @{ format = "OpenAI"; name = "gpt-4.1"; version = "2025-04-14" }
                @{ format = "Anthropic"; name = "claude-opus-5-5"; version = "1" }
            )
        }
    }
} | ConvertTo-Json -Depth 10

$updatedDeployment = Invoke-RestMethod `
    -Method PUT `
    -Uri $deploymentUri `
    -Token $armAccessToken `
    -Authentication Bearer `
    -ContentType "application/json" `
    -Body $modelRouterBody
$updatedDeployment | ConvertTo-Json -Depth 10