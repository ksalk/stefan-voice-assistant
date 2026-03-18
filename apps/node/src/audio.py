import os
import time
import wave
from collections import deque
from datetime import datetime

import numpy as np
import sounddevice as sd
from piper.voice import PiperVoice
from scipy.signal import resample_poly
from math import gcd

from config import audioConfig, ttsConfig

# ---------------------------------------------------------------------------
# Audio constants
# ---------------------------------------------------------------------------

SAMPLE_RATE = 16000  # Target rate for OpenWakeWord and Whisper
CHANNELS = 1
FRAME_MS = 20
FRAME_SAMPLES = int(SAMPLE_RATE * FRAME_MS / 1000)

OUTPUT_SAMPLE_RATE = ttsConfig.OUTPUT_SAMPLE_RATE  # Speaker native sample rate

INPUT_SAMPLE_RATE = audioConfig.INPUT_SAMPLE_RATE  # Mic native sample rate
INPUT_FRAME_SAMPLES = int(INPUT_SAMPLE_RATE * FRAME_MS / 1000)

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


# Lazy-loaded singleton — loaded once on first call to speak()
_piper_voice: PiperVoice | None = None

def _get_voice() -> PiperVoice:
    global _piper_voice
    if _piper_voice is None:
        print(f"[TTS] Loading TTS model: {ttsConfig.PIPER_MODEL}")
        _piper_voice = PiperVoice.load(ttsConfig.PIPER_MODEL)
    return _piper_voice


def preload_tts_model() -> None:
    """Load the TTS model eagerly so the first speak() call doesn't block."""
    _get_voice()


# TODO: move TTS synthesis to server possibly since its more powerful
def speak(text: str, node_state: dict) -> float:
    """
    Synthesize `text` via piper-tts and play it through the default output
    device using sounddevice (blocks until playback is complete).

    Returns the timestamp at which playback started.
    """
    voice = _get_voice()
    node_state["speaking"] = True
    try:
        chunks = list(voice.synthesize(text))
        if not chunks:
            return
        # Each AudioChunk.audio_float_array is float32 in [-1, 1]
        audio = np.concatenate([c.audio_float_array for c in chunks])
        tts_rate = chunks[0].sample_rate

        # Resample to the speaker's native rate if they differ
        if tts_rate != OUTPUT_SAMPLE_RATE:
            g = gcd(OUTPUT_SAMPLE_RATE, tts_rate)
            audio = resample_poly(audio, OUTPUT_SAMPLE_RATE // g, tts_rate // g).astype(
                np.float32
            )

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
        io = []
        if dev['max_input_channels'] > 0:
            io.append('in')
        if dev['max_output_channels'] > 0:
            io.append('out')
        io_str = '/'.join(io) if io else 'none'
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
    buffer_samples -= total_recorded  # sync the counter (nonlocal update handled by caller)

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
    with wave.open(path, 'rb') as wf:
        raw = wf.readframes(wf.getnframes())
    return np.frombuffer(raw, dtype=np.int16)


def save_wav(audio: np.ndarray, output_dir: str) -> str:
    """Write a numpy int16 audio array to a timestamped WAV file."""
    os.makedirs(output_dir, exist_ok=True)
    timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    filename = os.path.join(output_dir, f"command_{timestamp}.wav")
    with wave.open(filename, 'wb') as wf:
        wf.setnchannels(CHANNELS)
        wf.setsampwidth(2)  # int16 = 2 bytes
        wf.setframerate(SAMPLE_RATE)
        wf.writeframes(audio.tobytes())
    return filename
