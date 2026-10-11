# Changelog

All notable changes to this project are documented here.
Format based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### Added
- **Comedic Living Relic Personalities (`RelicPersonality.cs` & `RelicBrain.cs`)**: Integrated self-aware, comedic living relics with 4 distinct personality archetypes inspired by *High on Life*:
  - **The Sarcastic Critic ("Obsidian Bob")**: Cynical meta commentary roasting player cardio, un-shrimping posture, sweating palms, and lack of corner checking.
  - **The Anxious Coward ("Nervous Nelly")**: Neurotic hyperventilating coward begging players to whisper, panicking when monsters get close, and pre-mourning demise.
  - **The Dumb Gloop ("Gloop the Box")**: Goofy cartoon babbler making funny squeaks, squelches, burps, boings, and celebrating being an aerodynamic cube.
  - **The Grumpy Ancient Boomer ("Lord Malakor")**: Pompous celestial sovereign offended by mortal hands touching his divine geometry, threatening to curse players' descendants and Wi-Fi.
- **Procedural Speech Synthesizer & Doppler Screams (`RelicVoiceSynthesizer.cs`)**:
  - Runtime waveform synthesis with zero external audio assets, providing customized Banjo-Kazooie/Animal Crossing style speech blips.
  - Continuous 3D airborne Doppler scream during high-velocity physics launches (25m - 50m arcs), with velocity-dependent pitch modulation and logarithmic rolloff.
  - Procedural cartoon impact bonks and thuds upon landing.
- **High on Life Dialogue UI & 3D Speech Bubbles (`RelicDialogueUI.cs`)**:
  - Screen-space HUD dialogue card with neon archetype accents, typewriter text effect, animated cursor, and speaker badges.
  - 3D floating world-space speech bubble billboarding above the relic for co-op peer awareness.
- **Blind Monster Hearing Integration (`INoiseSource`)**:
  - `RelicBrain` implements `INoiseSource` and registers with `NoiseRegistry`. Screaming relics in mid-air emit 0.95 loudness, actively attracting the blind monster to track the flying projectile.
- **Testing & Iteration Controls**: Added `[T]` to switch relic personality archetype and `[Y]` to force instant comedic roasts during gameplay.
- **Living Relics & Physical Carrying System (`RelicCarryable.cs` & `IInteractable.cs`)**: Server-authoritative pickup and physical drop interaction (`E` interact, `G` drop) with smooth camera socket attachment (`CarrySocket`). Physics, collisions, and `NetworkTransform` are toggled seamlessly during carry and re-enabled upon throwing or dropping.
- **Player Interaction Raycasting (`PlayerInteraction.cs`)**: Owner-client interaction controller performing forward spherecasts, dynamic HUD prompts (`[E] Pick Up ...` / `Carrying: ... [G] Drop`), and ragdoll auto-drop listening to `PlayerRagdoll.OnRagdollStarted`.
- **Living Relic Prefab & Visuals (`Relic_Test.prefab` & `Echo_Relic.mat`)**: First networked sentient relic prefab registered in `DefaultPrefabObjects.asset` (PrefabId: 2) and `NetworkBootstrap`, sporting a distinctive golden/amber echolocation reveal material.
- **Living Relics Multi-Branch Game Loop Architecture (`docs/LIVING_RELICS_DESIGN.md`)**: Comprehensive design document in English detailing relic voice lines, comedic chatter, altar symphony escape sequence, and 6-branch delivery roadmap for the portfolio demo.
- **English Networking Documentation (`docs/NETWORKING.md`)**: Fully translated to English and aligned with FishNet P2P architecture.
- **Multi-Origin Echo Scanner (EchoReveal.shader & EchoShaderPublisher.cs)**: Hand-written HLSL shader upgraded to support up to 4 simultaneous sound origins via global vector arrays (_EchoSources[4] and _EchoFalloffs). EchoShaderPublisher auto-registers all active player instances and broadcasts origins and radii in LateUpdate.
- **Synchronized Echo Pulses (EchoVisionController.cs)**: Replicated sphere radius and noise level via quantized SyncVar and ServerRpc. Remote clients smoothly interpolate towards replicated radii.
- **Server-Authoritative Blind Monster (Enemy_Blind.prefab & EnemyBrain.cs)**: Synchronized monster state machine and movement via NetworkTransform. On clients, NavMeshAgent and HearingSensor are safely disabled to prevent physics conflicts while maintaining visual fidelity.
- **Networked Hearing & Noise Reception (VoiceNoiseEmitter.cs)**: Remote player loudness is transmitted to the server, allowing the server's HearingSensor to detect, investigate, and hunt both players based on proximity and volume.
- **Networked Ragdoll Synchronization (PlayerController.cs & EnemyCatchReaction.cs)**: Server triggers RpcTriggerRagdoll over network upon catching a victim. Only the owner initiates physical ragdoll tumbling while NetworkTransform replicates the flying trajectory to all peers.
- **Player Visuals & Camera Isolation**: Added blue checkered Echo_Player.mat material. Player body mesh is hidden locally for seamless first-person view while remote avatars are rendered. Camera and AudioListener instances on remote clones are disabled to eliminate multi-listener conflicts.
- **Robust Network Spawning (NetworkBootstrap.cs)**: Dynamic runtime prefab registration in SinglePrefabObjects ensuring both Player and Enemy prefabs are registered in FishNet's SpawnablePrefabs on host and client. Enemy is automatically spawned on server initialization.
- **Networked Player**: Refactored `PlayerController.cs` to inherit from `NetworkBehaviour`. Inputs, Camera, and AudioListener are now gated by ownership (`base.IsOwner`). 
- **Player Spawner**: Integrated FishNet's `PlayerSpawner` into the `NetworkBootstrap` UI to automatically spawn the player prefab for connecting clients.
- **Steam Lobby Integration**: Added `SteamLobbyManager.cs` to handle Steam Matchmaking. Players can now host friends-only lobbies and invite friends via the Steam Overlay (Shift+Tab).
- **Network Bootstrap**: Added `NetworkBootstrap` and `SteamManager` to auto-configure FishNet and initialize Steam dynamically, including a developer GUI to start P2P Host/Client connections.
- **Networking Architecture**: Added `docs/NETWORKING.md` defining the P2P client-host flow with Fish-Net, Steamworks.NET, and FishySteamworks.
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

