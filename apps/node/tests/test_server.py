import asyncio
import pytest
from aiohttp import web

from server.http_server import build_app
from state import AppState


@pytest.fixture
def state():
    """Fresh AppState for each test."""
    return AppState()


@pytest.fixture
async def client(aiohttp_client, state):
    """Create a test client for the aiohttp app with a dummy play queue."""
    play_queue: asyncio.Queue = asyncio.Queue()
    app = build_app(state, play_queue)
    return await aiohttp_client(app)


# ---------------------------------------------------------------------------
# GET /health
# ---------------------------------------------------------------------------


class TestHealth:
    async def test_returns_200(self, client):
        resp = await client.get("/health")
        assert resp.status == 200

    async def test_initializing_by_default(self, client):
        resp = await client.get("/health")
        data = await resp.json()
        assert data["status"] == "ok"
        assert data["state"] == "initializing"

    async def test_listening_state(self, client, state):
        state.listening = True
        resp = await client.get("/health")
        data = await resp.json()
        assert data["state"] == "listening"

    async def test_recording_state(self, client, state):
        state.recording = True
        resp = await client.get("/health")
        data = await resp.json()
        assert data["state"] == "recording"

    async def test_recording_takes_priority_over_listening(self, client, state):
        state.listening = True
        state.recording = True
        resp = await client.get("/health")
        data = await resp.json()
        assert data["state"] == "recording"


# ---------------------------------------------------------------------------
# POST /audio
# ---------------------------------------------------------------------------


class TestAudio:
    async def test_empty_payload_returns_400(self, client):
        resp = await client.post("/audio", data=b"")
        assert resp.status == 400

    async def test_valid_payload_returns_200_and_enqueues(self, aiohttp_client, state):
        """Audio data should be enqueued without blocking."""
        play_queue: asyncio.Queue = asyncio.Queue()
        app = build_app(state, play_queue)
        cl = await aiohttp_client(app)

        dummy_wav = b"\x00" * 64
        resp = await cl.post("/audio", data=dummy_wav)
        assert resp.status == 200
        assert play_queue.qsize() == 1
        assert play_queue.get_nowait() == dummy_wav

    async def test_full_queue_returns_503(self, aiohttp_client, state):
        """When the play queue is full the server should return 503."""
        play_queue: asyncio.Queue = asyncio.Queue(maxsize=1)
        play_queue.put_nowait(b"already full")
        app = build_app(state, play_queue)
        cl = await aiohttp_client(app)

        resp = await cl.post("/audio", data=b"\x00" * 64)
        assert resp.status == 503


# ---------------------------------------------------------------------------
# Unknown routes
# ---------------------------------------------------------------------------


class TestRouting:
    async def test_unknown_route_returns_404(self, client):
        resp = await client.get("/nonexistent")
        assert resp.status == 404
