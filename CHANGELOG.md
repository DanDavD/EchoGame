# Changelog

This file lists the notable changes to the project. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
I have not tagged a release yet, so everything is under Unreleased.

## [Unreleased]

### Added

- **Automatic microphone calibration** (`MicrophoneCalibrator.cs`, `MicrophoneLoudness.cs`). The game learns what silence, normal speech and a scream sound like on each microphone, so it plays the same on any headset, laptop or room.
  - The noise floor follows the quietest recent readings. It drops at once and rises slowly, and it can only rise fast during a 2 second warm-up.
  - The speech level is a slow average of the readings that clearly rise above the floor. Nothing counts as speech during the warm-up, and until the player speaks the guess follows the floor.
  - Normal speech always maps to the same loudness (0.35, about 7 m of echo). A scream 26 dB above it lights the whole cavern.
  - It is plain maths, tested against simulated microphones with floors from -72 to -35 dB, quiet and loud talkers, a muted microphone and a hot one.
  - The learned values show in the debug bar and in a log line whenever they change. *Auto Calibrate* can be turned off to use fixed values.
  - I checked it on my own headset: the floor settles at the real room noise within two seconds.
- **Echo voice sensitivity** (`EchoVisionController.cs`). The echo needs less noise to expand, in the same proportion for every radius. The voice level is multiplied by a sensitivity (1.3 by default, about 23% less noise) before it is mapped to a radius, so a whisper, a normal voice and a scream each reach their radius with the same fraction of the loudness. The noise gate still looks at the real level, so background noise is not amplified, and the monster still hears the real loudness. It is exposed as `VoiceSensitivity` for a future settings menu and covered by `EchoSensitivityTests`.
- **Procedural look** (`EchoReveal.shader`, `EchoMaterials.cs`). The placeholder checkerboard is replaced by procedural surfaces (bricks, slabs, rock, flesh and an energy portal), lit from the echo sphere, with a pulse wave that travels to the edge, a rim light and an HDR edge that blooms. The look of every material is defined in one place and a test checks it. See `docs/VISUAL_DESIGN.md`.
- **Post-processing and camera** (`PostProcessingRig.cs`, `CameraPolish.cs`). ACES tonemapping, bloom, vignette, film grain and chromatic aberration come from a global Volume built in code. Gameplay cameras get post-processing, HDR, SMAA and dithering, which URP leaves off by default.
- **`DustMotes`**: dust that drifts around the player and shows only where the echo lights the air.
- **`MonsterVisual`**: the blind monster gets a procedural body (huge ears that open when it hunts, swinging arms, a lean into the chase). It is animated from its movement speed, so every peer sees the same thing.
- **Escape portal energy** (`EscapePortal.cs`): the gateway is dormant stone until the portal opens, then it shows swirling energy that you can see from far away in the dark.
- **Photo mode** (`PhotoMode.cs`, `-echo-photo`): takes a fixed set of screenshots of the level from the real player camera, to review the look and to make press material.
- **`DebugOverlay`**: the debug readouts (microphone bar, monster state, networking panel once connected) only draw in the Editor and in Development builds. `F3` toggles them.
- **Debug key `M`** (`RelicBrain.cs`): mutes and unmutes the relics' chatter (subtitles, speech blips and the noise their dialogue makes). It only affects the player who presses it. I use it for testing and recording. `H` already froze the monsters.
- **Network rig prefab** (`NetworkRig.prefab`, `NetworkRigBuilder.cs`, `NetworkRigInstaller.cs`): all the multiplayer setup (NetworkManager, Tugboat, PlayerSpawner, Steam lobby support, NetworkBootstrap) is one regenerable prefab that the level builders install. It had been lost from the scenes when the altar commit regenerated them, and `Dungeon_Main` carries it again.
- **`NetworkBootstrap` API and launch flags** (`LaunchOptions.cs`): `StartLocalHost`, `JoinLocalClient`, `Disconnect` and `HostSteamLobby`, plus the command-line flags `-echo-host`, `-echo-join [address]` and `-echo-no-steam`, and log lines for connections and player spawns.
- **Network tests**:
  - EditMode checks that the rig prefab is complete, that every scene in the build carries the rig (except the offline `Sandbox_Test`, on purpose) and that a networked level has no player of its own.
  - PlayMode hosts a real local server in `Dungeon_Main` and checks that the player spawns at a level spawn point, the monster stands on the NavMesh, the server relic exists, the match can be won, and a disconnect and re-host cycle works.
  - The offline sandbox keeps its player, relics and monster.
- **Catacombs level** (`Assets/Level/Catacombs.txt`, `DungeonGrid.cs`, `DungeonBuilder.cs`): a 21x19 cell dungeon generated from an ASCII map into `Dungeon_Main.unity`, with merged wall runs, a ceiling, pillars, ragdoll traps, the altar, the exit portal and a baked NavMesh. `DungeonGrid` validates the map (one start, one altar, three shrines, a sealed border, everything reachable on foot) and is unit-tested. See `docs/LEVEL_DESIGN.md`.
- **Spawn points** (`SpawnPoint.cs`, `SpawnRegistry`): levels place markers for the player, relics and enemy, and the match flow reads them instead of hardcoded coordinates.
- **PlayMode test suite** (`Assets/Tests/PlayMode`): loads `Dungeon_Main` and checks spawns, relics, the monster on the NavMesh, NavMesh paths from the start to the altar, every shrine and the exit, and a full win through the portal.
- **EditMode test suite** (`Assets/Tests/EditMode`): hearing falloff, microphone dB to loudness buckets with hysteresis, `NoiseRegistry` and the relic dialogue tables.
- **Windows build pipeline** (`EchoBuildPipeline.cs`): the menu *Echo Protocol > Build > Windows (Release / Development)* and a command-line entry point for batch builds. It copies `steam_appid.txt` next to the exe and exits with an error code if the build fails.
- **`DebugKeys`**: one gate for the prototype shortcuts (`V`, `J`, `H`, `T`, `Y` and the debug ragdoll). They are compiled out of release builds.
- **Central altar** (`AltarReceptacle.cs`, `AltarSocket.cs`):
  - Pedestal sockets that implement `IInteractable`. A player carrying a living relic locks it into the altar with `E`.
  - Each socket tracks its own state and has a light (cyan while dormant, gold when filled). The altar detects completion by itself.
  - Relics say a unique `AltarPlaced` line when they are docked.
- **Offline and standalone testing** (`PlayerController.cs`, `PlayerInteraction.cs`, `RelicCarryable.cs`, `GameExtractionManager.cs`):
  - The first-person camera and AudioListener stay active by default when the game runs offline, which avoids the "No cameras rendering" black screen.
  - Movement, look controls and pick-up and drop work when running outside a FishNet server.
  - `GameExtractionManager` creates relics at runtime, to avoid errors from scene objects that were never serialized.
  - `OfflineSandboxGuard.cs`: when the scene has no FishNet `NetworkManager`, scene `NetworkObject`s are flagged as non-networked before `Start()`, so FishNet no longer deactivates the player. This fixes the black screen and the missing AudioListener.
- **Symphony choir** (`RelicVoiceSynthesizer.cs`, `RelicBrain.cs`): every relic generates its own sound at runtime (Bob a C4/E4 hum, Nelly a high A4 soprano with vibrato, Gloop an E3 pulsing chant, Lord Malakor a deep A2 drone). When all relics are in the altar they sing together as a silly A minor choir and keep making loud noise to attract the monsters.
- **Permanent sanctuary light** (`EchoShaderPublisher.cs`): `SetFullIllumination(bool)` floods the arena with permanent golden light when the symphony completes, so players do not need their voice for the final sprint.
- **Monster enrage** (`EnemyBrain.cs`): `EnrageAll(float speed)` sends the monster into a permanent 7 m/s hunt during the extraction sequence.
- **Escape portal and extraction flow** (`EscapePortal.cs`, `GameExtractionManager.cs`): a gateway on the perimeter wall that unlocks when the symphony starts, with beacon lights and audio, a 45 second countdown with a flashing warning, and end screens for victory (rank S+ with a relic quote) and defeat (with a roast). `R` restarts the match.
- **Sandbox integration** (`EchoSandboxBuilder.cs`): the menu item *Echo Protocol > Setup Altar and Escape Portal* and builder routines that set up the altar, three living relics, the north wall portal and the extraction manager in `Assets/Scenes/Sandbox_Test.unity`.
- **Altar dialogue** (`RelicPersonality.cs`): a new category, `RelicDialogueCategory.AltarPlaced = 9`, with self-aware lines for the four archetypes: Obsidian Bob (sarcastic critic), Nervous Nelly (anxious coward), Gloop the Box (dumb and happy) and Lord Malakor (grumpy ancient king).
- **Procedural speech and Doppler screams** (`RelicVoiceSynthesizer.cs`): relic voices are synthesised at runtime with no audio assets, as short speech blips in the style of Banjo-Kazooie and Animal Crossing. A thrown relic gives a continuous 3D scream (arcs of 25 to 50 m) whose pitch follows its speed, and landings play cartoon bonks and thuds.
- **Dialogue UI and 3D speech bubbles** (`RelicDialogueUI.cs`): a screen-space dialogue card with archetype colours, typewriter text, a blinking cursor and a speaker badge, plus a floating bubble above the relic so co-op players can see who is talking.
- **Relics as noise sources** (`INoiseSource`): `RelicBrain` registers with `NoiseRegistry`. A relic screaming in mid-air emits 0.95 loudness, so the blind monster tracks the flying relic.
- **Test controls**: `T` switches the relic personality and `Y` forces an instant roast.
- **Living relics and carrying** (`RelicCarryable.cs`, `IInteractable.cs`): pick-up and drop are decided by the server (`E` to interact, `G` to drop), and the relic attaches smoothly to the camera socket (`CarrySocket`). Physics, collisions and `NetworkTransform` are switched off while it is carried and back on when it is thrown or dropped.
- **Player interaction** (`PlayerInteraction.cs`): the owner's client casts a sphere forward, shows HUD prompts (`[E] Pick Up ...`, `Carrying: ... [G] Drop`) and drops the relic when the player ragdolls (`PlayerRagdoll.OnRagdollStarted`).
- **Living relic prefab** (`Relic_Test.prefab`, `Echo_Relic.mat`): the first networked relic prefab, registered in `DefaultPrefabObjects.asset` (PrefabId 2) and `NetworkBootstrap`, with a gold and amber reveal material.
- **Living relics design doc** (`docs/LIVING_RELICS_DESIGN.md`): relic voice lines, the altar symphony, and the six-branch plan for the portfolio demo.
- **Networking doc** (`docs/NETWORKING.md`), now in English and matching the FishNet P2P design.
- **Multi-origin echo** (`EchoReveal.shader`, `EchoShaderPublisher.cs`): the shader supports up to 4 sound origins at once through global vector arrays (`_EchoSources[4]`, `_EchoFalloffs`). `EchoShaderPublisher` finds every active player and sends the origins and radii in `LateUpdate`.
- **Synchronised echo** (`EchoVisionController.cs`): the radius and noise level replicate through a quantized SyncVar and a ServerRpc, and remote clients interpolate towards them.
- **Server-authoritative monster** (`Enemy_Blind.prefab`, `EnemyBrain.cs`): the state machine and movement are synchronised with `NetworkTransform`. On clients the NavMeshAgent and HearingSensor are disabled to avoid physics conflicts.
- **Networked hearing** (`VoiceNoiseEmitter.cs`): the loudness of a remote player is sent to the server, so the server's `HearingSensor` can detect, investigate and hunt both players by distance and volume.
- **Networked ragdoll** (`PlayerController.cs`, `EnemyCatchReaction.cs`): the server calls `RpcTriggerRagdoll` when it catches a player. Only the owner simulates the tumble, and `NetworkTransform` replicates the flight to everyone else.
- **Player visuals and camera isolation**: a blue checkered `Echo_Player.mat`. The local body mesh is hidden for the first-person view while remote avatars are drawn. Camera and AudioListener on remote copies are disabled, so there is only one listener.
- **Network spawning** (`NetworkBootstrap.cs`): the Player and Enemy prefabs are registered in FishNet's SpawnablePrefabs at runtime on host and client, and the enemy spawns when the server starts.
- **Networked player**: `PlayerController.cs` now inherits from `NetworkBehaviour`, and input, camera and AudioListener depend on ownership (`base.IsOwner`).
- **Player spawner**: FishNet's `PlayerSpawner` is part of the `NetworkBootstrap` setup and spawns the player prefab for each connecting client.
- **Steam lobbies**: `SteamLobbyManager.cs` handles Steam matchmaking. A host can create a friends-only lobby and invite friends from the Steam overlay (Shift+Tab).
- **Network bootstrap**: `NetworkBootstrap` and `SteamManager` configure FishNet and start Steam at runtime, with a developer panel to start a P2P host or client.
- **Networking architecture**: `docs/NETWORKING.md` describes the P2P client-host flow with Fish-Net, Steamworks.NET and FishySteamworks.
- **Player ragdoll**: players turn into a physical ragdoll when an enemy catches them or when they step on a `RagdollTrap`.
- **Enemy gloating**: after catching a player, the enemy stands still for 3 seconds.
- **Impact noise**: hitting the ground as a ragdoll makes a loud noise burst (`NoiseBurstEmitter`) that attracts enemies.
- **Project structure and conventions** (`AGENTS.md`, `CONTRIBUTING.md`).
- **`MicrophoneLoudness`**: microphone capture, RMS to dB to a calibrated 0-1 level, smoothing, Silent / Whisper / Talk / Scream states, silence calibration and a debug bar.
- **`IVoiceLevelSource`**: an abstraction for any voice-level provider (the local microphone now, networked or voice chat later).
- **`PlayerController`**: clunky first-person movement with the new Input System.
- **`EchoVisionController`**: maps voice loudness to a sphere radius and publishes global shader values.
- **`EchoReveal` shader**: URP shader that reveals world-space geometry inside the echo sphere, with a glowing edge ring.
- **`EchoSandboxBuilder`** editor tool and the `Sandbox_Test` scene, for testing the core mechanic.
- **Enemy AI** (blind hunters with perfect hearing):
  - `INoiseSource`, `NoiseRegistry` and `VoiceNoiseEmitter`: anything can be heard, and the player's voice is the first emitter.
  - `HearingSensor`: perceived loudness that falls off with distance, the last heard position, and suspicion for constant noise.
  - `EnemyBrain`: an Idle / Investigate / Hunt state machine on a `NavMeshAgent`, with `OnStateChanged` and `OnPlayerCaught` events.
  - `NavMeshBootstrap`: builds the NavMesh at runtime if it is missing.
  - The sandbox builder now bakes a NavMesh, adds a red `Enemy_Blind` (visible only inside echo light) and gives the player a `VoiceNoiseEmitter`.
- **Documentation**: README, architecture overview, `docs/ENEMY_AI.md` (hearing model, state diagram and tuning guide) and the pull request template.

### Changed

- **Networked altar and match flow**: `AltarReceptacle` and `GameExtractionManager` are now networked scene objects. The server decides every docking, the symphony, the win or lose result and the restart, and clients follow through `SyncVar`s. The escape portal reports to the server. A restart respawns the monster and the relics and sends players back to their spawn points, without reloading the scene (that would end the session). The pure rules live in `AltarRules` and `MatchRules`, with EditMode tests.
- **One relic per shrine, personality replicated**: the server spawns all three relics at the level's shrines and their personality reaches every peer. A docked relic locks on every peer.
- The Unity editor is now `6000.6.5f1` (docs updated).
- `HearingSensor.ComputePerceivedLevel`, `MicrophoneLoudness.DbToLevel01` and `MicrophoneLoudness.ResolveState` are now pure static functions. The behaviour is the same, and they can be unit-tested.
- Replaced the deprecated `FindFirstObjectByType` and `FindObjectsByType(..., FindObjectsSortMode)` calls with `FindAnyObjectByType` and `FindObjectsByType(FindObjectsInactive)`. This removes the `CS0618` warnings.
- Brought the docs back in line with the code: README (status, structure, controls, roadmap), architecture (multiplayer is implemented, gameplay modules), enemy AI (catch distance, enrage), ragdoll (owner only), networking (implementation notes, offline fallback, current limitations) and the living relics design (real archetypes, class diagram, PR status).

### Fixed

- **Calibration too deaf for a quiet microphone** (`MicrophoneLoudness.cs`, `Player.prefab`, `Sandbox_Test.unity`): the silence floor was -35 dB, so normal speech on a headset microphone (-45 to -35 dB, idling at -60 dB) gave no echo at all. The fixed floor is now -55 dB. It is the manual fallback, because the game calibrates automatically (see above). Speech now lights about 3 m at -45 dB, about 7 m at -40 dB and about 11 m at -35 dB. An idle microphone reveals nothing, and a shout still lights the whole cavern. `EchoCalibrationTests` reads the shipped values and pins this.
- **Relic subtitles were unreadable** (`RelicDialogueUI.cs`): the dark panel tint was still active while the text was drawn, which multiplied the letters into near black. The text now uses its own colours.
- **Debug hints in release builds** (`PlayerInteraction.cs`): the carried-relic line no longer advertises the `[T]` and `[Y]` debug keys in a release build.
- **Multiplayer missing from the scenes**: see the network rig prefab above.
- **Relic copies in networked scenes** (`GameExtractionManager.cs`): with a `NetworkManager` present, the manager no longer builds local relics (`NetworkObject`s that were never spawned and could not be picked up). The server owns them.
- **Steam manager** (`SteamManager.cs`): creating the manager again after it was destroyed on purpose threw "Tried to Initialize the SteamAPI twice". The guard now applies only while the game is quitting. Steam is optional: without it, the lobby manager logs a warning instead of an error.
- **Offline guard** (`OfflineSandboxGuard.cs`): it kept reporting the offline state after a networked scene loaded. It now clears the flag.
- **Offline guard** (`OfflineSandboxGuard.cs`): it only ran for the first scene of the game, so a scene loaded later (menu to level, match restart) lost its player and monster. It now also runs on every `sceneLoaded`.
- **Editor tooling** (`EchoSandboxBuilder.cs`): removed the `[InitializeOnLoad]` hooks that opened and changed `Sandbox_Test` on every script recompile. The builders are menu-only now.
- **Player builds** (`EchoSandboxBuilder.cs`): the editor tooling was inside the game assembly and referenced `UnityEditor`, so a player build could not compile. It now has its own editor-only assembly (`EchoProtocol.Editor`).
- **Microphone** (`MicrophoneLoudness.cs`): capture is now reference counted per device, so a remote avatar spawning (its copy of the component is disabled right away) no longer stops the local player's microphone. The loudness also no longer drops to silence for about 32 ms every second when the 1 second ring buffer wraps.
- **Enemy** (`EnemyBrain.cs`): an enraged monster now keeps tracking the nearest player in silence, instead of standing at its last heard position (the world origin if it never heard anything). `Enrage` is ignored on remote clients, where the agent is disabled and `SetDestination` logged errors.
- **Hearing** (`HearingSensor.cs`): sources whose owner was destroyed are skipped instead of throwing.
- **Ragdoll** (`PlayerRagdoll.cs`, `RagdollTrap.cs`): the trap and the `R` debug key only ragdoll the owner. Before, every peer made the remote avatars dynamic and fought their `NetworkTransform`.
- **Relics** (`RelicCarryable.cs`, `RelicBrain.cs`):
  - A host no longer handles carrier changes twice (double pickup events and dialogue).
  - Clients no longer call observers RPCs. Only the server sends dialogue and choir RPCs.
  - The loudness of a remote carrier is read from the networked `EchoVisionController`.
  - The server rejects picking up a relic that is locked in the altar.
- **Match flow** (`GameExtractionManager.cs`): restarting a match resets the static full-illumination flag, so the next match no longer starts fully lit. `Instance` is cleared on destroy. A player build with a `NetworkManager` no longer creates networked primitives without a `NetworkObject`.
- **Steam lobby** (`SteamLobbyManager.cs`): the lobby host no longer tries to connect to itself when `LobbyEnter_t` arrives before the server is reported as started. Callbacks are disposed and the lobby is left in `OnDisable` (using `SteamManager` from `OnDestroy` could create it again while quitting and throw).
- **Bootstrap** (`NetworkBootstrap.cs`): *Host Local* and *Join Local* no longer start a connection on the wrong transport when Tugboat is missing.
- **Unity objects** (`EnemyCatchReaction.cs`): replaced `??` on Unity components with explicit null checks, because `??` bypasses Unity's overloaded `==`.

### Removed

- Stray root files: `test.cs` (a dead copy of the FishySteamworks transport), `fishy_contents.json`, `fishy_latest.json` and `fix_log.txt`.
