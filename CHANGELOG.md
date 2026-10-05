# Changelog

All notable changes to this project are documented here.
Format based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### Added
- Project structure and coding conventions (`AGENTS.md`, `CONTRIBUTING.md`).
- `MicrophoneLoudness`: microphone capture, RMS → dB → calibrated 0–1 level, smoothing, Silent/Whisper/Talk/Scream states, silence calibration and debug bar.
- `IVoiceLevelSource`: abstraction for any voice-level provider (local mic now, networked or voice-chat later).
- `PlayerController`: clunky first-person movement using the new Input System.
- `EchoVisionController`: maps voice loudness to a sphere radius and publishes global shader values.
- `EchoReveal` URP shader: world-space textured geometry revealed inside the echo sphere with a glowing edge ring.
- `EchoSandboxBuilder` editor tool and `Sandbox_Test` scene for testing the core mechanic.
- Documentation: README, architecture overview, pull request template.
