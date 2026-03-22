# Stefan.Server.SttBenchmarks

Benchmarks for comparing Speech-to-Text solutions on speed and quality.

## Engines

| Engine | Models |
|--------|--------|
| **Whisper.net** | base, small, small-q8, medium, large-turbo-q5 |
| **Vosk** | All `vosk-model-*` directories in `stt-models/` |

## Setup

1. Place WAV test files in `TestAudio/` (16kHz mono 16-bit PCM)
2. Edit `TestAudio/test_cases.json` to map files to expected transcripts:

```json
[
  { "audioFile": "sample.wav", "expectedText": "expected transcript here" }
]
```

## Usage

### Speed benchmarks (BenchmarkDotNet)

```bash
# All engines
dotnet run --project benchmarks/Stefan.Server.SttBenchmarks -c Release -- speed

# Whisper only
dotnet run --project benchmarks/Stefan.Server.SttBenchmarks -c Release -- speed --engine whisper

# Vosk only
dotnet run --project benchmarks/Stefan.Server.SttBenchmarks -c Release -- speed --engine vosk
```

### Quality comparison (WER/CER)

```bash
# All engines
dotnet run --project benchmarks/Stefan.Server.SttBenchmarks -c Release -- quality

# Single engine
dotnet run --project benchmarks/Stefan.Server.SttBenchmarks -c Release -- quality --engine whisper
```

## Quality metrics

- **WER** (Word Error Rate) — substitutions, insertions, deletions at word level
- **CER** (Character Error Rate) — same at character level via Levenshtein distance

Lower is better for both. The quality mode outputs a summary table and per-file details with expected vs actual transcripts.
