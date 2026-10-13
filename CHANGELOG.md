# Changelog

All notable changes to this project are documented here.
Format based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### Added
- **Catacombs level (`Assets/Level/Catacombs.txt`, `DungeonGrid.cs`, `DungeonBuilder.cs`)**: a 21x19-cell dungeon generated from an ASCII map into `Dungeon_Main.unity` (merged wall runs, ceiling, pillars, ragdoll traps, altar, exit portal, baked NavMesh). `DungeonGrid` validates the map (one start/altar, three shrines, sealed border, everything reachable on foot) and is unit-tested. See `docs/LEVEL_DESIGN.md`.
- **Spawn points (`SpawnPoint.cs`, `SpawnRegistry`)**: levels place player / relic / enemy markers and the match flow reads them instead of hardcoded coordinates.
- **PlayMode test suite (`Assets/Tests/PlayMode`)**: loads `Dungeon_Main` and verifies spawns, relics, monster on NavMesh, NavMesh paths from the start to the altar, every shrine and the exit, and a full win through the portal.
- **Windows build pipeline (`EchoBuildPipeline.cs`)**: menu *Echo Protocol → Build → Windows (Release / Development)* and a headless entry point for batch builds; copies `steam_appid.txt` next to the exe and exits non-zero on failure.
- **EditMode test suite (`Assets/Tests/EditMode`)**: hearing falloff, microphone dB → loudness buckets with hysteresis, `NoiseRegistry` and relic dialogue tables.
- **`DebugKeys`**: single gate for the prototype shortcuts (`V`, `J`, `H`, `T`, `Y`, debug ragdoll); they are compiled out of release builds.
- **Central Altar & Receptacle System (`AltarReceptacle.cs` & `AltarSocket.cs`)**:
  - Interactive pedestal sockets implementing `IInteractable`, allowing players carrying living relics to lock them into the altar with `[E]`.
  - Individual socket state tracking, visual glowing lights (cyan dormant -> golden active), and automatic completion detection.
  - Sentient relics trigger unique `AltarPlaced` comedic dialogues upon being docked.
- **Offline & Standalone Testing Support (`PlayerController.cs`, `PlayerInteraction.cs`, `RelicCarryable.cs`, `GameExtractionManager.cs`)**:
  - Maintained first-person Camera and AudioListener active by default in offline/unspawned mode to prevent "No cameras rendering" black screens.
  - Enabled local movement, look controls, and `IInteractable` pickup/drop when running in standalone mode outside of a FishNet server.
  - Dynamic runtime relic spawning in `GameExtractionManager` to prevent un-reserialized scene object errors.
  - `OfflineSandboxGuard.cs`: when no FishNet `NetworkManager` is in the scene, scene `NetworkObject`s are flagged non-networked before `Start()` so FishNet no longer deactivates the player (fixes the "No cameras rendering" black screen and missing AudioListener).
- **The Symphony Choir Climax (`RelicVoiceSynthesizer.cs` & `RelicBrain.cs`)**:
  - Procedural runtime alien choral synthesis: each relic generates a unique musical drone/arpeggio (Bob's C4/E4 hum, Nelly's high A4 soprano vibrato, Gloop's E3 pulsing chant, Lord Malakor's deep A2 Gregorian throat drone).
  - When all relics are socketed, they sing together in a funny, harmonic A-minor alien choir, continuously emitting loud noise to attract enemies.
- **Permanent Sanctuary Illumination (`EchoShaderPublisher.cs`)**:
  - Added `SetFullIllumination(bool enabled)`: floods the entire arena in permanent golden echo light upon symphony completion, removing the need for vocal echolocation during the final sprint.
- **Monster Enrage Mechanic (`EnemyBrain.cs`)**:
  - Added `EnrageAll(float speed)`: the blind beast awakens and enrages into a permanent 7.0 m/s hunt mode during the extraction sequence.
- **Escape Portal & Extraction Flow (`EscapePortal.cs` & `GameExtractionManager.cs`)**:
  - Extraction gateway on the perimeter wall that unlocks and activates beacon lights/audio upon symphony activation.
  - 45-second extraction countdown with flashing warning HUD.
  - High on Life style post-match screens: Victory (Rank S+ with comedic relic celebration quotes) and Defeat (with post-mortem roasts), plus `[R]` quick match restart.
- **Sandbox Test Integration (`EchoSandboxBuilder.cs`)**:
  - `Echo Protocol > Setup Altar and Escape Portal` menu item and automatic builder routines configuring the Central Altar, 3 Living Relics, North Wall Escape Portal, and Game Extraction Manager in `Assets/Scenes/Sandbox_Test.unity`.
- **Altar Placement Relic Dialogues (`RelicPersonality.cs`)**:
  - Added `RelicDialogueCategory.AltarPlaced = 9` with self-aware lines for all 4 personality archetypes (Bob, Nelly, Gloop, and Malakor).
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


### Changed
- Unity editor bumped to `6000.6.5f1` (docs updated).
- `HearingSensor.ComputePerceivedLevel`, `MicrophoneLoudness.DbToLevel01` and `MicrophoneLoudness.ResolveState` extracted as pure static functions (same behaviour, now unit-testable).
- Replaced deprecated `FindFirstObjectByType` / `FindObjectsByType(..., FindObjectsSortMode)` calls with `FindAnyObjectByType` / `FindObjectsByType(FindObjectsInactive)` (removes the `CS0618` warnings).
- Documentation brought back in sync with the code: README (status, structure, controls, roadmap), architecture (multiplayer is implemented, gameplay modules), enemy AI (catch distance, enrage), ragdoll (owner-only), networking (implementation notes, offline fallback, current limitations) and the living-relics design (real archetypes, class diagram, PR status).

### Fixed
- **Offline guard (`OfflineSandboxGuard.cs`)**: it only ran for the first scene of the game, so a scene loaded later (menu to level, match restart) lost its player and monster. It now also runs on every `sceneLoaded`.
- **Editor tooling (`EchoSandboxBuilder.cs`)**: removed the `[InitializeOnLoad]` hooks that opened and modified `Sandbox_Test` on every script recompile; the builders are menu-only now.
- **Player builds (`EchoSandboxBuilder.cs`)**: the editor tooling lived inside the game assembly and referenced `UnityEditor`, so a player build could not compile. It now has its own editor-only assembly (`EchoProtocol.Editor`).
- **Microphone (`MicrophoneLoudness.cs`)**: capture is now reference counted per device, so a remote avatar spawning (its copy of the component is disabled right away) no longer stops the local player's microphone. The loudness also no longer drops to silence for ~32 ms every second when the 1 s ring buffer wraps.
- **Enemy (`EnemyBrain.cs`)**: an enraged monster now keeps tracking the nearest player in silence instead of standing at its last heard position (the world origin if it never heard anything); `Enrage` is ignored on remote clients, where the agent is disabled and `SetDestination` logged errors.
- **Hearing (`HearingSensor.cs`)**: sources whose owner was destroyed are skipped instead of throwing.
- **Ragdoll (`PlayerRagdoll.cs`, `RagdollTrap.cs`)**: the trap and the `R` debug key only ragdoll the *owner*; before, every peer turned remote avatars dynamic and fought their `NetworkTransform`.
- **Relics (`RelicCarryable.cs`, `RelicBrain.cs`)**: a host no longer handles carrier changes twice (double pickup events and dialogue); clients no longer call observers RPCs (only the server sends dialogue and choir RPCs); a remote carrier's loudness is read from the networked `EchoVisionController`; the server rejects picking up a relic locked in the altar.
- **Match flow (`GameExtractionManager.cs`)**: restarting a match resets the static full-illumination flag, so the next match no longer starts fully lit; `Instance` is cleared on destroy; a player build with a `NetworkManager` no longer creates networked primitives without a `NetworkObject`.
- **Steam (`SteamLobbyManager.cs`)**: the lobby host no longer tries to connect to itself when `LobbyEnter_t` arrives before the server is reported as started; callbacks are disposed and the lobby is left in `OnDisable` (using `SteamManager` from `OnDestroy` could re-create it while quitting and throw).
- **Bootstrap (`NetworkBootstrap.cs`)**: *Host Local* / *Join Local* no longer start a connection on the wrong transport when Tugboat is missing.
- **Unity objects (`EnemyCatchReaction.cs`)**: replaced `??` on Unity components with explicit null checks (it bypasses Unity's overloaded `==`).

### Removed
- Stray root files: `test.cs` (dead copy of the FishySteamworks transport), `fishy_contents.json`, `fishy_latest.json` and `fix_log.txt`.

