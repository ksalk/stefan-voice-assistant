import io
import time
import wave
from collections import deque

import numpy as np
import requests
import sounddevice as sd
from openwakeword.model import Model

from audio import (
    SAMPLE_RATE,
    CHANNELS,
    FRAME_SAMPLES,
    record_command,
)
from state import node_state

COOLDOWN_SECONDS = 1.5
DEFAULT_THRESHOLD = 0.6

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
                print(f"Wake word detected (score={score:.3f}) - recording command...")

                node_state["listening"] = False
                node_state["recording"] = True

                audio, buffer_samples = record_command(
                    buffer, buffer_samples,
                    silence_threshold,
                    silence_duration,
                    max_record_duration,
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
                print("Listening for wake word: 'alexa'...")


def _dispatch_command(audio: np.ndarray, server_url: str) -> None:
    """
    Encode `audio` as an in-memory WAV and POST it to the .NET server.

    # Future: replace this with a WebSocket send once the server supports it.
    """
    wav_buffer = io.BytesIO()
    with wave.open(wav_buffer, 'wb') as wf:
        wf.setnchannels(CHANNELS)
        wf.setsampwidth(2)  # int16 = 2 bytes
        wf.setframerate(SAMPLE_RATE)
        wf.writeframes(audio.tobytes())
    wav_buffer.seek(0)

    try:
        files = {'file': ('command.wav', wav_buffer, 'audio/wav')}
        response = requests.post(server_url, files=files)
        print(f"Server response: {response.status_code}")
    except requests.exceptions.RequestException as e:
        print(f"Failed to send to server: {e}")

    # # Local processing (commented out - server handles transcription + LLM)
    # filename = save_wav(audio, output_dir)
    # print(f"Command saved: {filename}")
    # recognizer.AcceptWaveform(audio.tobytes())
    # text = json.loads(recognizer.Result())['text']
    # print(f"Command: {text}")
    # recognizer.Reset()
