import io
import os
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
    save_wav,
    speak,
)
from state import node_state
from config import audioConfig, remoteServerConfig


# ---------------------------------------------------------------------------
# Wake word listener
# ---------------------------------------------------------------------------

def start_wakeword_listener(
    device_id: str,
    ssl_verify: bool = False,
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
        device=audioConfig.INPUT_DEVICE,
        callback=audio_callback,
    )

    print("[wakeword] Listening for wake word: 'alexa'...")
    print(f"[wakeword] Threshold: {audioConfig.WAKEWORD_THRESHOLD} | Cooldown: {audioConfig.WAKEWORD_COOLDOWN}s")

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
            if score >= audioConfig.WAKEWORD_THRESHOLD and (now - last_trigger) >= audioConfig.WAKEWORD_COOLDOWN:
                last_trigger = now
                print(f"[wakeword] Wake word detected (score={score:.3f}) - recording command...")

                node_state["listening"] = False
                node_state["recording"] = True

                audio, buffer_samples = record_command(
                    buffer, buffer_samples,
                    audioConfig.SILENCE_THRESHOLD,
                    audioConfig.SILENCE_DURATION,
                    audioConfig.MAX_RECORDING_DURATION,
                    audioConfig.WAKEWORD_SKIP_MS,
                )

                node_state["recording"] = False

                if len(audio) > 0:
                    _dispatch_command(audio, device_id, ssl_verify)
                else:
                    print("No audio captured after wake word.")

                # Clear any audio that accumulated during recording before
                # resuming detection
                buffer.clear()
                buffer_samples = 0
                last_trigger = time.time()
                node_state["listening"] = True


def _dispatch_command(audio: np.ndarray, device_id: str, ssl_verify: bool = False) -> None:
    """
    Encode `audio` as an in-memory WAV and POST it to the .NET server.
    If the server returns response text, synthesize it via piper-tts and
    play it through the speakers.

    # Future: replace this with a WebSocket send once the server supports it.
    """
    print(f"[command] Dispatching command audio to server at {remoteServerConfig.URL}...")
    op_start = time.time()

    save_wav(audio, audioConfig.RECORDINGS_OUTPUT_DIR)

    wav_buffer = io.BytesIO()
    with wave.open(wav_buffer, 'wb') as wf:
        wf.setnchannels(CHANNELS)
        wf.setsampwidth(2)  # int16 = 2 bytes
        wf.setframerate(SAMPLE_RATE)
        wf.writeframes(audio.tobytes())
    wav_buffer.seek(0)

    try:
        files = {'file': ('command.wav', wav_buffer, 'audio/wav')}
        headers = {"X-Node-Secret": remoteServerConfig.AUTH_SECRET, "X-Node-Device-ID": device_id}
        http_start = time.time()
        response = requests.post(remoteServerConfig.URL, files=files, headers=headers, verify=ssl_verify)
        http_elapsed = time.time() - http_start

        response_text = response.text.strip()
        if response.status_code == 200 and response_text:
            print(f"[assistant] {response_text}")
            speaking_start = speak(response_text, node_state)
            time_to_speaking = speaking_start - op_start
            print(f"[timing] http: {http_elapsed:.2f}s | time to speaking: {time_to_speaking:.2f}s")
        else:
            print(f"[error] Server returned status {response.status_code} with response: {response_text}")
    except requests.exceptions.RequestException as e:
        print(f"[error] Failed to send to server: {e}")
