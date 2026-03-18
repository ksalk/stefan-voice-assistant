import time
from collections import deque

import numpy as np
import sounddevice as sd
import openwakeword
from openwakeword.model import Model

from audio import (
    SAMPLE_RATE,
    CHANNELS,
    FRAME_SAMPLES,
    record_command,
)
from state import node_state
from config import audioConfig
from remote_server import dispatch_audio_command


# ---------------------------------------------------------------------------
# Command listener
# ---------------------------------------------------------------------------

def start_command_listener() -> None:
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
    openwakeword.utils.download_models()
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
                    dispatch_audio_command(audio)
                else:
                    print("No audio captured after wake word.")

                # Clear any audio that accumulated during recording before
                # resuming detection
                buffer.clear()
                buffer_samples = 0
                last_trigger = time.time()
                node_state["listening"] = True