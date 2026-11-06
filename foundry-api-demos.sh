#!/usr/bin/env bash
# Requires: Azure CLI (az), curl and jq

accessToken=$(az account get-access-token --resource "https://ai.azure.com" --query accessToken -o tsv)

foundry="<YOUR_FOUNDRY>"
project="proj-default"

azureAIAgentsEndpoint="https://$foundry.services.ai.azure.com/api/projects/$project"

#
# Microsoft Foundry REST API examples
# https://ai.azure.com/api-reference
#
apiVersion="v1"
agentName="Contoso-Demo"
modelDeployment="gpt-5"

#
# 1. List agents
#
agents=$(curl -s -X GET \
  "$azureAIAgentsEndpoint/agents?api-version=$apiVersion&limit=100" \
  -H "Authorization: Bearer $accessToken")

echo "$agents" | jq -r '
  ["name", "state", "version", "kind", "guardrail"],
  (.data[] | [
    .name,
    .state,
    .versions.latest.version,
    .versions.latest.definition.kind,
    ((.versions.latest.definition.rai_config.rai_policy_name // "") | split("/") | last)
  ] | map(. // "")) | @tsv' | column -t -s $'\t'

# If "has_more" is true, fetch the next page using the "after" cursor
echo "$agents" | jq '.has_more'
echo "$agents" | jq -r '.last_id'
# "$azureAIAgentsEndpoint/agents?api-version=$apiVersion&limit=100&after=$(echo "$agents" | jq -r '.last_id')"

# Filter by kind: prompt, hosted, workflow, external, voice
curl -s -X GET \
  "$azureAIAgentsEndpoint/agents?api-version=$apiVersion&kind=prompt" \
  -H "Authorization: Bearer $accessToken" | jq

#
# 2. Get agent
#
existingAgentName=$(echo "$agents" | jq -r '.data[0].name') # Or any agent name e.g., "SalesAgent"

agent=$(curl -s -X GET \
  "$azureAIAgentsEndpoint/agents/$existingAgentName?api-version=$apiVersion" \
  -H "Authorization: Bearer $accessToken")
echo "$agent" | jq
echo "$agent" | jq '.versions.latest.definition'
echo "$agent" | jq '.agent_endpoint'

# List versions of the agent
curl -s -X GET \
  "$azureAIAgentsEndpoint/agents/$existingAgentName/versions?api-version=$apiVersion" \
  -H "Authorization: Bearer $accessToken" | jq -r '
  ["name", "version", "status", "created_at"],
  (.data[] | [.name, .version, .status, .created_at] | map(tostring)) | @tsv' | column -t -s $'\t'

#
# 3. Publish prompt agent
#
# 3.1 Create a new prompt agent version (creates the agent if it doesn't exist).
#     Agent versions are immutable, so every change creates a new version.
promptAgent=$(jq -n --arg model "$modelDeployment" '{
  description: "Contoso demo prompt agent",
  definition: {
    kind: "prompt",
    model: $model,
    instructions: "You are a helpful assistant for Contoso. Answer briefly.",
    tools: []
  }
}')

agentVersion=$(curl -s -X POST \
  "$azureAIAgentsEndpoint/agents/$agentName/versions?api-version=$apiVersion" \
  -H "Authorization: Bearer $accessToken" \
  -H "Content-Type: application/json" \
  -d "$promptAgent")
echo "$agentVersion" | jq

# 3.2 Publish the version to the agent's stable endpoint
#     (by default the endpoint routes to "@latest").
publish=$(jq -n --arg version "$(echo "$agentVersion" | jq -r '.version')" '{
  agent_endpoint: {
    version_selector: {
      version_selection_rules: [
        {
          type: "FixedRatio",
          agent_version: $version,
          traffic_percentage: 100
        }
      ]
    },
    protocol_configuration: {
      responses: {}
    }
  }
}')

publishedAgent=$(curl -s -X PATCH \
  "$azureAIAgentsEndpoint/agents/$agentName?api-version=$apiVersion" \
  -H "Authorization: Bearer $accessToken" \
  -H "Content-Type: application/merge-patch+json" \
  -H "Foundry-Features: AgentEndpoints=V1Preview" \
  -d "$publish")
echo "$publishedAgent" | jq '.agent_endpoint'

# 3.3 Call the published agent via its stable endpoint (Responses protocol)
response=$(curl -s -X POST \
  "$azureAIAgentsEndpoint/agents/$agentName/endpoint/protocols/openai/responses?api-version=$apiVersion" \
  -H "Authorization: Bearer $accessToken" \
  -H "Content-Type: application/json" \
  -d "$(jq -n '{ input: "Hello! What can you do?" }')")
echo "$response" | jq -r '.output[] | select(.type == "message") | .content[].text'

#
# 4. Get guardrails
#
# Guardrails are RAI policies in the Foundry resource (Azure Resource Manager API)
managementAccessToken=$(az account get-access-token --resource "https://management.azure.com" --query accessToken -o tsv)

subscriptionId=$(az account show --query id -o tsv)
resourceGroup="rg-ai"

foundryResourceId="/subscriptions/$subscriptionId/resourceGroups/$resourceGroup/providers/Microsoft.CognitiveServices/accounts/$foundry"

curl -s -X GET \
  "https://management.azure.com$foundryResourceId/raiPolicies?api-version=2025-06-01" \
  -H "Authorization: Bearer $managementAccessToken" | jq -r '
  ["name", "type", "mode", "basePolicyName"],
  (.value[] | [.name, .properties.type, .properties.mode, .properties.basePolicyName] | map(. // "")) | @tsv' | column -t -s $'\t'

guardrailName="Guardrails240"

guardrail=$(curl -s -X GET \
  "https://management.azure.com$foundryResourceId/raiPolicies/$guardrailName?api-version=2025-06-01" \
  -H "Authorization: Bearer $managementAccessToken")
echo "$guardrail" | jq -r '
  ["name", "blocking", "enabled", "severityThreshold", "source"],
  (.properties.contentFilters[] | [.name, .blocking, .enabled, .severityThreshold, .source] | map(if . == null then "" else tostring end)) | @tsv' | column -t -s $'\t'
guardrailId=$(echo "$guardrail" | jq -r '.id')

#
# 5. Set guardrail to agent (prompt agents)
#
# Guardrail is part of the agent definition ("rai_config"), so it's set by creating a new agent version.
# Use full ARM resource id of the guardrail. Note: Non-existing policy is accepted but no filtering is applied!
agent=$(curl -s -X GET \
  "$azureAIAgentsEndpoint/agents/$agentName?api-version=$apiVersion" \
  -H "Authorization: Bearer $accessToken")

agentWithGuardrail=$(echo "$agent" | jq --arg id "$guardrailId" '.versions.latest | {
  description: .description,
  metadata: .metadata,
  definition: (.definition + { rai_config: { rai_policy_name: $id } })
}')

agentVersion=$(curl -s -X POST \
  "$azureAIAgentsEndpoint/agents/$agentName/versions?api-version=$apiVersion" \
  -H "Authorization: Bearer $accessToken" \
  -H "Content-Type: application/json" \
  -d "$agentWithGuardrail")
echo "$agentVersion" | jq -r '.version'
echo "$agentVersion" | jq '.definition.rai_config'

# If the endpoint is pinned to a specific version (see 3.2), publish the new version to take guardrail into use
publish=$(jq -n --arg version "$(echo "$agentVersion" | jq -r '.version')" '{
  agent_endpoint: {
    version_selector: {
      version_selection_rules: [
        {
          type: "FixedRatio",
          agent_version: $version,
          traffic_percentage: 100
        }
      ]
    }
  }
}')

curl -s -X PATCH \
  "$azureAIAgentsEndpoint/agents/$agentName?api-version=$apiVersion" \
  -H "Authorization: Bearer $accessToken" \
  -H "Content-Type: application/merge-patch+json" \
  -H "Foundry-Features: AgentEndpoints=V1Preview" \
  -d "$publish" | jq

#
# 6. Set guardrail to agent (hosted agents)
#
# Hosted agents deployed from code require multipart/form-data with both the definition and the code zip,
# so download the current code zip and post it together with the updated definition.
hostedAgentName="my-basic-python-thingy-agent"

hostedAgent=$(curl -s -X GET \
  "$azureAIAgentsEndpoint/agents/$hostedAgentName?api-version=$apiVersion" \
  -H "Authorization: Bearer $accessToken")
hostedLatestVersion=$(echo "$hostedAgent" | jq -r '.versions.latest.version')

hostedAgentWithGuardrail=$(echo "$hostedAgent" | jq --arg id "$guardrailId" '.versions.latest | {
  description: .description,
  metadata: .metadata,
  definition: (.definition + { rai_config: { rai_policy_name: $id } })
}')

if [[ $(echo "$hostedAgent" | jq '.versions.latest.definition.code_configuration != null') == "true" ]]; then
  # Hosted agent deployed from code
  codeZipFile="${TMPDIR:-/tmp}/$hostedAgentName.zip"
  metadataFile="${TMPDIR:-/tmp}/$hostedAgentName.json"
  curl -s -X GET \
    "$azureAIAgentsEndpoint/agents/$hostedAgentName/code:download?api-version=$apiVersion&agent_version=$hostedLatestVersion" \
    -H "Authorization: Bearer $accessToken" \
    -H "Accept: application/zip" \
    -o "$codeZipFile"
  codeZipSha256=$(sha256sum "$codeZipFile" | cut -d ' ' -f 1)

  echo "$hostedAgentWithGuardrail" > "$metadataFile"

  hostedAgentVersion=$(curl -s -X POST \
    "$azureAIAgentsEndpoint/agents/$hostedAgentName/versions?api-version=$apiVersion" \
    -H "Authorization: Bearer $accessToken" \
    -H "x-ms-code-zip-sha256: $codeZipSha256" \
    -F "metadata=<$metadataFile;type=application/json" \
    -F "code=@$codeZipFile;type=application/zip;filename=$hostedAgentName.zip")

  rm "$codeZipFile" "$metadataFile"
else
  # Hosted agent deployed from container image
  hostedAgentVersion=$(curl -s -X POST \
    "$azureAIAgentsEndpoint/agents/$hostedAgentName/versions?api-version=$apiVersion" \
    -H "Authorization: Bearer $accessToken" \
    -H "Content-Type: application/json" \
    -d "$hostedAgentWithGuardrail")
fi
echo "$hostedAgentVersion" | jq -r '.version'
echo "$hostedAgentVersion" | jq '.definition.rai_config'

# Hosted agent versions are provisioned asynchronously, so wait until the new version is active
while [[ $(echo "$hostedAgentVersion" | jq -r '.status') == "creating" ]]; do
  sleep 10
  hostedAgentVersion=$(curl -s -X GET \
    "$azureAIAgentsEndpoint/agents/$hostedAgentName/versions/$(echo "$hostedAgentVersion" | jq -r '.version')?api-version=$apiVersion" \
    -H "Authorization: Bearer $accessToken")
  echo "$hostedAgentVersion" | jq -r '.status'
done

# Delete the demo agent
curl -s -X DELETE \
  "$azureAIAgentsEndpoint/agents/$agentName?api-version=$apiVersion" \
  -H "Authorization: Bearer $accessToken" | jq
