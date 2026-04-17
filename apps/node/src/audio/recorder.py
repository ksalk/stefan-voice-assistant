"""
Command recording logic.

Captures audio from a shared deque (populated by the sounddevice callback)
until silence is detected or the max recording duration is reached.

This module contains only pure/blocking logic — callers are responsible for
running it in a thread executor if they don't want to block the event loop.
"""

import asyncio
import logging
import time
from collections import deque

import numpy as np

from audio.utils import resample_to_16k

logger = logging.getLogger(__name__)


async def record_command(
    buffer: deque,
    input_sample_rate: int,
    skip_ms: float,
    end_silence_threshold: float,
    end_silence_duration: float,
    max_recording_duration: float,
) -> np.ndarray:
    """
    Async wrapper: runs _record_command_blocking in a thread executor so the
    event loop stays responsive while waiting for audio.

    Returns a 16 kHz int16 numpy array (may be empty if no speech captured).
    """
    loop = asyncio.get_running_loop()
    return await loop.run_in_executor(
        None,
        _record_command_blocking,
        buffer,
        input_sample_rate,
        skip_ms,
        end_silence_threshold,
        end_silence_duration,
        max_recording_duration,
    )


def _record_command_blocking(
    buffer: deque,
    input_sample_rate: int,
    skip_ms: float,
    end_silence_threshold: float,
    end_silence_duration: float,
    max_recording_duration: float,
) -> np.ndarray:
    """
    Drain the shared audio buffer and continue capturing until silence or max
    duration.

    Buffer contains audio at input_sample_rate.  The returned array is
    resampled to 16 kHz.

    The first skip_ms milliseconds are discarded to avoid capturing the tail
    of the wake word in the command.
    """
    recorded_chunks: list[np.ndarray] = []
    silence_samples = 0
    silence_samples_limit = int(input_sample_rate * end_silence_duration)
    max_samples = int(input_sample_rate * max_recording_duration)
    total_recorded = 0
    got_speech = False

    # Discard skip_ms worth of audio to drop the wake-word tail.
    skip_samples = int(input_sample_rate * skip_ms / 1000)
    skipped = 0
    while skipped < skip_samples:
        if not buffer:
            time.sleep(0.005)
            continue
        chunk = buffer.popleft()
        skipped += len(chunk)

    # Drain whatever arrived right after the skip window.
    while buffer:
        chunk = buffer.popleft()
        recorded_chunks.append(chunk)
        total_recorded += len(chunk)

    logger.debug("[recorder] Recording command (max %.1fs)...", max_recording_duration)
    record_start = time.time()

    while True:
        if total_recorded >= max_samples:
            logger.debug("[recorder] Max recording duration reached.")
            break

        if not buffer:
            time.sleep(0.005)
            continue

        chunk = buffer.popleft()
        recorded_chunks.append(chunk)
        total_recorded += len(chunk)

        rms = float(np.sqrt(np.mean(chunk.astype(np.float32) ** 2)))

        if rms >= end_silence_threshold:
            got_speech = True
            silence_samples = 0
        else:
            if got_speech:
                silence_samples += len(chunk)
                if silence_samples >= silence_samples_limit:
                    break

    logger.debug("[recorder] Captured %.2fs of audio.", time.time() - record_start)

    if not recorded_chunks:
        return np.array([], dtype=np.int16)

    audio = np.concatenate(recorded_chunks)

    # Trim trailing silence so we don't send a long silent tail to the server.
    trim_samples = int(silence_samples_limit * 0.9)
    if trim_samples > 0 and len(audio) > trim_samples:
        audio = audio[:-trim_samples]

    return resample_to_16k(audio, input_sample_rate)
