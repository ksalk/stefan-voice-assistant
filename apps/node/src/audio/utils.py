"""
Audio utility functions and constants.

Constants are computed lazily via get_audio_constants() to avoid the
module-level freeze bug (issue #10) where constants were computed at
import time before CLI overrides could be applied.
"""

import io
import os
import wave
from datetime import datetime
from math import gcd

import numpy as np
import sounddevice as sd
from scipy.signal import resample_poly

# ---------------------------------------------------------------------------
# Fixed constants (not config-dependent)
# ---------------------------------------------------------------------------

SAMPLE_RATE = 16000  # Target rate for OpenWakeWord and Whisper
CHANNELS = 1
FRAME_MS = 20
FRAME_SAMPLES = int(SAMPLE_RATE * FRAME_MS / 1000)  # 320 samples @ 16kHz


# ---------------------------------------------------------------------------
# Resampling
# ---------------------------------------------------------------------------


def resample_audio(
    audio: np.ndarray,
    from_rate: int,
    to_rate: int,
) -> np.ndarray:
    """
    Resample an int16 audio array between arbitrary sample rates using
    polyphase filtering. Returns the array unchanged if rates match.
    """
    if from_rate == to_rate:
        return audio
    g = gcd(from_rate, to_rate)
    up = to_rate // g
    down = from_rate // g
    resampled = resample_poly(audio.astype(np.float64), up, down)
    return np.clip(resampled, -32768, 32767).astype(np.int16)


def resample_to_16k(audio: np.ndarray, input_sample_rate: int) -> np.ndarray:
    """Resample audio from input_sample_rate to 16 kHz."""
    return resample_audio(audio, input_sample_rate, SAMPLE_RATE)


# ---------------------------------------------------------------------------
# WAV encode / decode
# ---------------------------------------------------------------------------


def encode_wav(audio: np.ndarray, sample_rate: int = SAMPLE_RATE) -> bytes:
    """Encode a numpy int16 array into WAV bytes (in-memory)."""
    buf = io.BytesIO()
    with wave.open(buf, "wb") as wf:
        wf.setnchannels(CHANNELS)
        wf.setsampwidth(2)  # int16 = 2 bytes
        wf.setframerate(sample_rate)
        wf.writeframes(audio.tobytes())
    return buf.getvalue()


def decode_wav(wav_data: bytes) -> tuple[np.ndarray, int, int]:
    """
    Decode WAV bytes into a float32 numpy array normalized to [-1, 1].

    Returns:
        (audio: np.ndarray[float32], sample_rate: int, n_channels: int)
    """
    buf = io.BytesIO(wav_data)
    with wave.open(buf, "rb") as wf:
        sample_rate = wf.getframerate()
        n_channels = wf.getnchannels()
        sample_width = wf.getsampwidth()
        raw_frames = wf.readframes(wf.getnframes())

    if sample_width == 2:
        audio = np.frombuffer(raw_frames, dtype=np.int16).astype(np.float32) / 32768.0
    elif sample_width == 4:
        audio = (
            np.frombuffer(raw_frames, dtype=np.int32).astype(np.float32) / 2147483648.0
        )
    else:
        raise ValueError(f"Unsupported sample width: {sample_width}")

    if n_channels > 1:
        audio = audio.reshape(-1, n_channels)

    return audio, sample_rate, n_channels


def load_wav(path: str) -> np.ndarray:
    """Load a WAV file from disk and return it as a numpy int16 array at 16 kHz."""
    with wave.open(path, "rb") as wf:
        raw = wf.readframes(wf.getnframes())
        file_rate = wf.getframerate()
    audio = np.frombuffer(raw, dtype=np.int16)
    return resample_audio(audio, file_rate, SAMPLE_RATE)


def save_wav(audio: np.ndarray, output_dir: str) -> str:
    """Write a numpy int16 audio array to a timestamped WAV file at SAMPLE_RATE."""
    os.makedirs(output_dir, exist_ok=True)
    timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    filename = os.path.join(output_dir, f"command_{timestamp}.wav")
    with wave.open(filename, "wb") as wf:
        wf.setnchannels(CHANNELS)
        wf.setsampwidth(2)
        wf.setframerate(SAMPLE_RATE)
        wf.writeframes(audio.tobytes())
    return filename


# ---------------------------------------------------------------------------
# Device utilities
# ---------------------------------------------------------------------------


def list_devices() -> None:
    """Print all available audio input/output devices."""
    devices = sd.query_devices()
    for i, dev in enumerate(devices):
        caps = []
        if dev["max_input_channels"] > 0:
            caps.append("in")
        if dev["max_output_channels"] > 0:
            caps.append("out")
        io_str = "/".join(caps) if caps else "none"
        print(f"{i}: {dev['name']} ({io_str})")
