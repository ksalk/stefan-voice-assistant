# StefanAssistant Server

ASP.NET Core minimal API that receives a spoken command as a WAV file and transcribes it using [Vosk](https://alphacephei.com/vosk/) — a fully offline, Kaldi-based speech recognition engine. Part of the [Stefan Voice Assistant](../../README.md) project.

## Endpoint

```
POST /command
Content-Type: multipart/form-data

file: <WAV file, 16 kHz mono int16>
```

Returns `"OK"`. Transcription is logged to the console (response body with transcript is a planned improvement).

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Vosk model directory placed at:
  ```
  apps/server/StefanAssistant.Server.API/vosk-model-full/
  ```
  Download an English model from https://alphacephei.com/vosk/models. The `vosk-model-en-us-0.42-gigaspeech` (large/accurate) model is recommended.

## Running

```bash
cd apps/server/StefanAssistant.Server.API
dotnet run
```

The server starts on `http://localhost:5285` by default.

To use HTTPS:

```bash
dotnet run --launch-profile https
# http://localhost:5285 + https://localhost:7036
```

## Audio Requirements

The `/command` endpoint expects audio that matches the Vosk model's configuration:

| Parameter | Value |
|-----------|-------|
| Format | WAV (PCM) |
| Sample rate | 16,000 Hz |
| Channels | 1 (mono) |
| Bit depth | 16-bit int |

## Known Limitations

- Transcription result is logged to console only — the HTTP response always returns `"OK"` regardless of the transcript.
- Antiforgery validation is disabled on `/command` (noted as a TODO for future hardening).
- No authentication or rate limiting.
