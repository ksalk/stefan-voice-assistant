# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.3.0] - 2026-10-06

### Added
- (node) Audio volume support
- (node) Notification sound on wake word detection
- (node) Spoken error message for failed commands
- (node) Audio preprocessing: silence trimming and resampling
- (server) Node registration and management system with health/status endpoints
- (server) PostgreSQL persistence (migrated from SQLite)
- (server) Vosk, xAI, and Whisper STT providers with auto model download
- (server) Server-side TTS with Piper and xAI providers
- (server) Healthcheck endpoint with app version, commit hash, configuration, and LLM model
- (server) Tool system with registry
- (server) Document table for tool entities
- (server) Node ping jobs with Quartz scheduler
- (server) Command logs on command details page, fetched from Loki
- (server) Command history and storage in database
- (server) Serilog logging with startup configuration logs and separate logs per environment
- (ui) Svelte web dashboard (nodes, commands, shadcn UI, auth, CORS)
- (ui) Home page with stat cards and node metrics chart
- (ui) Command details page with transcript, response, duration bar, LLM conversation, tools, and logs
- (ui) Node details page with status labels and improved tables
- (tests) Integration tests for server and node
- (tests) Unit tests for audio processing, domain, and DI composition
- (build) Image tags are auto-derived from Nerdbank.GitVersioning

### Changed
- (node) Replaced Python node app with a dotnet-based app
- (node) Improved thread-safety and extracted documented audio processing logic
- (server) Refactored server architecture (moved more into Vertical Slice approach)
- (server) Moved all STT and TTS logic to the server
- (build) Replaced Just with mise as task runner

### Fixed
- (node) Deleting audio files after playback instead of leaving them on node

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

[Unreleased]: https://github.com/ksalk/stefan-voice-assistant/compare/v0.3.0...HEAD
[0.3.0]: https://github.com/ksalk/stefan-voice-assistant/compare/v0.2.0...v0.3.0
[0.2.0]: https://github.com/ksalk/stefan-voice-assistant/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/ksalk/stefan-voice-assistant/releases/tag/v0.1.0
