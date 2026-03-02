"""
Minimal WebSocket client for the Stefan assistant node.

Connects to the .NET server, sends a node_ready handshake, then holds
the connection open waiting for incoming messages (server-push).

Run directly to test the connection:
    python src/ws_client.py --server-ws-url ws://localhost:5285/ws
"""

import asyncio
import json
import argparse
import uuid
import threading

import websockets

DEFAULT_SERVER_WS_URL = "ws://localhost:5285/ws"

# Generated once at startup, stable for the lifetime of this process.
NODE_ID = str(uuid.uuid4())


# ---------------------------------------------------------------------------
# Protocol helpers
# ---------------------------------------------------------------------------

def _node_ready_msg() -> str:
    return json.dumps({
        "type": "node_ready",
        "payload": {
            "nodeId": NODE_ID,
        },
    })


# ---------------------------------------------------------------------------
# Client
# ---------------------------------------------------------------------------

async def run(server_ws_url: str) -> None:
    print(f"[ws] Node ID: {NODE_ID}")
    print(f"[ws] Connecting to {server_ws_url}...")

    async with websockets.connect(server_ws_url) as ws:
        print("[ws] Connected.")

        # Send handshake
        msg = _node_ready_msg()
        await ws.send(msg)
        print(f"[ws] Sent: {msg}")

        # Wait for incoming messages (server-push, future use)
        async for raw in ws:
            print(f"[ws] Received: {raw}")


def start_ws_thread(server_ws_url: str) -> threading.Thread:
    """Run the WebSocket client in a daemon thread with its own event loop."""
    def _target():
        asyncio.run(run(server_ws_url))

    thread = threading.Thread(
        target=_target,
        daemon=True,
        name="ws-client",
    )
    thread.start()
    return thread


# ---------------------------------------------------------------------------
# Entry point (for manual testing)
# ---------------------------------------------------------------------------

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="WebSocket client test")
    parser.add_argument("--server-ws-url", default=DEFAULT_SERVER_WS_URL)
    args = parser.parse_args()

    asyncio.run(run(args.server_ws_url))
