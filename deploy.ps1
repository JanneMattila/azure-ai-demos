$i = 0
$content = Get-Content .\agent.json -Raw

$content -replace '_NUMBER_', $i
$foundry = "<foundry>"
$project = "project"

$azureAIAgentsEndpoint = "https://$foundry.services.ai.azure.com/api/projects/$project"

while ($true) {
    $accessToken = (Get-AzAccessToken -ResourceUrl "https://ai.azure.com").Token
    $body = $content -replace '_NUMBER_', $i

    Invoke-RestMethod `
        -Method POST `
        -Uri "$azureAIAgentsEndpoint/agents/Contoso-$i/versions?api-version=2025-11-15-preview" `
        -Headers @{ "Content-Type" = "application/json" } `
        -Token $accessToken `
        -Authentication Bearer `
        -Body $body

    $i++    
}
