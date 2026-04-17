"""
Remote server HTTP client.

Uses aiohttp.ClientSession for async, non-blocking HTTP.  A single shared
session is passed in at construction time for connection pooling (important
on a Pi Zero 2 W where establishing TLS connections is expensive).

SSL verification is disabled by default because the .NET dev server uses a
self-signed certificate.  Set REMOTE_SERVER_SSL_VERIFY=true in .env for
production deployments with a valid cert.
"""

import asyncio
import io
import logging
import time
from urllib.parse import unquote
import wave

import aiohttp
import numpy as np

from audio.utils import SAMPLE_RATE, CHANNELS

logger = logging.getLogger(__name__)

COMMAND_ENDPOINT = "/commands"
REGISTER_NODE_ENDPOINT = "/nodes/register"

# Timeouts (seconds)
REGISTER_TIMEOUT = 10
COMMAND_TIMEOUT = 60  # STT + LLM + TTS can take a while on slow servers


class RemoteClient:
    """
    Async HTTP client for communicating with the central server.

    Args:
        session:    Shared aiohttp.ClientSession.
        server_url: Base URL of the remote server API (e.g. "http://host/api").
        auth_secret: Shared secret sent in X-Node-Secret header.
        node_name:  Human-readable node identifier.
        session_id: UUID for this run, sent in X-Node-Session-ID header.
        local_port: Port this node's local HTTP server is listening on.
        ssl_verify: Whether to verify TLS certificates.
    """

    def __init__(
        self,
        session: aiohttp.ClientSession,
        server_url: str,
        auth_secret: str,
        node_name: str,
        session_id: str,
        local_port: int,
        ssl_verify: bool = False,
    ) -> None:
        self._session = session
        self._server_url = server_url.rstrip("/")
        self._auth_secret = auth_secret
        self._node_name = node_name
        self._session_id = session_id
        self._local_port = local_port
        self._ssl = None if ssl_verify else False  # aiohttp: False = skip verify

    def _headers(self) -> dict[str, str]:
        return {
            "X-Node-Secret": self._auth_secret,
            "X-Node-Device-ID": self._node_name,
            "X-Node-Session-ID": self._session_id,
        }

    async def register_node(self) -> bool:
        """
        Register this node with the server.  Returns True on success.
        """
        logger.info(
            "[registration] Registering node '%s' with server at %s...",
            self._node_name,
            self._server_url,
        )
        url = f"{self._server_url}{REGISTER_NODE_ENDPOINT}"
        payload = {
            "NodeName": self._node_name,
            "SessionId": self._session_id,
            "Port": self._local_port,
        }
        headers = {"X-Node-Secret": self._auth_secret}
        try:
            timeout = aiohttp.ClientTimeout(total=REGISTER_TIMEOUT)
            async with self._session.post(
                url,
                json=payload,
                headers=headers,
                ssl=self._ssl,
                timeout=timeout,
            ) as resp:
                if resp.status == 200:
                    logger.info("[registration] Node registered successfully.")
                    return True
                text = await resp.text()
                logger.error(
                    "[registration] Failed: server returned %d — %s",
                    resp.status,
                    text.strip(),
                )
                return False
        except aiohttp.ClientError as exc:
            logger.error("[registration] Failed to register node: %s", exc)
            return False
        except asyncio.TimeoutError:
            logger.error("[registration] Timed out after %ds.", REGISTER_TIMEOUT)
            return False

    async def dispatch_command(self, command_audio: np.ndarray) -> bytes | None:
        """
        Encode audio as a WAV and POST it to the server's /commands endpoint.

        Returns the raw WAV response bytes on success, or None on failure.
        The caller is responsible for playing the audio.
        """
        logger.info("[command] Dispatching command audio to %s...", self._server_url)
        op_start = time.time()

        # Encode audio to an in-memory WAV
        wav_buf = io.BytesIO()
        with wave.open(wav_buf, "wb") as wf:
            wf.setnchannels(CHANNELS)
            wf.setsampwidth(2)
            wf.setframerate(SAMPLE_RATE)
            wf.writeframes(command_audio.tobytes())
        wav_buf.seek(0)

        url = f"{self._server_url}{COMMAND_ENDPOINT}"
        form = aiohttp.FormData()
        form.add_field(
            "file",
            wav_buf,
            filename="command.wav",
            content_type="audio/wav",
        )

        try:
            timeout = aiohttp.ClientTimeout(total=COMMAND_TIMEOUT)
            http_start = time.time()
            async with self._session.post(
                url,
                data=form,
                headers=self._headers(),
                ssl=self._ssl,
                timeout=timeout,
            ) as resp:
                http_elapsed = time.time() - http_start
                response_text = unquote(resp.headers.get("X-Response-Text", ""))

                if resp.status == 200:
                    content = await resp.read()
                    if content:
                        if response_text:
                            logger.info("[assistant] %s", response_text)
                        time_to_response = time.time() - op_start
                        logger.info(
                            "[timing] http: %.2fs | total: %.2fs",
                            http_elapsed,
                            time_to_response,
                        )
                        return content
                    logger.warning("[command] Server returned 200 but empty body.")
                    return None
                else:
                    text = await resp.text()
                    logger.error(
                        "[command] Server returned %d — %s",
                        resp.status,
                        text.strip(),
                    )
                    return None
        except aiohttp.ClientError as exc:
            logger.error("[command] Request failed: %s", exc)
            return None
        except asyncio.TimeoutError:
            logger.error("[command] Request timed out after %ds.", COMMAND_TIMEOUT)
            return None
