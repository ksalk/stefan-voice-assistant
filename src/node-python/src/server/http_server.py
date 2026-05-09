"""
Local HTTP server.

Runs on the shared asyncio event loop — no separate thread or event loop
required.  All handlers are async and non-blocking.

The /audio endpoint enqueues WAV data into the AudioPlayer's queue instead
of playing it directly, so the HTTP response is returned immediately without
blocking other requests during playback.
"""

import asyncio
import logging

from aiohttp import web

from diagnostics import get_system_diagnostics
from state import AppState

logger = logging.getLogger(__name__)


# ---------------------------------------------------------------------------
# App factory
# ---------------------------------------------------------------------------


def build_app(state: AppState, play_queue: asyncio.Queue) -> web.Application:
    """
    Build and return the aiohttp Application.

    Args:
        state:      Shared AppState instance.
        play_queue: AudioPlayer queue; POST /audio enqueues payloads here.
    """
    app = web.Application()

    # Attach shared objects so handlers can access them without globals.
    app["state"] = state
    app["play_queue"] = play_queue

    app.router.add_get("/health", handle_health)
    app.router.add_get("/status", handle_status)
    app.router.add_post("/audio", handle_audio)

    return app


# ---------------------------------------------------------------------------
# Route handlers
# ---------------------------------------------------------------------------


async def handle_health(request: web.Request) -> web.Response:
    """
    GET /health

    Simple liveness probe.  Returns 200 with a JSON body so clients can
    easily check both HTTP connectivity and the node's current state.
    """
    state: AppState = request.app["state"]
    return web.json_response({"status": "ok", "state": state.status_label})


async def handle_status(request: web.Request) -> web.Response:
    """
    GET /status

    Returns detailed status: current state label plus system diagnostics
    (CPU, memory, disk).  psutil's cpu_percent uses a 0.1 s sample which
    blocks briefly; run it in an executor to keep the loop free.
    """
    state: AppState = request.app["state"]
    loop = asyncio.get_running_loop()
    diagnostics = await loop.run_in_executor(None, get_system_diagnostics)
    return web.json_response({"state": state.status_label, **diagnostics})


async def handle_audio(request: web.Request) -> web.Response:
    """
    POST /audio

    Receives a WAV audio payload (e.g. a TTS notification pushed by the
    server) and enqueues it for playback.  Returns immediately without
    waiting for playback to finish.
    """
    audio_data = await request.read()
    if not audio_data:
        return web.Response(status=400, text="Empty audio payload")

    play_queue: asyncio.Queue = request.app["play_queue"]
    logger.info("[HTTP] Received audio notification: %d bytes", len(audio_data))

    try:
        play_queue.put_nowait(audio_data)
    except asyncio.QueueFull:
        logger.warning("[HTTP] Play queue full; dropping audio notification.")
        return web.Response(status=503, text="Play queue full, try again later")

    return web.Response(status=200, text="queued")


# ---------------------------------------------------------------------------
# Server lifecycle
# ---------------------------------------------------------------------------


async def start_http_server(
    app: web.Application,
    host: str,
    port: int,
) -> web.AppRunner:
    """
    Start the aiohttp server on the running event loop.

    Returns the AppRunner so the caller can shut it down cleanly via
    runner.cleanup().
    """
    runner = web.AppRunner(app)
    await runner.setup()
    site = web.TCPSite(runner, host, port)
    await site.start()
    logger.info("[HTTP] Server listening on http://%s:%d", host, port)
    return runner
