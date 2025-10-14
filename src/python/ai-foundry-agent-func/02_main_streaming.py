import os
from azure.ai.projects import AIProjectClient
from azure.identity import DefaultAzureCredential

# Get configuration from environment variables
endpoint = os.getenv("AZURE_AI_FOUNDRY_PROJECT_ENDPOINT", "https://<your-endpoint>.openai.azure.com/api/projects/project01")
agent_id = os.getenv("AGENT_ID", "asst_1234567890")

# Create the AI Project client
project_client = AIProjectClient(
    endpoint=endpoint,
    credential=DefaultAzureCredential()
)

# Get the agent
agent = project_client.agents.get_agent(agent_id)

# Create a thread
thread = project_client.agents.threads.create()

print(f"Type your message into thread, {thread.id}. Ctrl + C to exit")

try:
    while True:
        user_input = input("> ")
        
        # Create a message in the thread
        message = project_client.agents.messages.create(
            thread_id=thread.id,
            role="user",
            content=user_input
        )
        
        print("Response: ", end="", flush=True)
        
        # Create a run with streaming enabled
        with project_client.agents.runs.stream(
            thread_id=thread.id,
            agent_id=agent.id
        ) as stream:
            for event_type, event_data, _ in stream:
                # Handle text delta events (streaming text as it's generated)
                if hasattr(event_data, 'text') and event_data.text:
                    print(event_data.text, end="", flush=True)
                
                # Handle errors
                elif event_type == "error":
                    raise Exception(f"Stream error: {event_data}")
                
                # Stream completion
                elif event_type == "done":
                    break
        
        print("\n")

except KeyboardInterrupt:
    print("\nExiting...")
