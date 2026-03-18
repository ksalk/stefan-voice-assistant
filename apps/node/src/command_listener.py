import time
from collections import deque

import numpy as np
import sounddevice as sd
import openwakeword
from openwakeword.model import Model

from audio import (
    SAMPLE_RATE,
    CHANNELS,
    INPUT_SAMPLE_RATE,
    INPUT_FRAME_SAMPLES,
    FRAME_SAMPLES,
    record_command,
    resample_to_16k,
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
    # 80 ms window for openWakeWord, sized for the input (native) sample rate
    target_input_frame_samples = int(INPUT_SAMPLE_RATE * 0.08)

    def audio_callback(indata, frames, time_info, status):
        nonlocal buffer_samples
        if status:
            print(status)
        audio = indata[:, 0].copy()
        buffer.append(audio)
        buffer_samples += len(audio)

    stream = sd.InputStream(
        samplerate=INPUT_SAMPLE_RATE,
        channels=CHANNELS,
        dtype="int16",
        blocksize=INPUT_FRAME_SAMPLES,
        device=audioConfig.INPUT_DEVICE,
        callback=audio_callback,
    )

    print("[wakeword] Listening for wake word: 'alexa'...")
    print(
        f"[wakeword] Threshold: {audioConfig.WAKEWORD_THRESHOLD} | Cooldown: {audioConfig.WAKEWORD_COOLDOWN}s"
    )
    if INPUT_SAMPLE_RATE != SAMPLE_RATE:
        print(
            f"[wakeword] Mic sample rate: {INPUT_SAMPLE_RATE} Hz -> resampling to {SAMPLE_RATE} Hz"
        )

    node_state["listening"] = True

    with stream:
        while True:
            if buffer_samples < target_input_frame_samples:
                time.sleep(0.005)
                continue

            # Collect one 80 ms frame (at native rate) for the wake word model
            chunks = []
            collected = 0
            while buffer and collected < target_input_frame_samples:
                chunk = buffer.popleft()
                chunks.append(chunk)
                collected += len(chunk)

            buffer_samples -= collected
            frame = np.concatenate(chunks)[:target_input_frame_samples]

            # Resample to 16 kHz before feeding to openWakeWord
            frame_16k = resample_to_16k(frame)

            prediction = model.predict(frame_16k)
            score = float(prediction.get("alexa", 0.0))

            now = time.time()
            if score >= audioConfig.WAKEWORD_THRESHOLD and (now - last_trigger) >= audioConfig.WAKEWORD_COOLDOWN:
                last_trigger = now
                print(f"[wakeword] Wake word detected (score={score:.3f}) - recording command...")

                node_state["listening"] = False
                node_state["recording"] = True

                audio, buffer_samples = record_command(
                    buffer,
                    buffer_samples
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