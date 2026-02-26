#!/usr/bin/env python3
import argparse
import os
import time
import wave
from collections import deque
from datetime import datetime

import numpy as np
import sounddevice as sd

import json
import io
import requests

import openwakeword
from openwakeword.model import Model

SAMPLE_RATE = 16000
CHANNELS = 1
FRAME_MS = 20
FRAME_SAMPLES = int(SAMPLE_RATE * FRAME_MS / 1000)
COOLDOWN_SECONDS = 1.5
DEFAULT_THRESHOLD = 0.6

DEFAULT_SILENCE_THRESHOLD = 200       # RMS level (int16) below which audio is silence
DEFAULT_SILENCE_DURATION = 1.0        # seconds of consecutive silence to end recording
DEFAULT_MAX_RECORD_DURATION = 10.0    # safety cap in seconds
DEFAULT_OUTPUT_DIR = "./recordings"


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


def parse_args():
    parser = argparse.ArgumentParser(description="Wake word detection MVP (openWakeWord)")
    parser.add_argument('--list-devices', action='store_true', help='List audio devices and exit')
    parser.add_argument('--device', type=int, default=None, help='Input device index')
    parser.add_argument('--threshold', type=float, default=DEFAULT_THRESHOLD, help='Detection threshold')
    parser.add_argument('--silence-threshold', type=float, default=DEFAULT_SILENCE_THRESHOLD,
                        help='RMS energy below which audio is considered silence (default: 200)')
    parser.add_argument('--silence-duration', type=float, default=DEFAULT_SILENCE_DURATION,
                        help='Seconds of consecutive silence to stop recording (default: 1.0)')
    parser.add_argument('--max-record-duration', type=float, default=DEFAULT_MAX_RECORD_DURATION,
                        help='Maximum recording duration in seconds (default: 10.0)')
    parser.add_argument('--output-dir', type=str, default=DEFAULT_OUTPUT_DIR,
                        help='Directory to save command recordings (default: ./recordings)')

    return parser.parse_args()


def record_command(buffer, buffer_samples, silence_threshold, silence_duration, max_duration):
    """
    Drain the shared audio buffer and continue capturing until silence or max duration.
    Returns recorded audio as a numpy int16 array.
    """
    recorded_chunks = []
    silence_samples = 0
    silence_samples_limit = int(SAMPLE_RATE * silence_duration)
    max_samples = int(SAMPLE_RATE * max_duration)
    total_recorded = 0
    got_speech = False

    # Drain whatever is already in the buffer (audio right after the wake word)
    while buffer:
        chunk = buffer.popleft()
        recorded_chunks.append(chunk)
        total_recorded += len(chunk)
    buffer_samples -= total_recorded  # sync the counter (nonlocal update handled by caller)

    record_start = time.time()

    while True:
        elapsed = time.time() - record_start
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

        if rms >= silence_threshold:
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
    return audio, buffer_samples


def save_wav(audio, output_dir):
    os.makedirs(output_dir, exist_ok=True)
    timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    filename = os.path.join(output_dir, f"command_{timestamp}.wav")
    with wave.open(filename, 'wb') as wf:
        wf.setnchannels(CHANNELS)
        wf.setsampwidth(2)  # int16 = 2 bytes
        wf.setframerate(SAMPLE_RATE)
        wf.writeframes(audio.tobytes())
    return filename


def main():
    args = parse_args()

    if args.list_devices:
        list_devices()
        return

    model = Model()

    threshold = float(args.threshold)
    last_trigger = 0.0

    buffer = deque()
    buffer_samples = 0
    target_frame_samples = int(SAMPLE_RATE * 0.08)

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
        device=args.device,
        callback=audio_callback,
    )

    print("Listening for wake word: 'alexa'...")
    print(f"Threshold: {threshold} | Cooldown: {COOLDOWN_SECONDS}s")

    with stream:
        while True:
            if buffer_samples < target_frame_samples:
                time.sleep(0.005)
                continue

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
                # print(f"Wake word detected: alexa (score={score:.3f})")
                print(f"Wake word detected (score={score:.3f}) - recording command...")

                audio, buffer_samples = record_command(
                    buffer, buffer_samples,
                    args.silence_threshold,
                    args.silence_duration,
                    args.max_record_duration,
                )

                if len(audio) > 0:
                    # Save audio to bytes buffer for sending
                    wav_buffer = io.BytesIO()
                    with wave.open(wav_buffer, 'wb') as wf:
                        wf.setnchannels(CHANNELS)
                        wf.setsampwidth(2)
                        wf.setframerate(SAMPLE_RATE)
                        wf.writeframes(audio.tobytes())
                    wav_buffer.seek(0)

                    # Send to .NET server
                    try:
                        files = {'file': ('command.wav', wav_buffer, 'audio/wav')}
                        response = requests.post('http://localhost:5285/command', files=files)
                        print(f"Server response: {response.status_code}")
                    except requests.exceptions.RequestException as e:
                        print(f"Failed to send to server: {e}")

                    # # Local processing (commented out - server handles it)
                    # filename = save_wav(audio, args.output_dir)
                    # print(f"Command saved: {filename}")
                    # recognizer.AcceptWaveform(audio.tobytes())
                    # text = json.loads(recognizer.Result())['text']
                    # print(f"Command: {text}")
                    # recognizer.Reset()
                else:
                    print("No audio captured after wake word.")

                # Clear any audio that accumulated during recording before resuming detection
                buffer.clear()
                buffer_samples = 0
                last_trigger = time.time()
                print("Listening for wake word: 'alexa'...")


if __name__ == '__main__':
    main()
