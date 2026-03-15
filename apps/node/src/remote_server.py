from config import remoteServerConfig, nodeConfig
import io
import time
import wave

import numpy as np
import requests

from audio import (
    SAMPLE_RATE,
    CHANNELS,
    speak,
)
from state import node_state


def dispatch_audio_command(command_audio: np.ndarray) -> None:
    """
    Encode `audio` as an in-memory WAV and POST it to the .NET server.
    If the server returns response text, synthesize it via piper-tts and
    play it through the speakers.

    # Future: replace this with a WebSocket send once the server supports it.
    """
    print(f"[command] Dispatching command audio to server at {remoteServerConfig.URL}...")
    op_start = time.time()

    wav_buffer = io.BytesIO()
    with wave.open(wav_buffer, 'wb') as wf:
        wf.setnchannels(CHANNELS)
        wf.setsampwidth(2)  # int16 = 2 bytes
        wf.setframerate(SAMPLE_RATE)
        wf.writeframes(command_audio.tobytes())
    wav_buffer.seek(0)

    try:
        files = {'file': ('command.wav', wav_buffer, 'audio/wav')}
        headers = {"X-Node-Secret": remoteServerConfig.AUTH_SECRET, "X-Node-Device-ID": nodeConfig.NODE_NAME}
        http_start = time.time()
        response = requests.post(remoteServerConfig.URL, files=files, headers=headers, verify=remoteServerConfig.SSL_VERIFY)
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
