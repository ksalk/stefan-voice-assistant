import pytest
from aiohttp import web

from server import build_app
from state import node_state


@pytest.fixture
async def client(aiohttp_client):
    """Create a test client for the aiohttp app."""
    app = build_app()
    return await aiohttp_client(app)


@pytest.fixture(autouse=True)
def reset_state():
    """Reset node_state to defaults before each test."""
    node_state["listening"] = False
    node_state["recording"] = False
    node_state["speaking"] = False
    yield


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

    async def test_listening_state(self, client):
        node_state["listening"] = True
        resp = await client.get("/health")
        data = await resp.json()
        assert data["state"] == "listening"

    async def test_recording_state(self, client):
        node_state["recording"] = True
        resp = await client.get("/health")
        data = await resp.json()
        assert data["state"] == "recording"

    async def test_recording_takes_priority_over_listening(self, client):
        node_state["listening"] = True
        node_state["recording"] = True
        resp = await client.get("/health")
        data = await resp.json()
        assert data["state"] == "recording"


# ---------------------------------------------------------------------------
# POST /text
# ---------------------------------------------------------------------------

class TestText:
    async def test_json_body(self, client):
        resp = await client.post("/text", json={"text": "hello world"})
        assert resp.status == 200

    async def test_plain_text_body(self, client):
        resp = await client.post(
            "/text",
            data="hello world",
            headers={"Content-Type": "text/plain"},
        )
        assert resp.status == 200

    async def test_empty_json_text_returns_400(self, client):
        resp = await client.post("/text", json={"text": ""})
        assert resp.status == 400

    async def test_missing_text_key_returns_400(self, client):
        resp = await client.post("/text", json={"foo": "bar"})
        assert resp.status == 400

    async def test_empty_plain_text_returns_400(self, client):
        resp = await client.post(
            "/text",
            data="",
            headers={"Content-Type": "text/plain"},
        )
        assert resp.status == 400

    async def test_invalid_json_returns_400(self, client):
        resp = await client.post(
            "/text",
            data="{bad json",
            headers={"Content-Type": "application/json"},
        )
        assert resp.status == 400


# ---------------------------------------------------------------------------
# Unknown routes
# ---------------------------------------------------------------------------

class TestRouting:
    async def test_unknown_route_returns_404(self, client):
        resp = await client.get("/nonexistent")
        assert resp.status == 404
