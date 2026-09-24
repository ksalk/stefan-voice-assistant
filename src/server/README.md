# Stefan Server

The server is the .NET 10 ASP.NET Core API for the [Stefan Voice Assistant](../../README.md) project. It authenticates registered nodes, processes command audio through speech recognition and an OpenAI-compatible LLM, synthesizes the response, stores command data, schedules tool jobs, and exposes dashboard APIs.

## API

### Register a node

```http
POST /api/nodes/register
X-Node-Secret: <shared node secret>
Content-Type: application/json
```

The JSON body contains `NodeName`, `SessionId`, and `Port`. The node must register successfully before it can send commands.

### Process a command

```http
POST /api/commands
X-Node-Secret: <shared node secret>
X-Node-Device-ID: <registered node name>
X-Node-Session-ID: <registered session id>
X-Command-ID: <non-empty GUID>
Content-Type: multipart/form-data
```

The request contains a multipart field named `file` with the command WAV. A successful response contains:

- `audio/wav` response audio in the body
- `X-Command-ID` with the accepted command ID
- `X-Response-Text` with the URL-escaped response text

### Health

`GET /api/health` is anonymous and reports the server version, configured STT and TTS providers, and LLM model.

Dashboard endpoints under `/api/nodes` and `/api/commands` require the dashboard policy and configured CORS origin.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- PostgreSQL for application data and the Quartz job store
- `ffmpeg` on `PATH` for audio compression and decompression
- A non-empty `NodeSecret` shared with the node
- Dashboard CORS configuration when using the dashboard
- LLM credentials and endpoint configuration under `OpenAI`
- Provider-specific STT and TTS credentials and models

The application expects the EF Core and Quartz database schema to be provisioned by the deployment process. It does not create that schema during startup.

## Providers

The checked-in server configuration selects xAI for both STT and TTS. Provider selection is controlled by `SttProvider` and `TtsProvider`:

| Setting | Selection | Configuration |
|---------|-----------|---------------|
| `SttProvider` | `XAi` | `xAI:ApiKey`; calls the xAI speech-to-text endpoint |
| `SttProvider` | `Whisper` | `Whisper:ModelPath` (default `ggml-base.bin` in the server process working directory); the model is downloaded at startup when missing |
| `SttProvider` | `Vosk` | `Vosk:ModelPath`; the default is `../../stt-models/vosk-model-en-us-0.22`, downloaded and extracted at startup when the directory is missing (`Vosk:ModelUrl` overrides the zip source) |
| `TtsProvider` | `XAi` | `xAI:ApiKey`; calls the xAI text-to-speech endpoint |
| `TtsProvider` | any other value | Piper, configured by `Piper:ExecutablePath`, `Piper:WorkingDirectory`, and `Piper:ModelKey` |

The LLM uses the OpenAI SDK and the `OpenAI:ApiKey`, `OpenAI:Endpoint`, and `OpenAI:Model` settings. The checked-in endpoint is OpenRouter. LLM tools include timers and shopping-list operations.

The server Docker image contains no speech models. The Whisper, Vosk, and Piper services download their files at startup when they are missing. The Whisper model source is configured by `Whisper:ModelPath` (default `ggml-base.bin` in the server process working directory) and `Whisper:ModelUrl`. The Vosk model source is configured by `Vosk:ModelPath` and `Vosk:ModelUrl` (the default model zip is about 1.8 GB). The Piper service downloads its executable and configured voice model when they are missing.

## Running locally

Run commands from the repository root:

```bash
dotnet restore Stefan.sln
dotnet run --project src/server/Stefan.Server.API
```

The development launch profile listens on `http://localhost:5285`.

To use the HTTPS profile:

```bash
dotnet run --project src/server/Stefan.Server.API --launch-profile https
```

The HTTPS profile listens on `https://localhost:7036` and `http://localhost:5285`.

The `justfile` also provides `just runserver`.

## Audio

The current node creates 16 kHz, mono, 16-bit PCM WAV input for the command endpoint. The server uses `ffmpeg` to compress input and response audio for persistence and returns synthesized speech as WAV. Individual STT and TTS providers may have additional model requirements.

## Persistence and background work

The server stores command records, node status, shopping-list items, and timers in PostgreSQL. Quartz persists scheduled jobs, including node health pings and timer actions. The server exposes corresponding node and command information to the dashboard.
