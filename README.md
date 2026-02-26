# Stefan Voice Assistant

A two-component voice assistant MVP. A Python process on an edge device (Raspberry Pi or any Linux machine) listens for the "alexa" wake word, records the spoken command, POSTs the audio to a local .NET 10 server which transcribes it and generates a response via an LLM, then speaks the response aloud using piper-tts. No cloud services required beyond the LLM API.

## Architecture

```
[Microphone]
     |
     v
apps/node  (Python)
  - openWakeWord detects "alexa"
  - records command audio (WAV, 16kHz mono)
  - POST /command  -->  apps/server  (.NET 10 / ASP.NET Core)
                              - Vosk transcribes the WAV
                              - LLM (OpenRouter) generates a response
                              - returns response text in HTTP body
  - piper-tts synthesizes response text
     |
     v
[Speakers]
```

## Tech Stack

| Component | Language | Key Libraries |
|-----------|----------|---------------|
| `apps/node` | Python 3.9+ | openWakeWord, sounddevice, numpy, requests, piper-tts |
| `apps/server` | .NET 10 (C#) | ASP.NET Core minimal API, Vosk 0.3.38, OpenAI SDK |

## Prerequisites

- Python 3.9+, `pip`
- `portaudio19-dev` system library (`sudo apt install portaudio19-dev`)
- .NET 10 SDK
- Microphone and speaker/audio output device
- Vosk model directory placed at `apps/server/StefanAssistant.Server.API/vosk-model-full/`
  - Download a model from https://alphacephei.com/vosk/models
- piper-tts voice model placed at `apps/node/models/` (see below)
- OpenRouter API key set in `apps/server/StefanAssistant.Server.API/appsettings.json`

## Setup & Running

**Server (`apps/server`):**

```bash
cd apps/server/StefanAssistant.Server.API
dotnet run
# Listens on http://localhost:5285
```

**Python node (`apps/node`):**

```bash
cd apps/node
pip install -r requirements.txt
```

Download the piper-tts voice model:

```bash
mkdir -p models
wget -O models/en_US-lessac-medium.onnx \
  "https://huggingface.co/rhasspy/piper-voices/resolve/main/en/en_US/lessac/medium/en_US-lessac-medium.onnx"
wget -O models/en_US-lessac-medium.onnx.json \
  "https://huggingface.co/rhasspy/piper-voices/resolve/main/en/en_US/lessac/medium/en_US-lessac-medium.onnx.json"
```

Then run:

```bash
python src/main.py
```

## Python CLI Options

| Flag | Default | Description |
|------|---------|-------------|
| `--device INT` | system default | Input audio device index |
| `--threshold FLOAT` | `0.6` | Wake word detection confidence threshold |
| `--silence-threshold FLOAT` | `200` | RMS energy level below which audio counts as silence |
| `--silence-duration FLOAT` | `1.0` | Seconds of consecutive silence to stop recording |
| `--max-record-duration FLOAT` | `10.0` | Maximum recording length (seconds) |
| `--output-dir PATH` | `./recordings` | Directory to save command WAV files |
| `--list-devices` | — | List available audio input devices and exit |

## Status

MVP. The full pipeline is functional end-to-end: wake word detection, command recording, transcription, LLM response, and TTS playback.

## License

MIT © 2026 Konrad Sałkowski
