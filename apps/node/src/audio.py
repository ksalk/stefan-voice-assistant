import io
import os
import time
import wave
from collections import deque
from datetime import datetime

import numpy as np
import sounddevice as sd
from scipy.signal import resample_poly
from math import gcd

from config import audioConfig, audioOutputConfig

# ---------------------------------------------------------------------------
# Audio constants
# ---------------------------------------------------------------------------

SAMPLE_RATE = 16000  # Target rate for OpenWakeWord and Whisper
CHANNELS = 1
FRAME_MS = 20
FRAME_SAMPLES = int(SAMPLE_RATE * FRAME_MS / 1000)

INPUT_SAMPLE_RATE = audioConfig.INPUT_SAMPLE_RATE  # Mic native sample rate
INPUT_FRAME_SAMPLES = int(INPUT_SAMPLE_RATE * FRAME_MS / 1000)

OUTPUT_SAMPLE_RATE = audioOutputConfig.SAMPLE_RATE  # Speaker native sample rate

# Pre-compute resampling ratio (used by resample_to_16k)
_RESAMPLE_GCD = gcd(INPUT_SAMPLE_RATE, SAMPLE_RATE)
_RESAMPLE_UP = SAMPLE_RATE // _RESAMPLE_GCD
_RESAMPLE_DOWN = INPUT_SAMPLE_RATE // _RESAMPLE_GCD
_NEEDS_RESAMPLE = INPUT_SAMPLE_RATE != SAMPLE_RATE


def resample_to_16k(audio: np.ndarray) -> np.ndarray:
    """
    Resample an int16 audio array from INPUT_SAMPLE_RATE to 16 kHz.
    Returns the audio unchanged if the rates already match.
    """
    if not _NEEDS_RESAMPLE:
        return audio
    resampled = resample_poly(audio.astype(np.float64), _RESAMPLE_UP, _RESAMPLE_DOWN)
    return np.clip(resampled, -32768, 32767).astype(np.int16)


# ---------------------------------------------------------------------------
# Audio playback (for server-synthesized WAV)
# ---------------------------------------------------------------------------


def play_audio(wav_data: bytes, node_state: dict) -> float:
    """
    Play a WAV byte buffer through the default output device using sounddevice.
    Resamples to OUTPUT_SAMPLE_RATE if the WAV sample rate differs.
    Blocks until playback is complete.

    Returns the timestamp at which playback started.
    """
    node_state["speaking"] = True
    try:
        buf = io.BytesIO(wav_data)
        with wave.open(buf, "rb") as wf:
            sample_rate = wf.getframerate()
            n_channels = wf.getnchannels()
            sample_width = wf.getsampwidth()
            raw_frames = wf.readframes(wf.getnframes())

        # Convert raw bytes to numpy array based on sample width
        if sample_width == 2:
            audio = (
                np.frombuffer(raw_frames, dtype=np.int16).astype(np.float32) / 32768.0
            )
        elif sample_width == 4:
            audio = (
                np.frombuffer(raw_frames, dtype=np.int32).astype(np.float32)
                / 2147483648.0
            )
        else:
            raise ValueError(f"Unsupported sample width: {sample_width}")

        # Reshape for multi-channel audio
        if n_channels > 1:
            audio = audio.reshape(-1, n_channels)

        # Resample to the speaker's native rate if they differ
        if sample_rate != OUTPUT_SAMPLE_RATE:
            g = gcd(OUTPUT_SAMPLE_RATE, sample_rate)
            audio = resample_poly(
                audio, OUTPUT_SAMPLE_RATE // g, sample_rate // g
            ).astype(np.float32)

        speaking_start = time.time()
        sd.play(audio, samplerate=OUTPUT_SAMPLE_RATE)
        sd.wait()
    finally:
        node_state["speaking"] = False
    return speaking_start


# ---------------------------------------------------------------------------
# Device utilities
# ---------------------------------------------------------------------------


def list_devices():
    devices = sd.query_devices()
    for i, dev in enumerate(devices):
        caps = []
        if dev["max_input_channels"] > 0:
            caps.append("in")
        if dev["max_output_channels"] > 0:
            caps.append("out")
        io_str = "/".join(caps) if caps else "none"
        print(f"{i}: {dev['name']} ({io_str})")


# ---------------------------------------------------------------------------
# Recording
# ---------------------------------------------------------------------------


def record_command(
    buffer: deque,
    buffer_samples: int,
) -> tuple:
    """
    Drain the shared audio buffer and continue capturing until silence or max
    duration is reached.

    The buffer contains audio at INPUT_SAMPLE_RATE.  All sample-count
    thresholds are therefore computed relative to that rate.  The final
    audio is resampled to 16 kHz before being returned.

    The first `skip_ms` milliseconds of audio are discarded to avoid capturing
    the tail of the wake word in the command recording.

    Returns:
        (audio: np.ndarray[int16] at 16 kHz, updated_buffer_samples: int)
    """
    recorded_chunks = []
    silence_samples = 0
    silence_samples_limit = int(INPUT_SAMPLE_RATE * audioConfig.END_SILENCE_DURATION)
    max_samples = int(INPUT_SAMPLE_RATE * audioConfig.MAX_RECORDING_DURATION)
    total_recorded = 0
    got_speech = False

    # Discard the first skip_ms ms of audio to drop the wake word tail.
    # We consume from the buffer (and wait for new chunks if needed) until
    # skip_samples worth of audio has been thrown away.
    skip_samples = int(INPUT_SAMPLE_RATE * audioConfig.SKIP_MS_AFTER_WAKEWORD / 1000)
    skipped = 0
    while skipped < skip_samples:
        if not buffer:
            time.sleep(0.005)
            continue
        chunk = buffer.popleft()
        buffer_samples -= len(chunk)
        skipped += len(chunk)

    # Drain whatever is now in the buffer (audio right after the skip window)
    while buffer:
        chunk = buffer.popleft()
        recorded_chunks.append(chunk)
        total_recorded += len(chunk)
    buffer_samples -= (
        total_recorded  # sync the counter (nonlocal update handled by caller)
    )

    record_start = time.time()

    while True:
        if total_recorded >= max_samples:
            print("Max recording duration reached.")
            break

        # Wait for new audio to arrive in the buffer
        if not buffer:
            time.sleep(0.005)
            continue

        chunk = buffer.popleft()
        buffer_samples -= len(chunk)
        recorded_chunks.append(chunk)
        total_recorded += len(chunk)

        rms = float(np.sqrt(np.mean(chunk.astype(np.float32) ** 2)))

        if rms >= audioConfig.END_SILENCE_THRESHOLD:
            got_speech = True
            silence_samples = 0
        else:
            if got_speech:
                silence_samples += len(chunk)
                if silence_samples >= silence_samples_limit:
                    break

    if not recorded_chunks:
        return np.array([], dtype=np.int16), buffer_samples

    audio = np.concatenate(recorded_chunks)
    audio = resample_to_16k(audio)
    return audio, buffer_samples


def load_wav(path: str) -> np.ndarray:
    """Load a WAV file from disk and return it as a numpy int16 array."""
    with wave.open(path, "rb") as wf:
        raw = wf.readframes(wf.getnframes())
    return np.frombuffer(raw, dtype=np.int16)


def save_wav(audio: np.ndarray, output_dir: str) -> str:
    """Write a numpy int16 audio array to a timestamped WAV file."""
    os.makedirs(output_dir, exist_ok=True)
    timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    filename = os.path.join(output_dir, f"command_{timestamp}.wav")
    with wave.open(filename, "wb") as wf:
        wf.setnchannels(CHANNELS)
        wf.setsampwidth(2)  # int16 = 2 bytes
        wf.setframerate(SAMPLE_RATE)
        wf.writeframes(audio.tobytes())
    return filename
