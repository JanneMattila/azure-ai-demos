import os
import time
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
        
        print("Response: ")
        
        # Create a run with streaming
        run = project_client.agents.runs.create_and_process(
            thread_id=thread.id,
            agent_id=agent.id
        )

        # Check if the run failed
        if run.status == "failed":
            raise Exception(f"Run failed or was canceled: {run.last_error.message if run.last_error else 'Unknown error'}")
        
        # Get messages from the run (filter by run_id to get only new messages)
        messages = project_client.agents.messages.list(
            thread_id=thread.id,
            run_id=run.id,
            order="asc"
        )
        
        # Display the messages
        for thread_message in messages:
            for content_item in thread_message.content:
                if content_item.type == "text":
                    print(content_item.text.value)
                elif content_item.type == "image_file":
                    print(f"<image from ID: {content_item.image_file.file_id}>")
                print()

        print()

except KeyboardInterrupt:
    print("\nExiting...")
