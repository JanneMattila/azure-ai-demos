import asyncio
import os
from agent_framework import AgentThread, ChatAgent, ChatMessage, TextContent
from agent_framework.azure import AzureAIClient
from azure.identity.aio import DefaultAzureCredential
from azure.ai.projects.aio import AIProjectClient
from typing import Any

BLUE = "\033[94m"
YELLOW = "\033[93m"
RED = "\033[91m"
RESET = "\033[0m"

async def run_scenario() -> None:
    endpoint = os.getenv("AZURE_AI_FOUNDRY_PROJECT_ENDPOINT")
    deployment = os.getenv("MODEL_DEPLOYMENT_NAME")
    agent_name = os.getenv("AGENT_NAME")

    if not endpoint or not deployment or not agent_name:
        raise ValueError("Configuration missing. Set AZURE_AI_FOUNDRY_PROJECT_ENDPOINT, MODEL_DEPLOYMENT_NAME, and AGENT_NAME.")
    
    async with (
        DefaultAzureCredential() as credential,
        AIProjectClient(
            endpoint=endpoint,
            credential=credential) as project_client,
        AzureAIClient(
            project_endpoint=endpoint,
            model_deployment_name=deployment,
            agent_name=agent_name,
            credential=credential,
            use_latest_version=True
        ) as chat_client,
        ChatAgent(
            name=agent_name,
            store=True,
            chat_client=chat_client
        ) as agent,
    ):
        # Create a conversation using OpenAI client
        openai_client = project_client.get_openai_client()
        conversation = await openai_client.conversations.create()
        conversation_id = conversation.id
        print(f"Conversation ID: {conversation_id}")
                
        thread = agent.get_new_thread(service_thread_id=conversation_id)
        
        await execute_user_query(agent, thread,
            "Tell me about travel policies."
        )

        await execute_user_query(agent, thread,
            "How does our yearly review process works? What should I understand about it?"
        )

        await execute_user_query(agent, thread,
            "Am I 100% guaranteed to get my bonuses?"
        )

        await execute_user_query(agent, thread,
            "How much can I get bonuses compared to my yearly base salary?"
        )

        await execute_user_query(agent, thread,
            "Is my salary higher than my colleagues in my team?"
        )

# From:
# https://github.com/microsoft/agent-framework/blob/main/python/samples/getting_started/tools/ai_function_with_approval.py#L77
async def execute_user_query(agent: ChatAgent, thread: AgentThread, query: str) -> None:
    print(f"{BLUE}User:\n{query}{RESET} (Thread ID: {thread.service_thread_id})\n")
    print(f"{RED}Agent:{RESET} \n", end="", flush=True)
    
    current_input: list[ChatMessage] = [ChatMessage(role="user", contents=[TextContent(text=query)])]
    has_user_input_requests = True

    while has_user_input_requests:
        has_user_input_requests = False
        user_input_requests: list[Any] = []

        async for chunk in agent.run_stream(current_input, thread=thread):
            if chunk.text:
                print(f"{RED}{chunk.text}{RESET}", end="", flush=True)
            if chunk.user_input_requests:
                user_input_requests.extend(chunk.user_input_requests)

        if user_input_requests:
            has_user_input_requests = True
            current_input.clear()

            for user_input_needed in user_input_requests:
                print(
                    f"\n{YELLOW}User Input Request for function from {agent.name}:"
                    f"\n  Function Call ID: {user_input_needed.function_call.call_id}"
                    f"\n  Function name: {user_input_needed.function_call.name}"
                    f"\n  Arguments: {user_input_needed.function_call.arguments}"
                    f"\n  JSON: {user_input_needed.to_json()}"
                    f"\n  -> Auto approving{RESET}\n"
                )

                approved = True
                approval_response = user_input_needed.create_response(approved=approved)                
                current_input.append(ChatMessage(role="user", contents=[approval_response]))

    # Wait for user input before proceeding
    input("\n\nPress Enter to continue...\n")

async def main() -> None:
    print("=== Contoso Agent ===")
    await run_scenario()


if __name__ == "__main__":
    asyncio.run(main())
