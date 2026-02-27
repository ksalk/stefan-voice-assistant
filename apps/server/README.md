# StefanAssistant Server

ASP.NET Core minimal API that receives a spoken command as a WAV file and transcribes it using [Whisper.NET](https://github.com/sandrohanea/whisper.net) — a fully offline, .NET wrapper around OpenAI's Whisper model via whisper.cpp. Part of the [Stefan Voice Assistant](../../README.md) project.

## Endpoint

```
POST /command
Content-Type: multipart/form-data

file: <WAV file, 16 kHz mono int16>
```

Returns `"OK"`. Transcription is logged to the console (response body with transcript is a planned improvement).

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Whisper GGML model file placed at:
  ```
  apps/server/StefanAssistant.Server.API/ggml-base.bin
  ```
  Download the base model from https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.bin

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

The `/command` endpoint expects audio that matches Whisper's required format:

| Parameter | Value |
|-----------|-------|
| Format | WAV (PCM) |
| Sample rate | 16,000 Hz |
| Channels | 1 (mono) |
| Bit depth | 16-bit int |

## To do

- Transcription result is logged to console only — the HTTP response always returns `"OK"` regardless of the transcript.
- Antiforgery validation is disabled on `/command` (noted as a TODO for future hardening).
- No authentication or rate limiting.
- Test scripts 
