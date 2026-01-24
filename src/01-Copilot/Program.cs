using GitHub.Copilot.SDK;

// copilot --server --port 4321 --yolo
using var client = new CopilotClient(new CopilotClientOptions
{
    CliUrl = "localhost:4321",
    UseStdio = false
});
await using var session = await client.CreateSessionAsync(new SessionConfig { Model = "claude-opus-4.5" });

var response = await session.SendAndWaitAsync(new MessageOptions { Prompt = "What is 2 + 2?" });
Console.WriteLine(response?.Data.Content);

response = await session.SendAndWaitAsync(new MessageOptions { 
    Prompt = @"
Run the following curl command and summarize that blog post in a few sentences:

curl https://www.jannemattila.com/appdev/2026/01/12/using-agent-skills.html

Then run the following command:

dir c:\

List the output in the format:
- file1
- file2
- file3
"
});
Console.WriteLine(response?.Data.Content);