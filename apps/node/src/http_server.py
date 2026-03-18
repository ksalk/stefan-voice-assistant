import asyncio
import threading

from aiohttp import web

from audio import play_audio
from state import node_state
from diagnostics import get_system_diagnostics

from config import localServerConfig

# ---------------------------------------------------------------------------
# Route handlers
# ---------------------------------------------------------------------------


async def handle_health(request):
    """
    GET /health
    Simple health check endpoint.

    Returns 200 OK with plain text "I'm alive".
    """
    return web.Response(status=200, text="I'm alive")


async def handle_status(request):
    """
    GET /status
    Returns detailed node status including state and system diagnostics.

    Response JSON:
        {
            "state": "listening" | "recording" | "initializing",
            "cpuUsage": <float>,
            "memoryUsage": {...},
            "diskUsage": {...}
        }
    """
    if node_state["recording"]:
        state = "recording"
    elif node_state["listening"]:
        state = "listening"
    else:
        state = "initializing"

    diagnostics = get_system_diagnostics()

    return web.json_response({"state": state, **diagnostics})


async def handle_audio(request):
    """
    POST /audio
    Receives a WAV audio payload (e.g. a TTS-synthesized notification from the server)
    and plays it through the speakers.

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
    audio_data = await request.read()

    if not audio_data:
        return web.Response(status=400, text="Empty audio payload")

    print(f"[HTTP] Received audio notification: {len(audio_data)} bytes")
    play_audio(audio_data, node_state)

    return web.Response(status=200, text="OK")


# ---------------------------------------------------------------------------
# App factory + server runner
# ---------------------------------------------------------------------------


def build_app() -> web.Application:
    app = web.Application()
    app.router.add_get("/health", handle_health)
    app.router.add_get("/status", handle_status)
    app.router.add_post("/audio", handle_audio)
    return app


def start_http_server() -> None:
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
        site = web.TCPSite(runner, localServerConfig.HOST, localServerConfig.PORT)
        await site.start()
        print(
            f"[HTTP] Server listening on http://{localServerConfig.HOST}:{localServerConfig.PORT}"
        )
        while True:
            await asyncio.sleep(3600)

    loop.run_until_complete(_run())


def start_http_server_thread() -> threading.Thread:
    """Convenience wrapper: creates, starts, and returns the daemon thread."""
    thread = threading.Thread(
        target=start_http_server,
        daemon=True,
        name="http-server",
    )
    thread.start()
    return thread
