import asyncio
import os
import time
from azure.identity.aio import DefaultAzureCredential
from azure.ai.projects.aio import AIProjectClient
from azure.ai.projects.models import (
    ResponsesUserMessageItemParam,
    MemoryStoreDefaultDefinition,
    MemoryStoreDefaultOptions,
    MemorySearchTool,
    PromptAgentDefinition,
)
from azure.core.exceptions import ResourceNotFoundError
from dotenv import load_dotenv

BLUE = "\033[94m"
YELLOW = "\033[93m"
RED = "\033[91m"
RESET = "\033[0m"

load_dotenv()


async def run_scenario() -> None:
    endpoint = os.getenv("AZURE_AI_FOUNDRY_PROJECT_ENDPOINT")
    deployment = os.getenv("MODEL_DEPLOYMENT_NAME")
    embedding_deployment = os.getenv("EMBEDDING_MODEL_DEPLOYMENT_NAME")
    agent_name = os.getenv("AGENT_NAME", "PizzaBot")

    if not endpoint or not deployment:
        raise ValueError(
            "Configuration missing. Set AZURE_AI_FOUNDRY_PROJECT_ENDPOINT and MODEL_DEPLOYMENT_NAME."
        )

    async with (
        DefaultAzureCredential() as credential,
        AIProjectClient(endpoint=endpoint, credential=credential) as project_client,
    ):
        # Specify memory store options
        options = MemoryStoreDefaultOptions(
            chat_summary_enabled=True,
            user_profile_enabled=True,
            user_profile_details="Remember everything about user pizza preferences and interests and orders.",
        )

        # Create memory store
        definition = MemoryStoreDefaultDefinition(
            chat_model=deployment,
            embedding_model=embedding_deployment,
            options=options,
        )

        # Use a unique name for testing (change to force new store)
        memory_store_name = "pizza_memory_store_v2"
        
        # Try to get existing memory store, create if not found
        try:
            memory_store = await project_client.memory_stores.get(name=memory_store_name)
            print(f"Using existing memory store with ID: {memory_store.id}")
        except ResourceNotFoundError:
            memory_store = await project_client.memory_stores.create(
                name=memory_store_name,
                definition=definition,
                description="Memory store for pizza agent",
            )
            print(f"Created memory store with ID: {memory_store.id}")

        # Define scope for memories
        # scope = "{tid}_{oid}"
        scope = "janne"

        # Create memory search tool - this will be attached to the Prompt Agent
        memory_tool = MemorySearchTool(
            memory_store_name=memory_store.name,
            scope=scope,
            update_delay=1,  # Wait 1 second of inactivity before updating memories
        )

        # Create a Prompt Agent (server-side) with memory search tool
        # This is the key difference - the agent runs on Azure AI Foundry
        # and automatically handles memory read/write during conversations
        agent = await project_client.agents.create_version(
            agent_name=agent_name,
            definition=PromptAgentDefinition(
                model=deployment,
                instructions="""You're PizzaBot, an AI assistant that helps users with their pizza orders.
You remember user preferences and previous orders.
When users ask about previous orders or preferences, use your memory to provide personalized responses.""",
                tools=[memory_tool],
            ),
            description="Pizza ordering assistant with memory",
        )
        print(f"Agent created (id: {agent.id}, name: {agent.name}, version: {agent.version})")

        # Get the OpenAI client for conversations
        openai_client = project_client.get_openai_client()

        # === First Conversation ===
        print(f"\n{'='*50}")
        print("FIRST CONVERSATION - Placing an order")
        print(f"{'='*50}")

        conversation1 = await openai_client.conversations.create()
        print(f"Conversation ID: {conversation1.id}")

        # First message
        await chat_with_agent(
            openai_client,
            agent,
            conversation1.id,
            "I would like to order pepperoni pizza.",
        )

        # Second message in same conversation
        await chat_with_agent(
            openai_client,
            agent,
            conversation1.id,
            "I'll have a large pepperoni pizza with extra cheese and I'll pick up in 30 minutes, please.",
        )

        # === DIAGNOSTIC: Manually update memories to test the API ===
        print(f"\n{YELLOW}📝 Manually updating memories via API to test...{RESET}")
        try:
            update_poller = await project_client.memory_stores.begin_update_memories(
                name=memory_store.name,
                scope=scope,
                items=[
                    ResponsesUserMessageItemParam(content="User ordered a large pepperoni pizza with extra cheese for pickup."),
                ],
                update_delay=0,
            )
            update_result = await update_poller.result()
            print(f"✅ Memory update successful! {len(update_result.memory_operations)} operations:")
            for op in update_result.memory_operations:
                print(f"   - {op.kind}: {op.memory_item.content}")
        except Exception as e:
            print(f"❌ Memory update failed: {e}")

        # Wait for memory to be processed
        print(f"\n{YELLOW}⏳ Waiting 30 seconds for memory to be indexed...{RESET}")
        for i in range(30, 0, -10):
            print(f"  {i} seconds remaining...")
            await asyncio.sleep(10)
        print()

        # === DIAGNOSTIC: Try to retrieve memories before second conversation ===
        print(f"{YELLOW}🔍 Testing memory retrieval before second conversation...{RESET}")
        try:
            # Retrieve static memories (no items = user profile memories)
            static_response = await project_client.memory_stores.search_memories(
                name=memory_store.name,
                scope=scope,
            )
            print(f"✅ Static memories found: {len(static_response.memories)}")
            for m in static_response.memories:
                print(f"   - {m.memory_item.content}")
        except Exception as e:
            print(f"❌ Static memory retrieval failed: {e}")

        # === Second Conversation ===
        print(f"\n{'='*50}")
        print("SECOND CONVERSATION - Testing memory recall")
        print(f"{'='*50}")

        conversation2 = await openai_client.conversations.create()
        print(f"Conversation ID: {conversation2.id}")

        # Ask about previous order - the agent should remember!
        await chat_with_agent(
            openai_client,
            agent,
            conversation2.id,
            "Could you remind me about my previous order?",
        )

        # === Show Memory Contents ===
        print(f"\n{'='*50}")
        print("MEMORY STORE CONTENTS")
        print(f"{'='*50}")

        print("\nMemory Stores:")
        async for ms in project_client.memory_stores.list():
            print(f"- ID: {ms.id}, Name: {ms.name}, Description: {ms.description}")

        # Search memories to see what was stored
        print("\n🔍 Searching memories...")
        try:
            search_response = await project_client.memory_stores.search_memories(
                name=memory_store.name,
                scope=scope,
                items=[
                    ResponsesUserMessageItemParam(
                        content="What pizza preferences or orders do I have?"
                    )
                ],
            )
            print(f"Found {len(search_response.memories)} memories:")
            for memory in search_response.memories:
                print(f"  - Content: {memory.memory_item.content}")
        except Exception as e:
            print(f"⚠️ Memory search failed: {e}")

        # Cleanup: Delete agent version (optional)
        # await project_client.agents.delete_version(agent_name=agent.name, agent_version=agent.version)
        # print(f"\nAgent version deleted")

        # Close the OpenAI client
        await openai_client.close()


async def chat_with_agent(openai_client, agent, conversation_id: str, message: str) -> None:
    """Send a message to the Prompt Agent and stream the response."""
    print(f"\n{BLUE}User:{RESET}\n{message}\n")
    print(f"{RED}Agent:{RESET}")

    # Use streaming for real-time response
    stream_response = await openai_client.responses.create(
        stream=True,
        input=message,
        conversation=conversation_id,
        extra_body={"agent": {"name": agent.name, "type": "agent_reference"}},
    )

    full_response = ""
    async for event in stream_response:
        if event.type == "response.output_text.delta":
            print(f"{RED}{event.delta}{RESET}", end="", flush=True)
            full_response += event.delta
        elif event.type == "response.completed":
            print()  # New line after response

    # Wait for user input before proceeding
    input(f"\n{YELLOW}Press Enter to continue...{RESET}\n")


async def main() -> None:
    print("=== Contoso Pizza Bot (Prompt Agent with Memory) ===")
    await run_scenario()


if __name__ == "__main__":
    asyncio.run(main())
