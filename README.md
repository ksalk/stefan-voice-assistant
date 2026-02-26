# Stefan Voice Assistant

A fully offline, two-component voice assistant MVP. A Python process on an edge device (Raspberry Pi or any Linux machine) listens for the "alexa" wake word, records the spoken command, and POSTs the audio to a local .NET 10 server, which transcribes it using Vosk (Kaldi). No cloud services.

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
                              - logs transcription + processing time
```

## Tech Stack

| Component | Language | Key Libraries |
|-----------|----------|---------------|
| `apps/node` | Python 3.9+ | openWakeWord, sounddevice, numpy, requests |
| `apps/server` | .NET 10 (C#) | ASP.NET Core minimal API, Vosk 0.3.38 |

## Prerequisites

- Python 3.9+, `pip`
- `portaudio19-dev` system library (`sudo apt install portaudio19-dev`)
- .NET 10 SDK
- Vosk model directory placed at `apps/server/StefanAssistant.Server.API/vosk-model-full/`
  - Download a model from https://alphacephei.com/vosk/models

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

MVP. The transcription pipeline is functional end-to-end. Command dispatch and response logic are not yet implemented.

## License

MIT © 2026 Konrad Sałkowski
