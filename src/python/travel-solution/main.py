from pathlib import Path
from uuid import uuid4

from fastapi import FastAPI, HTTPException, Request
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import HTMLResponse, JSONResponse
from fastapi.staticfiles import StaticFiles
from fastapi.templating import Jinja2Templates


BASE_DIR = Path(__file__).parent

app = FastAPI(title="Travel Chat Demo")

# Allow local development without CORS friction
app.add_middleware(
	CORSMiddleware,
	allow_origins=["*"],
	allow_credentials=True,
	allow_methods=["*"],
	allow_headers=["*"],
)

# Serve static assets and templates
app.mount("/static", StaticFiles(directory=BASE_DIR / "static"), name="static")
templates = Jinja2Templates(directory=str(BASE_DIR / "templates"))


@app.get("/", response_class=HTMLResponse)
async def home(request: Request):
	return templates.TemplateResponse("home.html", {"request": request})


@app.post("/chats/new")
async def create_chat() -> JSONResponse:
	chat_id = uuid4().hex
	return JSONResponse({"chatId": chat_id, "redirectUrl": f"/chats/{chat_id}"})


@app.get("/chats/{chat_id}", response_class=HTMLResponse)
async def chat_view(request: Request, chat_id: str):
	return templates.TemplateResponse("chat.html", {"request": request, "chat_id": chat_id})


@app.post("/api/chats/{chat_id}/messages")
async def post_message(chat_id: str, payload: dict) -> JSONResponse:
	message = payload.get("message") if payload else None
	if not message:
		raise HTTPException(status_code=400, detail="Message is required")

	# Hardcoded markdown response for now
	markdown = (
		f"### Travel Assistant\n\n"
		f"You asked: {message}\n\n"
		"**Bot:** Thanks for trying this demo!\n\n"
		"- This reply is hardcoded markdown.\n"
		"- Swap this with your model output when ready.\n"
	)

	return JSONResponse({"markdown": markdown, "chatId": chat_id})


if __name__ == "__main__":
	import uvicorn

	uvicorn.run("main:app", host="0.0.0.0", port=8000, reload=True)
