import io
import os
import time
import wave
from collections import deque

import numpy as np
import requests
import sounddevice as sd
from openwakeword.model import Model
from piper.voice import PiperVoice

from audio import (
    SAMPLE_RATE,
    CHANNELS,
    FRAME_SAMPLES,
    DEFAULT_WAKEWORD_SKIP_MS,
    record_command,
)
from state import node_state

COOLDOWN_SECONDS = 1.5
DEFAULT_THRESHOLD = 0.6

# ---------------------------------------------------------------------------
# TTS configuration
# ---------------------------------------------------------------------------

_MODELS_DIR = os.path.join(os.path.dirname(__file__), "..", "models")
_PIPER_MODEL = os.path.join(_MODELS_DIR, "en_US-hfc_female-medium.onnx")

# Lazy-loaded singleton — loaded once on first call to _speak()
_piper_voice: PiperVoice | None = None


def _get_voice() -> PiperVoice:
    global _piper_voice
    if _piper_voice is None:
        print(f"Loading TTS model: {_PIPER_MODEL}")
        _piper_voice = PiperVoice.load(_PIPER_MODEL)
    return _piper_voice

# ---------------------------------------------------------------------------
# Wake word listener
# ---------------------------------------------------------------------------

def run_listener(
    threshold: float,
    silence_threshold: float,
    silence_duration: float,
    max_record_duration: float,
    device: int | None,
    server_url: str,
    wakeword_skip_ms: float = DEFAULT_WAKEWORD_SKIP_MS,
) -> None:
    """
    Loads the wake word model, opens the microphone stream, and runs the
    detection loop indefinitely (blocking).

    On each wake word detection:
      1. Records the command audio (silence-gated).
      2. Encodes it as an in-memory WAV.
      3. POSTs it to the .NET server at `server_url`.

    State transitions written to `node_state` so that the HTTP health
    endpoint reflects real-time status.
    """
    model = Model()

    last_trigger = 0.0
    buffer: deque = deque()
    buffer_samples = 0
    target_frame_samples = int(SAMPLE_RATE * 0.08)  # 80 ms window for openWakeWord

    def audio_callback(indata, frames, time_info, status):
        nonlocal buffer_samples
        if status:
            print(status)
        audio = indata[:, 0].copy()
        buffer.append(audio)
        buffer_samples += len(audio)

    stream = sd.InputStream(
        samplerate=SAMPLE_RATE,
        channels=CHANNELS,
        dtype='int16',
        blocksize=FRAME_SAMPLES,
        device=device,
        callback=audio_callback,
    )

    print("Listening for wake word: 'alexa'...")
    print(f"Threshold: {threshold} | Cooldown: {COOLDOWN_SECONDS}s")

    node_state["listening"] = True

    with stream:
        while True:
            if buffer_samples < target_frame_samples:
                time.sleep(0.005)
                continue

            # Collect one 80 ms frame for the wake word model
            chunks = []
            collected = 0
            while buffer and collected < target_frame_samples:
                chunk = buffer.popleft()
                chunks.append(chunk)
                collected += len(chunk)

            buffer_samples -= collected
            frame = np.concatenate(chunks)[:target_frame_samples]

            prediction = model.predict(frame)
            score = float(prediction.get('alexa', 0.0))

            now = time.time()
            if score >= threshold and (now - last_trigger) >= COOLDOWN_SECONDS:
                last_trigger = now
                print(f"[wakeword] Wake word detected (score={score:.3f}) - recording command...")

                node_state["listening"] = False
                node_state["recording"] = True

                audio, buffer_samples = record_command(
                    buffer, buffer_samples,
                    silence_threshold,
                    silence_duration,
                    max_record_duration,
                    wakeword_skip_ms,
                )

                node_state["recording"] = False

                if len(audio) > 0:
                    _dispatch_command(audio, server_url)
                else:
                    print("No audio captured after wake word.")

                # Clear any audio that accumulated during recording before
                # resuming detection
                buffer.clear()
                buffer_samples = 0
                last_trigger = time.time()
                node_state["listening"] = True


def _dispatch_command(audio: np.ndarray, server_url: str) -> None:
    """
    Encode `audio` as an in-memory WAV and POST it to the .NET server.
    If the server returns response text, synthesize it via piper-tts and
    play it through the speakers.

    # Future: replace this with a WebSocket send once the server supports it.
    """
    print(f"[command] Dispatching command audio to server at {server_url}...")
    op_start = time.time()

    wav_buffer = io.BytesIO()
    with wave.open(wav_buffer, 'wb') as wf:
        wf.setnchannels(CHANNELS)
        wf.setsampwidth(2)  # int16 = 2 bytes
        wf.setframerate(SAMPLE_RATE)
        wf.writeframes(audio.tobytes())
    wav_buffer.seek(0)

    try:
        files = {'file': ('command.wav', wav_buffer, 'audio/wav')}
        http_start = time.time()
        response = requests.post(server_url, files=files)
        http_elapsed = time.time() - http_start

        response_text = response.text.strip()
        if response.status_code == 200 and response_text:
            print(f"[assistant] {response_text}")
            speaking_start = _speak(response_text)
            time_to_speaking = speaking_start - op_start
            print(f"[timing] http: {http_elapsed:.2f}s | time to speaking: {time_to_speaking:.2f}s")
        else:
            print(f"[error] Server returned status {response.status_code} with response: {response_text}")
    except requests.exceptions.RequestException as e:
        print(f"[error] Failed to send to server: {e}")


def _speak(text: str) -> int:
    """
    Synthesize `text` via piper-tts and play it through the default output
    device using sounddevice (blocks until playback is complete).
    """
    voice = _get_voice()
    node_state["speaking"] = True
    try:
        chunks = list(voice.synthesize(text))
        if not chunks:
            return
        # Each AudioChunk.audio_float_array is float32 in [-1, 1]
        audio = np.concatenate([c.audio_float_array for c in chunks])
        sample_rate = chunks[0].sample_rate
        speaking_start = time.time()
        sd.play(audio, samplerate=sample_rate)
        sd.wait()
    finally:
        node_state["speaking"] = False
    return speaking_start
