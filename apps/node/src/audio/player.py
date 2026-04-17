"""
AudioPlayer — consumes WAV payloads from an asyncio.Queue and plays them
sequentially through the default output device.

Design decisions:
  - A single task drains the queue so playback is always sequential (no
    overlap / no concurrent sd.play calls, which sounddevice doesn't support).
  - The blocking sd.play() + sd.wait() calls run in a ThreadPoolExecutor so
    the event loop is never blocked during playback.
  - state.speaking is set/cleared around each playback with a finally guard
    so it is always accurate even if an exception is raised.
  - The queue has a bounded maxsize so a runaway caller can't queue unlimited
    audio on a memory-constrained device (Pi Zero 2 W has 512 MB total RAM).
"""

import asyncio
import logging
import time
from math import gcd

import numpy as np
import sounddevice as sd

from audio.utils import decode_wav

logger = logging.getLogger(__name__)

# Maximum number of pending audio payloads in the queue.
# Each TTS response is typically 20–200 KB; 10 items ≈ at most ~2 MB.
PLAY_QUEUE_MAXSIZE = 10


class AudioPlayer:
    """
    Async audio player that serialises WAV playback through a queue.

    Usage:
        player = AudioPlayer(state, output_sample_rate)
        # Pass player.queue to anything that wants to schedule audio.
        asyncio.create_task(player.run())
    """

    def __init__(self, state, output_sample_rate: int) -> None:
        self._state = state
        self._output_sample_rate = output_sample_rate
        self.queue: asyncio.Queue[bytes] = asyncio.Queue(maxsize=PLAY_QUEUE_MAXSIZE)

    async def run(self) -> None:
        """
        Main loop: wait for audio on the queue and play it.
        Runs forever; cancel the task to stop it.
        """
        loop = asyncio.get_running_loop()
        while True:
            wav_data = await self.queue.get()
            try:
                await loop.run_in_executor(None, self._play_blocking, wav_data)
            except Exception:
                logger.exception("[player] Error during audio playback")
            finally:
                self.queue.task_done()

    def _play_blocking(self, wav_data: bytes) -> None:
        """
        Decode and play a WAV payload.  Runs in a thread executor so the
        asyncio event loop is not blocked.
        """
        self._state.speaking = True
        speaking_start = time.time()
        try:
            audio, sample_rate, _ = decode_wav(wav_data)

            # Resample to the speaker's native rate if they differ
            if sample_rate != self._output_sample_rate:
                g = gcd(self._output_sample_rate, sample_rate)
                from scipy.signal import (
                    resample_poly,
                )  # local import to keep utils lean

                audio = resample_poly(
                    audio, self._output_sample_rate // g, sample_rate // g
                ).astype(np.float32)

            sd.play(audio, samplerate=self._output_sample_rate)
            sd.wait()
            logger.debug(
                "[player] Playback finished in %.2fs", time.time() - speaking_start
            )
        finally:
            self._state.speaking = False
