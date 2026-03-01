# Alexa-like Wake Word MVP (openWakeWord)

Listens for the wake word "alexa", records the command that follows, sends it to the .NET server for transcription and LLM processing, then speaks the response aloud using piper-tts.

## License note
- openWakeWord code: Apache-2.0
- Built-in pre-trained models (including `alexa`): CC BY-NC-SA 4.0 (non-commercial)

## Requirements
- Python 3.9+
- Linux / Raspberry Pi OS
- Microphone input device
- Speaker / audio output device

## System dependencies (Linux/Raspberry Pi)
```bash
sudo apt-get update
sudo apt-get install -y portaudio19-dev
```

## Install
```bash
python -m venv .venv
source .venv/bin/activate
pip install -r requirements.txt
```

## Download piper-tts voice model

The app uses [piper-tts](https://github.com/rhasspy/piper) for offline text-to-speech. Voice models must be placed in the `models/` directory.

```bash
mkdir -p models
wget -O models/en_US-lessac-medium.onnx \
  "https://huggingface.co/rhasspy/piper-voices/resolve/main/en/en_US/lessac/medium/en_US-lessac-medium.onnx"
wget -O models/en_US-lessac-medium.onnx.json \
  "https://huggingface.co/rhasspy/piper-voices/resolve/main/en/en_US/lessac/medium/en_US-lessac-medium.onnx.json"
```

Both the `.onnx` model file and its `.onnx.json` config must be present. Other voices are available at https://huggingface.co/rhasspy/piper-voices — replace the filename in `src/listener.py` (`_PIPER_MODEL`) to switch voices.

The model is loaded lazily on the first response from the server, so startup time is unaffected.

## Run
```bash
python src/main.py
```

Say "alexa" followed by your command. The app records your command, sends it to the server, and speaks the response through your speakers.

## List audio devices
```bash
python src/main.py --list-devices
```

## Select a device and adjust threshold
```bash
python src/main.py --device 2 --threshold 0.6
```

## All options
```
--device INT              Input device index (default: system default)
--threshold FLOAT         Wake word detection threshold (default: 0.6)
--silence-threshold FLOAT RMS energy below which audio is silence (default: 200)
--silence-duration FLOAT  Seconds of consecutive silence to stop recording (default: 1.0)
--max-record-duration FLOAT  Safety cap on recording length in seconds (default: 10.0)
--output-dir PATH         Directory to save command .wav files (default: ./recordings)
```

## Notes
- Audio stream: 16 kHz mono int16
- Frame size: 20 ms (320 samples)
- Default wake word threshold: 0.6
- Cooldown between detections: 1.5 seconds
- Recording stops on 1 second of silence (tunable via `--silence-duration`)
- If `--silence-threshold` is too sensitive for your mic, increase it; if recording never stops, raise it further
- TTS voice model is loaded once and cached in memory for the lifetime of the process
