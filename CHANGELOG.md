# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Dotnet node app with wakeword detection and command processing
- Svelte web dashboard (nodes, commands, shadcn UI, auth, CORS)
- Node registration and management system with health/status endpoints
- Docker Compose support with profiles for server and node
- PostgresDB persistence (migrated from SQLite)
- Tool system with registry, shopping list, and scheduler tools
- Vosk, xAI, and Whisper STT providers with auto model download
- Server-side TTS with Piper and xAI providers
- Audio preprocessing: silence trimming and resampling
- Command history and storage in database
- Node ping jobs with Quartz scheduler
- Justfile for build, push, and deployment commands
- App versioning and git commit info endpoint
- CLI parameter for test command dispatch

### Changed
- Replaced Python node app with a dotnet-based console app
- Refactored server to vertical slice architecture
- Extracted Application, AI, and Infrastructure projects
- Moved all STT and TTS logic to the server
- Improved thread-safety in node app

### Fixed
- Node Dockerfile build issues
- CI pipeline build errors
- Command dispatching and audio playback issues

## [0.2.0] - 2026-02-27

### Added
- LLM integration for response and tool use
- Simple system prompt
- TTS for server responses
- Test command CLI parameter

### Changed
- Extract just command text from transcription result
- Modularized python node app
- Updated logging and minor fixes

### Fixed
- Skip TTS response if status code indicates error

## [0.1.0] - 2026-02-26

### Added
- Initial project setup
- Wake-word listening and command recording
- Vosk-based speech-to-text
- Simple dotnet server for speech processing
- README files

[Unreleased]: https://github.com/ksalk/stefan-voice-assistant/compare/v0.2.0...HEAD
[0.2.0]: https://github.com/ksalk/stefan-voice-assistant/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/ksalk/stefan-voice-assistant/releases/tag/v0.1.0
