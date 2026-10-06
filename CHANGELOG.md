# Changelog

All notable changes to this project are documented here.
Format based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### Added
- **PS1 Retro Aesthetics**: 
  - Vertex snapping (wobble) and affine texture mapping (no perspective correction) added to the `EchoReveal` shader.
  - Custom URP `PS1RetroFeature` that natively downsamples the viewport for crunchy pixels.
  - Post-processing shader with color depth reduction (banding), film grain, and dynamic VHS screen tearing.
  - `PS1GlitchController` that triggers heavy screen tearing when the player falls into ragdoll.
- **Player Ragdoll System**: Players now turn into a physical ragdoll when caught by enemies or when stepping on a `RagdollTrap`.
- **Enemy Gloating**: Enemies now stall for 3 seconds after catching a player.
- **Impact Noise**: Hitting the ground in ragdoll mode generates a loud noise burst (`NoiseBurstEmitter`) that attracts enemies.
- Project structure and coding conventions (`AGENTS.md`, `CONTRIBUTING.md`).
- `MicrophoneLoudness`: microphone capture, RMS → dB → calibrated 0–1 level, smoothing, Silent/Whisper/Talk/Scream states, silence calibration and debug bar.
- `IVoiceLevelSource`: abstraction for any voice-level provider (local mic now, networked or voice-chat later).
- `PlayerController`: clunky first-person movement using the new Input System.
- `EchoVisionController`: maps voice loudness to a sphere radius and publishes global shader values.
- `EchoReveal` URP shader: world-space textured geometry revealed inside the echo sphere with a glowing edge ring.
- `EchoSandboxBuilder` editor tool and `Sandbox_Test` scene for testing the core mechanic.
- Documentation: README, architecture overview, pull request template.
- Enemy AI (blind hunters with perfect hearing):
  - `INoiseSource` / `NoiseRegistry` / `VoiceNoiseEmitter`: any noise can be heard; the player's voice is the first emitter.
  - `HearingSensor`: distance-attenuated perceived loudness, last heard position and suspicion for constant noise.
  - `EnemyBrain`: Idle / Investigate / Hunt state machine on a `NavMeshAgent`, with `OnStateChanged` and `OnPlayerCaught` events.
  - `NavMeshBootstrap`: runtime fallback that builds the NavMesh if it is missing.
- Sandbox builder now bakes a NavMesh, adds a red `Enemy_Blind` (visible only inside echo light) and gives the player a `VoiceNoiseEmitter`.
- Documentation: `docs/ENEMY_AI.md` (hearing model, state diagram, tuning guide).
