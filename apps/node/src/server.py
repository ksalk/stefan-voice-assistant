import asyncio
import threading

from aiohttp import web

from state import node_state

DEFAULT_HTTP_HOST = "0.0.0.0"
DEFAULT_HTTP_PORT = 8080

# ---------------------------------------------------------------------------
# Route handlers
# ---------------------------------------------------------------------------

async def handle_health(request):
    """
    GET /health
    Returns the current node status.

    Response JSON:
        {
            "status": "ok",
            "state": "listening" | "recording" | "initializing"
        }
    """
    if node_state["recording"]:
        state = "recording"
    elif node_state["listening"]:
        state = "listening"
    else:
        state = "initializing"

    return web.json_response({"status": "ok", "state": state})


async def handle_text(request):
    """
    POST /text
    Receives a text payload (e.g. a TTS response from the server to speak back).

    # Future endpoints to consider:
    #   POST /control  — remote control: {"action": "start"|"stop"|"mute"}
    #   GET  /config   — read current thresholds / device settings
    #   POST /config   — update thresholds / device settings at runtime
    #
    # Future WebSocket migration note:
    #   aiohttp supports WebSocket endpoints natively on the same server.
    #   To add a /ws endpoint, add a handler using web.WebSocketResponse()
    #   and register it with app.router.add_get("/ws", handle_ws).
    #   No changes to build_app or start_http_server are required.
    """
    content_type = request.content_type or ""

    if "application/json" in content_type:
        try:
            body = await request.json()
            text = body.get("text", "")
        except Exception:
            return web.Response(status=400, text="Invalid JSON body")
    else:
        text = await request.text()

    if not text:
        return web.Response(status=400, text="Empty text payload")

    print(f"[HTTP] Received text: {text}")
    # TODO: pass `text` to a TTS engine for playback

    return web.Response(status=200, text="OK")


# ---------------------------------------------------------------------------
# App factory + server runner
# ---------------------------------------------------------------------------

def build_app() -> web.Application:
    app = web.Application()
    app.router.add_get("/health", handle_health)
    app.router.add_post("/text", handle_text)
    return app


def start_http_server(host: str, port: int) -> None:
    """
    Runs the aiohttp server in its own asyncio event loop inside a daemon
    thread. Using a dedicated loop (rather than asyncio.run in the main
    thread) keeps the synchronous mic loop untouched.

    Intended to be called as the target of a threading.Thread.
    """
    loop = asyncio.new_event_loop()
    asyncio.set_event_loop(loop)

    app = build_app()
    runner = web.AppRunner(app)

    async def _run():
        await runner.setup()
        site = web.TCPSite(runner, host, port)
        await site.start()
        print(f"[HTTP] Server listening on http://{host}:{port}")
        while True:
            await asyncio.sleep(3600)

    loop.run_until_complete(_run())


def start_http_thread(host: str, port: int) -> threading.Thread:
    """Convenience wrapper: creates, starts, and returns the daemon thread."""
    thread = threading.Thread(
        target=start_http_server,
        args=(host, port),
        daemon=True,
        name="http-server",
    )
    thread.start()
    return thread
