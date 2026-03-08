"""
Minimal WebSocket client for the Stefan assistant node.

Connects to the .NET server over WSS, sends a node_ready handshake, then holds
the connection open waiting for incoming messages (server-push).

Authentication uses a shared secret passed as the X-Node-Secret HTTP header
during the WebSocket upgrade request.

Run directly to test the connection:
    NODE_SECRET=dev-secret-change-me python src/ws_client.py --server-ws-url wss://localhost:7036/ws
"""

import asyncio
import json
import argparse
import os
import ssl
import threading

import websockets

from config import remoteServerConfig, localServerConfig

# ---------------------------------------------------------------------------
# SSL helpers
# ---------------------------------------------------------------------------

def _build_ssl_context(no_verify: bool) -> ssl.SSLContext:
    """Return an SSL context for WSS connections.

    Args:
        no_verify: When True, skip certificate verification (dev / self-signed
                   certs only). Never use in production.
    """
    ctx = ssl.create_default_context()
    if no_verify:
        ctx.check_hostname = False
        ctx.verify_mode = ssl.CERT_NONE
    return ctx


# ---------------------------------------------------------------------------
# Protocol helpers
# ---------------------------------------------------------------------------

def _node_ready_msg(device_id: str) -> str:
    return json.dumps({
        "type": "node_ready",
        "payload": {
            "nodeId": device_id,
        },
    })


# ---------------------------------------------------------------------------
# Client
# ---------------------------------------------------------------------------

async def run(server_ws_url: str, secret: str, device_id: str, ssl_no_verify: bool = False) -> None:
    print(f"[ws] Node ID: {device_id}")
    print(f"[ws] Connecting to {server_ws_url}...")

    ssl_context = _build_ssl_context(ssl_no_verify) if server_ws_url.startswith("wss://") else None

    async with websockets.connect(
        server_ws_url,
        ssl=ssl_context,
        additional_headers={"X-Node-Secret": secret},
    ) as ws:
        print("[ws] Connected.")

        # Send handshake
        msg = _node_ready_msg(device_id)
        await ws.send(msg)
        print(f"[ws] Sent: {msg}")

        # Wait for incoming messages (server-push, future use)
        async for raw in ws:
            print(f"[ws] Received: {raw}")


def start_ws_thread(server_ws_url: str, secret: str, device_id: str, ssl_no_verify: bool = False) -> threading.Thread:
    """Run the WebSocket client in a daemon thread with its own event loop."""
    def _target():
        asyncio.run(run(server_ws_url, secret, device_id, ssl_no_verify))

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
    parser.add_argument(
        "--node-secret",
        default=config.NODE_SECRET,
        help="Shared secret for X-Node-Secret header (default: $NODE_SECRET env var)",
    )
    parser.add_argument(
        "--ssl-verify",
        action="store_true",
        help="Enable TLS certificate verification (use with a trusted/production cert)",
    )
    args = parser.parse_args()

    asyncio.run(run(args.server_ws_url, args.node_secret, ssl_no_verify=not args.ssl_verify))
