import os
from contextlib import asynccontextmanager
from pathlib import Path
from typing import AsyncIterator
from uuid import uuid4

from agent_framework import ChatAgent
from agent_framework.azure import AzureAIClient
from azure.identity.aio import DefaultAzureCredential
from fastapi import FastAPI, HTTPException, Request
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import HTMLResponse, JSONResponse, StreamingResponse
from fastapi.staticfiles import StaticFiles
from fastapi.templating import Jinja2Templates


BASE_DIR = Path(__file__).parent
credential: DefaultAzureCredential | None = None


@asynccontextmanager
async def lifespan(app: FastAPI):
	"""Initialize and dispose shared resources."""
	global credential
	credential = DefaultAzureCredential()
	yield


app = FastAPI(title="Travel Chat Demo", lifespan=lifespan)


# Serve static assets and templates
app.mount("/static", StaticFiles(directory=BASE_DIR / "static"), name="static")
templates = Jinja2Templates(directory=str(BASE_DIR / "templates"))


def build_agent(conversation_id) -> ChatAgent:
	"""Create a new ChatAgent instance using shared credential and env config."""
	endpoint = os.getenv("AZURE_AI_FOUNDRY_PROJECT_ENDPOINT")
	deployment = os.getenv("MODEL_DEPLOYMENT_NAME")

	if not endpoint or not deployment:
		raise HTTPException(
			status_code=500,
			detail="Configuration missing. Set AZURE_AI_FOUNDRY_PROJECT_ENDPOINT and MODEL_DEPLOYMENT_NAME.",
		)

	if not credential:
		raise HTTPException(status_code=500, detail="Credential is not initialized")

	return ChatAgent(
		chat_client=AzureAIClient(
			project_endpoint=endpoint,
			model_deployment_name=deployment,
			credential=credential,
			agent_name="TravelAgent",
			use_latest_version=True
		),
		instructions="You are a helpful travel assistant.",
		store=True
	)


@app.get("/", response_class=HTMLResponse)
async def home(request: Request):
	return templates.TemplateResponse("home.html", {"request": request})


@app.post("/chats/new")
async def create_chat() -> JSONResponse:
	conversation_id = uuid4().hex
	agent = build_agent(conversation_id=conversation_id)
	async with agent:
		thread = agent.get_new_thread()
	return JSONResponse({"chatId": conversation_id, "redirectUrl": f"/chats/{conversation_id}"})


@app.get("/chats/{conversation_id}", response_class=HTMLResponse)
async def chat_view(request: Request, conversation_id: str):
	return templates.TemplateResponse("chat.html", {"request": request, "chat_id": conversation_id})


@app.post("/api/chats/{conversation_id}/messages")
async def post_message(conversation_id: str, payload: dict):
	message = payload.get("message") if payload else None
	if not message:
		raise HTTPException(status_code=400, detail="Message is required")

	async def stream_agent_response() -> AsyncIterator[str]:
		agent = build_agent(conversation_id=conversation_id)
		thread = agent.get_new_thread()

		try:
			async with agent:
				async for chunk in agent.run_stream(message, thread=thread):
					if chunk.text and len(chunk.text) > 0:
						yield chunk.text
		except Exception as exc:  # pragma: no cover - graceful fallback
			yield f"Agent call failed: {exc}\n"
			yield f"Echo: {message}\n"

	return StreamingResponse(stream_agent_response(), media_type="text/plain")


if __name__ == "__main__":
	import uvicorn

	uvicorn.run("main:app", host="0.0.0.0", port=8000, reload=True)
