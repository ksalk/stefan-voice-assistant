# Stefan Voice Assistant

Stefan Voice Assistant is a voice assistant composed of a .NET 10 edge node, a .NET 10 API server, and an optional Svelte dashboard. The node listens for the "stefan" wake word, records a command, sends it to the server for speech recognition and LLM processing, and plays the synthesized response through the local audio device.

## Architecture

```text
[Microphone]
        |
        v
src/node/Stefan.Node (.NET 10 / C#)
  - reads ALSA input
  - normalizes audio for recognition
  - uses Sherpa-ONNX to detect "stefan"
  - records until silence, timeout, or "stop"
  - sends multipart WAV data to POST /api/commands
        |
        v
src/server/Stefan.Server.API (.NET 10 / ASP.NET Core)
  - validates node registration, session, and command headers
  - transcribes with the configured STT provider
  - calls the configured OpenAI-compatible LLM
  - supports timer and shopping-list tools
  - synthesizes speech with the configured TTS provider
  - stores command data in PostgreSQL
  - returns response.wav and X-Response-Text
        |
        v
src/node/Stefan.Node
  - plays the returned WAV through ALSA/aplay
        |
        v
[Speakers]

src/dashboard/stefan-ui
  - reads node and command data from the server API
```

## Tech Stack

| Component | Runtime | Key Technologies |
|-----------|----------|-------------------|
| `src/node/Stefan.Node` | .NET 10 / C# | ASP.NET Core, Alsa.Net, Sherpa-ONNX, Serilog |
| `src/server` | .NET 10 / C# | ASP.NET Core, OpenAI SDK, Whisper.NET 1.9.1, Vosk, PiperSharp, EF Core/Npgsql, Quartz |
| `src/dashboard/stefan-ui` | TypeScript / SvelteKit | Svelte dashboard for nodes and commands |

## Prerequisites

For local .NET runs:

- .NET 10 SDK
- Linux audio support with microphone and speaker access
- `aplay` and `amixer` on the node host
- `ffmpeg` on the server host
- PostgreSQL with the application and Quartz database schema available
- A node secret shared by the node and server
- An OpenAI-compatible LLM API key;
- A congifuration specified `SttProvider`/`TtsProvider` which can be local engine or remote service;
- Sherpa-ONNX keyword-spotter model files configured by `KeywordSpotter:ModelPath`
- Network access at server startup when the local Whisper provider is selected; the server downloads the Whisper model to `Whisper:ModelPath` (default `ggml-base.bin` in the server working directory) if it is missing

The server downloads the Whisper model at startup when the local Whisper provider is selected. Piper is an optional server-side provider and downloads its executable and configured model when they are not already present. The dashboard additionally requires Node.js and pnpm.

## Setup and Running

Run the commands below from the repository root.

### Database

Provide the `POSTGRES_USER`, `POSTGRES_PASSWORD`, and `POSTGRES_DB` values expected by the Compose file, then start PostgreSQL:

```bash
docker compose --profile db up -d
```

The application expects the EF Core and Quartz schema to be provisioned by the deployment process; it does not create that schema during application startup.

### Server

```bash
dotnet restore Stefan.sln
dotnet run --project src/server/Stefan.Server.API
```

The development launch profile listens on `http://localhost:5285`. Configure the provider, API key, database connection, `NodeSecret`, and dashboard CORS settings before starting it.

The server downloads the Whisper model at startup when the local Whisper provider is selected, so the Docker image contains no speech models.

### Node

Start the node after the server is available so it can register:

```bash
dotnet run --project src/node/Stefan.Node
```

The default remote server URL is `http://127.0.0.1:5285`; override it with `RemoteServer:Url` for a remote server. The node sends its shared secret in `RemoteServer:AuthSecret`.

The `justfile` provides the equivalent `just runserver` and `just runnode` commands.

### Docker deployment

The root Compose file can start PostgreSQL, pgAdmin, and the API:

```bash
docker compose --profile full up --build
```

Prebuilt server and edge-node deployments are defined in `docker-compose.server.yml` and `docker-compose.edge.yml`. They require their respective environment variables, API credentials, audio device access, and database network configuration.

## Node Options and Configuration

The node loads configuration from `appsettings.json`, optional `appsettings.Development.json`, environment variables, and command-line arguments. The following command-line options are available:

| Option | Description |
|--------|-------------|
| `--send-file PATH` | Sends a WAV file to the server, plays the response, and exits. |
| `--play-file PATH` | Plays a local WAV file and exits. |

The node registers with the server before either command runs. Examples:

```bash
dotnet run --project src/node/Stefan.Node -- --send-file /path/to/command.wav
dotnet run --project src/node/Stefan.Node -- --play-file /path/to/response.wav
```

Important settings include:

| Setting | Default | Description |
|---------|---------|-------------|
| `Audio:Input:DeviceName` | `plughw:0,0` | ALSA input device |
| `Audio:Input:SampleRate` | `48000` | Input sample rate |
| `Audio:Input:ProcessingSampleRate` | `16000` | Recognition sample rate |
| `Audio:SilenceThreshold` | `0.02` | Normalized RMS silence threshold |
| `Audio:SilenceTimeoutMs` | `1000` | Silence duration before sending a command |
| `Audio:MaxRecordingMs` | `10000` | Maximum command duration |
| `KeywordSpotter:ModelPath` | `/app/models` | Directory containing the Sherpa-ONNX model files |

The Docker image uses `/app/models`; configure a different model path for a local run.

## Status

The core voice pipeline is implemented. Node integration tests exercise wake-word detection, recording, HTTP dispatch, response playback, and failure playback against a mock server; server integration tests cover health, registration, and command correlation. CI does not run a real microphone, all external AI providers, and speaker playback together.

## License

MIT © 2026 Konrad Sałkowski
