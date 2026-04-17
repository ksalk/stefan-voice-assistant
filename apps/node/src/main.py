#!/usr/bin/env python3
"""
Application entry point.

Parses CLI arguments, wires up shared resources, and runs all async tasks
concurrently on a single asyncio event loop:

  - AudioPlayer task  — consumes WAV payloads from play_queue, plays them
  - HTTP server task  — aiohttp web app (health, status, /audio push)
  - CommandListener task — mic wake word loop + server dispatch

Tasks communicate via asyncio.Queue (play_queue) and a shared AppState
object.  No inter-thread or inter-process communication is needed.
"""

import argparse
import asyncio
import logging
import signal
import sys

import aiohttp

from config import (
    AudioInputConfig,
    AudioOutputConfig,
    LocalServerConfig,
    NodeConfig,
    RemoteServerConfig,
)
from state import AppState
from audio import AudioPlayer, list_devices, load_wav
from listener import CommandListener
from server.http_server import build_app, start_http_server
from server.remote_client import RemoteClient

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s %(levelname)-8s %(message)s",
    datefmt="%H:%M:%S",
)
logger = logging.getLogger(__name__)


# ---------------------------------------------------------------------------
# CLI
# ---------------------------------------------------------------------------


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Voice assistant node (openWakeWord)")

    # Node
    parser.add_argument("--node-name", type=str, help="Unique name for this node")

    # Audio input
    parser.add_argument(
        "--audio-device", type=int, default=None, help="Input device index"
    )
    parser.add_argument(
        "--audio-max-recording-duration",
        type=float,
        help="Max recording duration in seconds",
    )
    parser.add_argument("--output-dir", type=str, help="Directory for saved recordings")

    # Remote server
    parser.add_argument(
        "--remote-server-url", type=str, help="Remote server API base URL"
    )
    parser.add_argument("--node-secret", type=str, help="Shared secret for server auth")

    # Local HTTP server
    parser.add_argument("--local-server-host", type=str, help="HTTP server bind host")
    parser.add_argument("--local-server-port", type=int, help="HTTP server port")

    # Utility modes
    parser.add_argument(
        "--list-devices", action="store_true", help="List audio devices and exit"
    )
    parser.add_argument(
        "--test-command",
        type=str,
        default=None,
        metavar="WAV_FILE",
        help="Send a WAV file directly to the server and exit (skips microphone)",
    )

    return parser.parse_args()


def _apply_cli_overrides(args: argparse.Namespace) -> None:
    """Patch pydantic-settings config singletons with any CLI-provided values."""
    from config import audioConfig, localServerConfig, nodeConfig, remoteServerConfig

    if args.node_name is not None:
        nodeConfig.NAME = args.node_name
    if args.audio_device is not None:
        audioConfig.INPUT_DEVICE = args.audio_device
    if args.audio_max_recording_duration is not None:
        audioConfig.MAX_RECORDING_DURATION = args.audio_max_recording_duration
    if args.output_dir is not None:
        audioConfig.RECORDINGS_OUTPUT_DIR = args.output_dir
    if args.remote_server_url is not None:
        remoteServerConfig.URL = args.remote_server_url
    if args.node_secret is not None:
        remoteServerConfig.AUTH_SECRET = args.node_secret
    if args.local_server_host is not None:
        localServerConfig.HOST = args.local_server_host
    if args.local_server_port is not None:
        localServerConfig.PORT = args.local_server_port


# ---------------------------------------------------------------------------
# Async main
# ---------------------------------------------------------------------------


async def async_main() -> None:
    args = parse_args()
    _apply_cli_overrides(args)

    # Import config singletons after CLI overrides are applied
    from config import (
        audioConfig,
        audioOutputConfig,
        localServerConfig,
        nodeConfig,
        remoteServerConfig,
    )

    if args.list_devices:
        list_devices()
        return

    # Shared state and play queue
    state = AppState()
    player = AudioPlayer(state, output_sample_rate=audioOutputConfig.SAMPLE_RATE)

    # Single shared aiohttp session (connection pooling, important on Pi Zero)
    async with aiohttp.ClientSession() as http_session:
        remote_client = RemoteClient(
            session=http_session,
            server_url=remoteServerConfig.URL,
            auth_secret=remoteServerConfig.AUTH_SECRET,
            node_name=nodeConfig.NAME,
            session_id=nodeConfig.SESSION_ID,
            local_port=localServerConfig.PORT,
            ssl_verify=remoteServerConfig.SSL_VERIFY,
        )

        # ------------------------------------------------------------------
        # Register node before any work, then start all tasks
        # ------------------------------------------------------------------
        if not await remote_client.register_node():
            logger.critical("[fatal] Node registration failed. Exiting.")
            sys.exit(1)

        # ------------------------------------------------------------------
        # Test-command mode: dispatch a single WAV file and exit
        # ------------------------------------------------------------------
        if args.test_command:
            logger.info("[test] Loading %s", args.test_command)
            audio = load_wav(args.test_command)
            response_wav = await remote_client.dispatch_command(audio)
            if response_wav:
                player._play_blocking(response_wav)  # blocking ok here (single shot)
            return

        # Build the HTTP app (wires state + play_queue into route handlers)
        http_app = build_app(state, player.queue)

        listener = CommandListener(
            state=state,
            play_queue=player.queue,
            remote_client=remote_client,
            input_sample_rate=audioConfig.INPUT_SAMPLE_RATE,
            input_device=audioConfig.INPUT_DEVICE,
            wakeword_threshold=audioConfig.WAKEWORD_THRESHOLD,
            wakeword_cooldown=audioConfig.WAKEWORD_COOLDOWN,
            skip_ms=audioConfig.SKIP_MS_AFTER_WAKEWORD,
            end_silence_threshold=audioConfig.END_SILENCE_THRESHOLD,
            end_silence_duration=audioConfig.END_SILENCE_DURATION,
            max_recording_duration=audioConfig.MAX_RECORDING_DURATION,
            recordings_output_dir=audioConfig.RECORDINGS_OUTPUT_DIR,
        )

        # Start HTTP server (returns runner for clean shutdown)
        http_runner = await start_http_server(
            http_app, localServerConfig.HOST, localServerConfig.PORT
        )

        # Create async tasks
        tasks = [
            asyncio.create_task(player.run(), name="audio-player"),
            asyncio.create_task(listener.run(), name="command-listener"),
        ]

        # Graceful shutdown on SIGINT / SIGTERM
        loop = asyncio.get_running_loop()
        stop_event = asyncio.Event()

        def _shutdown(sig_name: str) -> None:
            logger.info("[main] Received %s, shutting down...", sig_name)
            stop_event.set()

        for sig in (signal.SIGINT, signal.SIGTERM):
            loop.add_signal_handler(sig, _shutdown, sig.name)

        try:
            # Run until a shutdown signal is received or a task crashes
            done, pending = await asyncio.wait(
                [asyncio.create_task(stop_event.wait()), *tasks],
                return_when=asyncio.FIRST_COMPLETED,
            )

            # If a worker task ended (crash or normal), log it
            for task in done:
                if task.get_name() in ("audio-player", "command-listener"):
                    exc = task.exception() if not task.cancelled() else None
                    if exc:
                        logger.error(
                            "[main] Task '%s' crashed: %s", task.get_name(), exc
                        )

        finally:
            # Cancel remaining tasks
            for task in tasks:
                task.cancel()
            await asyncio.gather(*tasks, return_exceptions=True)

            # Tear down HTTP server
            await http_runner.cleanup()
            logger.info("[main] Shutdown complete.")


# ---------------------------------------------------------------------------
# Entry point
# ---------------------------------------------------------------------------


def main() -> None:
    asyncio.run(async_main())


if __name__ == "__main__":
    main()
