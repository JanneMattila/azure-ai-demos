
$accessToken = (Get-AzAccessToken -ResourceUrl "https://ai.azure.com").Token
# Or Azure CLI
$accessToken = az account get-access-token --resource "https://ai.azure.com" --query accessToken -o tsv | ConvertTo-SecureString -AsPlainText -Force

$foundry = "<YOUR_FOUNDRY>"
$project = "proj-default"

$azureAIAgentsEndpoint = "https://$foundry.services.ai.azure.com/api/projects/$project"

$body = $content -replace '_NUMBER_', $i

Invoke-RestMethod `
    -Method POST `
    -Uri "$azureAIAgentsEndpoint/agents/Contoso-$i/versions?api-version=2025-11-15-preview" `
    -Headers @{ "Content-Type" = "application/json" } `
    -Token $accessToken `
    -Authentication Bearer `
    -Body $body

#
# Microsoft Foundry REST API examples
# https://ai.azure.com/api-reference
#
$apiVersion = "v1"
$agentName = "Contoso-Demo"
$modelDeployment = "gpt-5"

# Note: Use "$($agentName)?..." in URLs, because PowerShell 7 treats "$agentName?" as a variable name.

#
# 1. List agents
#
$agents = Invoke-RestMethod `
    -Method GET `
    -Uri "$azureAIAgentsEndpoint/agents?api-version=$apiVersion&limit=100" `
    -Token $accessToken `
    -Authentication Bearer

$agents.data | Select-Object name, state, `
@{ Name = "version"; Expression = { $_.versions.latest.version } }, `
@{ Name = "kind"; Expression = { $_.versions.latest.definition.kind } }, `
@{ Name = "guardrail"; Expression = { ($_.versions.latest.definition.rai_config.rai_policy_name -split "/")[-1] } } | Format-Table

# If "has_more" is true, fetch the next page using the "after" cursor
$agents.has_more
$agents.last_id
# -Uri "$azureAIAgentsEndpoint/agents?api-version=$apiVersion&limit=100&after=$($agents.last_id)"

# Filter by kind: prompt, hosted, workflow, external, voice
Invoke-RestMethod `
    -Method GET `
    -Uri "$azureAIAgentsEndpoint/agents?api-version=$apiVersion&kind=prompt" `
    -Token $accessToken `
    -Authentication Bearer

#
# 2. Get agent
#
$existingAgentName = $agents.data[0].name # Or any agent name e.g., "SalesAgent"

$agent = Invoke-RestMethod `
    -Method GET `
    -Uri "$azureAIAgentsEndpoint/agents/$($existingAgentName)?api-version=$apiVersion" `
    -Token $accessToken `
    -Authentication Bearer

$agent
$agent.versions.latest.definition
$agent.agent_endpoint | ConvertTo-Json -Depth 10

# List versions of the agent
$agentVersions = Invoke-RestMethod `
    -Method GET `
    -Uri "$azureAIAgentsEndpoint/agents/$existingAgentName/versions?api-version=$apiVersion" `
    -Token $accessToken `
    -Authentication Bearer
$agentVersions.data | Select-Object name, version, status, created_at

#
# 3. Publish prompt agent
#
# 3.1 Create a new prompt agent version (creates the agent if it doesn't exist).
#     Agent versions are immutable, so every change creates a new version.
$promptAgent = @{
    description = "Contoso demo prompt agent"
    definition  = @{
        kind         = "prompt"
        model        = $modelDeployment
        instructions = "You are a helpful assistant for Contoso. Answer briefly."
        tools        = @()
    }
} | ConvertTo-Json -Depth 10

$agentVersion = Invoke-RestMethod `
    -Method POST `
    -Uri "$azureAIAgentsEndpoint/agents/$agentName/versions?api-version=$apiVersion" `
    -ContentType "application/json" `
    -Token $accessToken `
    -Authentication Bearer `
    -Body $promptAgent
$agentVersion

# 3.2 Publish the version to the agent's stable endpoint
#     (by default the endpoint routes to "@latest").
$publish = @{
    agent_endpoint = @{
        version_selector       = @{
            version_selection_rules = @(
                @{
                    type               = "FixedRatio"
                    agent_version      = $agentVersion.version
                    traffic_percentage = 100
                }
            )
        }
        protocol_configuration = @{
            responses = @{}
        }
    }
} | ConvertTo-Json -Depth 10

$publishedAgent = Invoke-RestMethod `
    -Method PATCH `
    -Uri "$azureAIAgentsEndpoint/agents/$($agentName)?api-version=$apiVersion" `
    -ContentType "application/merge-patch+json" `
    -Headers @{ "Foundry-Features" = "AgentEndpoints=V1Preview" } `
    -Token $accessToken `
    -Authentication Bearer `
    -Body $publish
$publishedAgent.agent_endpoint | ConvertTo-Json -Depth 10

# 3.3 Call the published agent via its stable endpoint (Responses protocol)
$response = Invoke-RestMethod `
    -Method POST `
    -Uri "$azureAIAgentsEndpoint/agents/$agentName/endpoint/protocols/openai/responses?api-version=$apiVersion" `
    -ContentType "application/json" `
    -Token $accessToken `
    -Authentication Bearer `
    -Body (@{ input = "Hello! What can you do?" } | ConvertTo-Json)
$response.output | Where-Object { $_.type -eq "message" } | ForEach-Object { $_.content.text }

#
# 4. Get guardrails
#
# Guardrails are RAI policies in the Foundry resource (Azure Resource Manager API)
$managementAccessToken = (Get-AzAccessToken -ResourceUrl "https://management.azure.com").Token
# Or Azure CLI
$managementAccessToken = az account get-access-token --resource "https://management.azure.com" --query accessToken -o tsv | ConvertTo-SecureString -AsPlainText -Force

$subscriptionId = (Get-AzContext).Subscription.Id
# Or Azure CLI
$subscriptionId = az account show --query id -o tsv
$resourceGroup = "rg-ai"

$foundryResourceId = "/subscriptions/$subscriptionId/resourceGroups/$resourceGroup/providers/Microsoft.CognitiveServices/accounts/$foundry"

$guardrails = Invoke-RestMethod `
    -Method GET `
    -Uri "https://management.azure.com$foundryResourceId/raiPolicies?api-version=2025-06-01" `
    -Token $managementAccessToken `
    -Authentication Bearer

$guardrails.value | Select-Object name, `
@{ Name = "type"; Expression = { $_.properties.type } }, `
@{ Name = "mode"; Expression = { $_.properties.mode } }, `
@{ Name = "basePolicyName"; Expression = { $_.properties.basePolicyName } } | Format-Table

$guardrailName = "Guardrails240"

$guardrail = Invoke-RestMethod `
    -Method GET `
    -Uri "https://management.azure.com$foundryResourceId/raiPolicies/$($guardrailName)?api-version=2025-06-01" `
    -Token $managementAccessToken `
    -Authentication Bearer
$guardrail.properties.contentFilters | Format-Table

#
# 5. Set guardrail to agent (prompt agents)
#
# Guardrail is part of the agent definition ("rai_config"), so it's set by creating a new agent version.
# Use full ARM resource id of the guardrail. Note: Non-existing policy is accepted but no filtering is applied!
$agent = Invoke-RestMethod `
    -Method GET `
    -Uri "$azureAIAgentsEndpoint/agents/$($agentName)?api-version=$apiVersion" `
    -Token $accessToken `
    -Authentication Bearer

$latest = $agent.versions.latest
$definition = $latest.definition
$definition | Add-Member -NotePropertyName "rai_config" -NotePropertyValue @{ rai_policy_name = $guardrail.id } -Force

$agentWithGuardrail = @{
    description = $latest.description
    metadata    = $latest.metadata
    definition  = $definition
} | ConvertTo-Json -Depth 20

$agentVersion = Invoke-RestMethod `
    -Method POST `
    -Uri "$azureAIAgentsEndpoint/agents/$agentName/versions?api-version=$apiVersion" `
    -ContentType "application/json" `
    -Token $accessToken `
    -Authentication Bearer `
    -Body $agentWithGuardrail
$agentVersion.version
$agentVersion.definition.rai_config

# If the endpoint is pinned to a specific version (see 3.2), publish the new version to take guardrail into use
$publish = @{
    agent_endpoint = @{
        version_selector = @{
            version_selection_rules = @(
                @{
                    type               = "FixedRatio"
                    agent_version      = $agentVersion.version
                    traffic_percentage = 100
                }
            )
        }
    }
} | ConvertTo-Json -Depth 10

Invoke-RestMethod `
    -Method PATCH `
    -Uri "$azureAIAgentsEndpoint/agents/$($agentName)?api-version=$apiVersion" `
    -ContentType "application/merge-patch+json" `
    -Headers @{ "Foundry-Features" = "AgentEndpoints=V1Preview" } `
    -Token $accessToken `
    -Authentication Bearer `
    -Body $publish

#
# 6. Set guardrail to agent (hosted agents)
#
# Hosted agents deployed from code require multipart/form-data with both the definition and the code zip,
# so download the current code zip and post it together with the updated definition.
$hostedAgentName = "my-basic-python-thingy-agent"

$hostedAgent = Invoke-RestMethod `
    -Method GET `
    -Uri "$azureAIAgentsEndpoint/agents/$($hostedAgentName)?api-version=$apiVersion" `
    -Token $accessToken `
    -Authentication Bearer

$hostedLatest = $hostedAgent.versions.latest
$hostedDefinition = $hostedLatest.definition
$hostedDefinition | Add-Member -NotePropertyName "rai_config" -NotePropertyValue @{ rai_policy_name = $guardrail.id } -Force

$hostedAgentWithGuardrail = @{
    description = $hostedLatest.description
    metadata    = $hostedLatest.metadata
    definition  = $hostedDefinition
} | ConvertTo-Json -Depth 20

if ($null -ne $hostedDefinition.code_configuration) {
    # Hosted agent deployed from code
    $codeZipFile = Join-Path $env:TEMP "$hostedAgentName.zip"
    Invoke-RestMethod `
        -Method GET `
        -Uri "$azureAIAgentsEndpoint/agents/$hostedAgentName/code:download?api-version=$apiVersion&agent_version=$($hostedLatest.version)" `
        -Headers @{ "Accept" = "application/zip" } `
        -Token $accessToken `
        -Authentication Bearer `
        -OutFile $codeZipFile
    $codeZipSha256 = (Get-FileHash -Path $codeZipFile -Algorithm SHA256).Hash.ToLowerInvariant()

    $multipart = [System.Net.Http.MultipartFormDataContent]::new()
    $metadataPart = [System.Net.Http.StringContent]::new($hostedAgentWithGuardrail, [System.Text.Encoding]::UTF8, "application/json")
    $multipart.Add($metadataPart, "metadata")
    $codePart = [System.Net.Http.ByteArrayContent]::new([System.IO.File]::ReadAllBytes($codeZipFile))
    $codePart.Headers.ContentType = "application/zip"
    $multipart.Add($codePart, "code", "$hostedAgentName.zip")

    $hostedAgentVersion = Invoke-RestMethod `
        -Method POST `
        -Uri "$azureAIAgentsEndpoint/agents/$hostedAgentName/versions?api-version=$apiVersion" `
        -Headers @{ "x-ms-code-zip-sha256" = $codeZipSha256 } `
        -Token $accessToken `
        -Authentication Bearer `
        -Body $multipart

    Remove-Item $codeZipFile
}
else {
    # Hosted agent deployed from container image
    $hostedAgentVersion = Invoke-RestMethod `
        -Method POST `
        -Uri "$azureAIAgentsEndpoint/agents/$hostedAgentName/versions?api-version=$apiVersion" `
        -ContentType "application/json" `
        -Token $accessToken `
        -Authentication Bearer `
        -Body $hostedAgentWithGuardrail
}
$hostedAgentVersion.version
$hostedAgentVersion.definition.rai_config

# Hosted agent versions are provisioned asynchronously, so wait until the new version is active
while ($hostedAgentVersion.status -eq "creating") {
    Start-Sleep -Seconds 10
    $hostedAgentVersion = Invoke-RestMethod `
        -Method GET `
        -Uri "$azureAIAgentsEndpoint/agents/$hostedAgentName/versions/$($hostedAgentVersion.version)?api-version=$apiVersion" `
        -Token $accessToken `
        -Authentication Bearer
    $hostedAgentVersion.status
}

# Delete the demo agent
Invoke-RestMethod `
    -Method DELETE `
    -Uri "$azureAIAgentsEndpoint/agents/$($agentName)?api-version=$apiVersion" `
    -Token $accessToken `
    -Authentication Bearer
