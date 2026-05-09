"""
Wake word detection and command dispatch loop.

Runs as an asyncio Task on the shared event loop.  The sounddevice mic
callback fires on its own OS thread and pushes chunks into a deque; all
other work (wake word inference, recording, server dispatch) is done on
the event loop — CPU-bound pieces run in a thread executor so the loop
remains free to service HTTP requests.

Echo suppression: while state.speaking is True the buffer is drained and
wake word detection is skipped, preventing the node's own TTS output from
triggering a new command.
"""

import asyncio
import logging
import time
from collections import deque

import numpy as np
import sounddevice as sd
import openwakeword
from openwakeword.model import Model

from audio.utils import SAMPLE_RATE, CHANNELS, resample_to_16k
from audio.recorder import record_command
from audio.utils import encode_wav
from state import AppState
from server.remote_client import RemoteClient

logger = logging.getLogger(__name__)

# How many native-rate frames to keep in the deque at most.
# 5 seconds × (input_sample_rate / frame_samples) frames — capped so
# unbounded growth can't exhaust RAM on the Pi Zero 2 W.
# The actual maxlen is set at runtime once we know input_sample_rate.
_BUFFER_SECONDS = 5

# How often the async loop checks the buffer (20 ms matches one audio frame)
_POLL_INTERVAL = 0.02


class CommandListener:
    """
    Listens for the "alexa" wake word, records a command, sends it to the
    remote server, and enqueues the response audio for playback.

    Args:
        state:           Shared AppState instance.
        play_queue:      AudioPlayer queue; response audio is put here.
        remote_client:   Async remote server client.
        input_sample_rate: Native mic sample rate (Hz).
        input_device:    sounddevice device index (None = default).
        wakeword_threshold: Confidence score (0–1) to trigger detection.
        wakeword_cooldown:  Minimum seconds between successive detections.
        skip_ms:         Milliseconds of audio to discard after wake word.
        end_silence_threshold: RMS below which audio is considered silence.
        end_silence_duration:  Seconds of silence that ends a recording.
        max_recording_duration: Hard cap on recording length (seconds).
        recordings_output_dir: Directory to save command WAV files (or None).
    """

    def __init__(
        self,
        state: AppState,
        play_queue: asyncio.Queue,
        remote_client: RemoteClient,
        input_sample_rate: int,
        input_device: int | None,
        wakeword_threshold: float,
        wakeword_cooldown: float,
        skip_ms: float,
        end_silence_threshold: float,
        end_silence_duration: float,
        max_recording_duration: float,
        recordings_output_dir: str | None = None,
    ) -> None:
        self._state = state
        self._play_queue = play_queue
        self._remote = remote_client
        self._input_sample_rate = input_sample_rate
        self._input_device = input_device
        self._wakeword_threshold = wakeword_threshold
        self._wakeword_cooldown = wakeword_cooldown
        self._skip_ms = skip_ms
        self._end_silence_threshold = end_silence_threshold
        self._end_silence_duration = end_silence_duration
        self._max_recording_duration = max_recording_duration
        self._recordings_output_dir = recordings_output_dir

        # 20 ms frames at the native input rate
        self._input_frame_samples = int(input_sample_rate * 0.02)
        # 80 ms window required by openWakeWord
        self._target_input_frame_samples = int(input_sample_rate * 0.08)

        # Bounded deque — caps memory on constrained hardware
        max_buffer_frames = int(
            _BUFFER_SECONDS * input_sample_rate / self._input_frame_samples
        )
        self._buffer: deque[np.ndarray] = deque(maxlen=max_buffer_frames)
        self._buffer_samples = 0  # approximate, only written from callback

    # ------------------------------------------------------------------
    # sounddevice callback (runs on its own OS thread)
    # ------------------------------------------------------------------

    def _audio_callback(self, indata, frames, time_info, status) -> None:
        if status:
            logger.warning("[mic] %s", status)
        chunk = indata[:, 0].copy()
        self._buffer.append(chunk)
        self._buffer_samples += len(chunk)

    # ------------------------------------------------------------------
    # Public async entry point
    # ------------------------------------------------------------------

    async def run(self) -> None:
        """
        Download wake word models, open the mic stream, and run the
        detection loop.  Runs until cancelled.
        """
        logger.info("[wakeword] Downloading/verifying wake word models...")
        loop = asyncio.get_running_loop()
        await loop.run_in_executor(None, openwakeword.utils.download_models)

        model: Model = await loop.run_in_executor(None, Model)

        stream = sd.InputStream(
            samplerate=self._input_sample_rate,
            channels=CHANNELS,
            dtype="int16",
            blocksize=self._input_frame_samples,
            device=self._input_device,
            callback=self._audio_callback,
        )

        logger.info("[wakeword] Listening for wake word: 'alexa'...")
        logger.info(
            "[wakeword] Threshold: %.2f | Cooldown: %.1fs",
            self._wakeword_threshold,
            self._wakeword_cooldown,
        )
        if self._input_sample_rate != SAMPLE_RATE:
            logger.info(
                "[wakeword] Mic rate: %d Hz -> resampling to %d Hz",
                self._input_sample_rate,
                SAMPLE_RATE,
            )

        self._state.listening = True
        last_trigger = 0.0

        with stream:
            while True:
                await asyncio.sleep(_POLL_INTERVAL)

                # Echo suppression: skip processing while speaker is active
                if self._state.speaking:
                    self._buffer.clear()
                    self._buffer_samples = 0
                    continue

                if self._buffer_samples < self._target_input_frame_samples:
                    continue

                # Collect one 80 ms frame for the wake word model
                chunks = []
                collected = 0
                while self._buffer and collected < self._target_input_frame_samples:
                    chunk = self._buffer.popleft()
                    chunks.append(chunk)
                    collected += len(chunk)

                self._buffer_samples = max(0, self._buffer_samples - collected)
                frame = np.concatenate(chunks)[: self._target_input_frame_samples]

                # Wake word inference (CPU-bound — run in executor)
                frame_16k = resample_to_16k(frame, self._input_sample_rate)
                prediction = await loop.run_in_executor(None, model.predict, frame_16k)

                # openwakeword returns either a dict or a (dict, extra) tuple
                scores = prediction[0] if isinstance(prediction, tuple) else prediction
                score = float(scores.get("alexa", 0.0))

                now = time.time()
                if (
                    score >= self._wakeword_threshold
                    and (now - last_trigger) >= self._wakeword_cooldown
                ):
                    last_trigger = now
                    logger.info(
                        "[wakeword] Wake word detected (score=%.3f) — recording...",
                        score,
                    )
                    await self._handle_wake_word(loop)
                    last_trigger = time.time()  # reset after full round-trip
                    self._state.listening = True

    # ------------------------------------------------------------------
    # Wake word handler
    # ------------------------------------------------------------------

    async def _handle_wake_word(self, loop: asyncio.AbstractEventLoop) -> None:
        self._state.listening = False
        self._state.recording = True

        try:
            audio = await record_command(
                buffer=self._buffer,
                input_sample_rate=self._input_sample_rate,
                skip_ms=self._skip_ms,
                end_silence_threshold=self._end_silence_threshold,
                end_silence_duration=self._end_silence_duration,
                max_recording_duration=self._max_recording_duration,
            )
        finally:
            self._state.recording = False

        # Clear any audio that accumulated during recording
        self._buffer.clear()
        self._buffer_samples = 0

        if len(audio) == 0:
            logger.warning("[wakeword] No audio captured after wake word.")
            return

        # Optionally save the command to disk for debugging
        if self._recordings_output_dir:
            try:
                from audio.utils import save_wav

                path = await loop.run_in_executor(
                    None, save_wav, audio, self._recordings_output_dir
                )
                logger.debug("[recorder] Saved command to %s", path)
            except Exception:
                logger.exception("[recorder] Failed to save command audio")

        # Dispatch to remote server (async HTTP — does not block event loop)
        try:
            response_wav = await self._remote.dispatch_command(audio)
        except Exception:
            logger.exception("[command] Unexpected error dispatching command")
            return

        if response_wav:
            try:
                self._play_queue.put_nowait(response_wav)
            except asyncio.QueueFull:
                logger.warning("[command] Play queue full; dropping TTS response.")
