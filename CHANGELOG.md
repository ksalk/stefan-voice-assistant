# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Skip first X ms on recording audio
- SQLite persistence
- Whisper.NET for STT (replacing Vosk)
- Colored logs
- Modularized tools
- Basic tests for node HTTP server
- GitHub Actions CI for dotnet build and node python app

### Changed
- Refactored server code
- Moved dotnet code to `src` directory
- Refactored server solution and added more projects
- Centralized package and build management

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
