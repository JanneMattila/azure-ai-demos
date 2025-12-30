import os
import asyncio
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
cancel_events: dict[str, asyncio.Event] = {}
agent_threads: dict[str, object] = {}

app = FastAPI(title="Travel Chat Demo")


# Serve static assets and templates
app.mount("/static", StaticFiles(directory=BASE_DIR / "static"), name="static")
templates = Jinja2Templates(directory=str(BASE_DIR / "templates"))


def build_agent() -> ChatAgent:
	"""Create a new ChatAgent instance using shared credential and env config."""
	endpoint = os.getenv("AZURE_AI_FOUNDRY_PROJECT_ENDPOINT")
	deployment = os.getenv("MODEL_DEPLOYMENT_NAME")

	if not endpoint or not deployment:
		raise HTTPException(
			status_code=500,
			detail="Configuration missing. Set AZURE_AI_FOUNDRY_PROJECT_ENDPOINT and MODEL_DEPLOYMENT_NAME.",
		)

	return ChatAgent(
		chat_client=AzureAIClient(
			project_endpoint=endpoint,
			model_deployment_name=deployment,
			credential=DefaultAzureCredential(),
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
	chat_id = uuid4().hex
	cancel_events[chat_id] = asyncio.Event()
	return JSONResponse({"chatId": chat_id, "redirectUrl": f"/chats/{chat_id}"})


@app.get("/chats/{chat_id}", response_class=HTMLResponse)
async def chat_view(request: Request, chat_id: str):
	return templates.TemplateResponse("chat.html", {"request": request, "chat_id": chat_id})


@app.post("/api/chats/{chat_id}/messages")
async def post_message(chat_id: str, payload: dict):
	message = payload.get("message") if payload else None
	if not message:
		raise HTTPException(status_code=400, detail="Message is required")

	async def stream_agent_response() -> AsyncIterator[str]:
		agent = build_agent()

		if chat_id in agent_threads:
			agent_thread = agent_threads[chat_id]
		else:
			agent_thread = agent.get_new_thread()
			agent_threads[chat_id] = agent_thread

		cancel_event = cancel_events.setdefault(chat_id, asyncio.Event())
		cancel_event.clear()

		try:
			async with agent:
				async for chunk in agent.run_stream(message, thread=agent_thread):
					if cancel_event.is_set():
						yield "User cancelled\n"
						break
					if chunk.text and chunk.text.strip():
						yield chunk.text
		except Exception as exc:  # pragma: no cover - graceful fallback
			yield f"Agent call failed: {exc}\n"
			yield f"Echo: {message}\n"

	return StreamingResponse(stream_agent_response(), media_type="text/plain")


@app.post("/api/chats/{chat_id}/cancel")
async def cancel_chat(chat_id: str) -> JSONResponse:
	cancel_event = cancel_events.setdefault(chat_id, asyncio.Event())
	cancel_event.set()
	return JSONResponse({"status": "cancelled"})


if __name__ == "__main__":
	import uvicorn

	uvicorn.run("main:app", host="0.0.0.0", port=8000, reload=True)
