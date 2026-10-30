# Echo Protocol

A co-op horror-comedy game where your voice is your only light.

The cave is pitch black. Hold `V` and whisper, and you see a few metres. Hold `V` and scream, and the whole cavern lights up, but you also tell the blind monsters exactly where you are, because they hunt by sound.

![Unity](https://img.shields.io/badge/Unity-6000.6-black?logo=unity)
![Render Pipeline](https://img.shields.io/badge/URP-17.6-blue)
![Status](https://img.shields.io/badge/status-prototype-orange)

---

## The idea

I'm building Echo Protocol as a "friendslop" game for streamers. Instead of lots of content, I want a few simple systems that create chaotic moments on their own: accidental betrayals, panic, ragdoll falls.

The echo is **push to talk**: your voice only lights the cave while you hold `V`. Talking without it is plain voice chat. It reveals nothing, but the monster and the team noise meter still hear it, just less than an echo.

| Holding `V`, you | The game does |
|---|---|
| **Stay silent** | You are blind. Nothing is revealed. |
| **Whisper** | A small sphere of light appears around you, a few metres wide. |
| **Talk** | A bigger sphere lights up (about 7 m at a normal speaking voice). Monsters start to investigate. |
| **Scream** | The whole cavern is revealed, and so is your position. Monsters hunt you. |

On top of that, the team shares one noise meter, and the relics are as sensitive as babies:

- **Team noise meter.** Every voice fills it, an echo more than chat, and so does a crying relic. When it is full the beast charges at whoever made the racket, the music turns dramatic, and the alarm only ends once the whole team stays silent for a few seconds.
- **Carry a relic like a baby.** Whoever carries one has to whisper. Talking out loud, a hard landing, or keeping it too long while a teammate could take it makes it cry. Pass it on: look at a teammate close by and press `E`.
- **The human echo ritual.** A crying relic plays a short rhythm. The players near it repeat it one after the other with their voice ("ba ba baaa", any sound, any language: only the rhythm counts). Everyone right, and it calms down. Someone wrong, and the meter rises and it tries an easier rhythm. Fail three times and it cries itself out, and the beast hears it.

See [Team noise and relic rituals](docs/TEAM_NOISE_AND_RITUALS.md).

## Screenshots

The same corridor before and after the procedural look. Both were taken with the built-in photo mode (`-echo-photo`).

| Before: placeholder checkerboard | After: procedural bricks, slabs, dust and post-processing |
|---|---|
| ![Before](docs/media/look-before-checkerboard.png) | ![After](docs/media/look-after-corridor.png) |

| The blind monster | The escape portal, open |
|---|---|
| ![Monster](docs/media/monster-closeup.png) | ![Portal](docs/media/portal-open.png) |

## Current status

This is an active prototype that I'm building on my own with a deadline. Progress is tracked in the [roadmap](#roadmap) and the [changelog](CHANGELOG.md).

**What works today:**
- Microphone capture: RMS to decibels to a 0-1 loudness, with attack and release smoothing, Silent / Whisper / Talk / Scream states, and automatic calibration for each microphone.
- A first-person controller that is clunky on purpose (new Input System).
- Echo vision with push to talk (`V`): while the key is held, the loudness drives spheres of light (up to 4 players) through a URP shader that reads global shader values.
- A shared team noise meter: voices and crying relics fill it, silence drains it, and a full meter sends the monster and starts dramatic procedural music until the team goes silent.
- Proximity voice chat: open-mic voice travels over the game's own network and plays from each player's body in 3D, so voices fade with distance, around corners and when a friend is dragged away. Headphones are recommended (there is no echo cancellation).
- Sensitive relics: they cry when the carrier is too loud, when they land hard, or when nobody passes them around; players hand them over with `E`. A crying relic is calmed with the human echo ritual, a rhythm the team repeats with their voices, checked by rhythm only.
- Blind enemy AI: a hearing sensor feeds an Idle / Investigate / Hunt state machine that moves on a NavMesh.
- A physics-driven player ragdoll (enemy catches, traps) that makes noise when it hits the ground.
- Multiplayer with Fish-Net in client-host mode: networked players, monster, echo pulses, relics, altar and match flow. It runs locally over Tugboat. Steam lobbies exist, but the Steam transport is not wired yet (see the [networking limitations](docs/NETWORKING.md#5-current-limitations)).
- Living relics: carryable, talking relics with procedural voices, jokes, and reactions to the microphone, sprinting and the monster.
- The altar, the symphony choir and the escape portal. This gives a full loop from objective to extraction to victory or defeat. The server decides it in multiplayer, and it also runs offline in the sandbox (see [networking](docs/NETWORKING.md)).
- A catacombs level generated from a text map, with a validator and tests.
- A procedural look with no art assets: one shader draws brick walls, stone slabs, rock, flesh and an energy portal, lit by the sound that reveals them. On top of that there is post-processing (bloom, vignette, grain), drifting dust and a monster animated in code. A photo mode (`-echo-photo`) takes reference screenshots of the level. See [Visual design](docs/VISUAL_DESIGN.md).
- A one-click sandbox scene builder (editor tool) with a baked NavMesh and a test arena.

**Still to do:** ambience audio, a retro low-resolution look, a main menu and a verified release build. See the [roadmap](#roadmap).

## Tech stack

| Area | Choice |
|---|---|
| Engine | Unity 6 (`6000.6.5f1`) |
| Rendering | Universal Render Pipeline (URP 17.6), custom HLSL shader |
| Input | Unity Input System (`InputSystem_Actions.inputactions`) |
| Networking | [Fish-Net](https://fish-networking.gitbook.io/docs/) (client-hosted P2P), FishySteamworks and Steamworks.NET |
| Voice chat | Built-in proximity voice over Fish-Net (8 kHz mu-law, 3D audio), no extra service |
| Language | C# |

## Getting started

**You need:** Unity `6000.6.5f1` (install it with Unity Hub) and a working microphone.

1. Clone the repository. The project uses **Git LFS**, so install it first (`git lfs install`).
2. Open the folder in Unity Hub with the exact editor version above.
3. Open `Assets/Scenes/Dungeon_Main.unity` (the catacombs level). `Sandbox_Test.unity` is the small test arena.
   - If `Dungeon_Main` does not exist, use the menu **Echo Protocol > Build > Dungeon Scene (Catacombs)**.
4. Press **Play**, click **Host Local (No Steam)** in the panel (top left) and talk into your microphone. To test co-op, start a second instance (Multiplayer Play Mode or a build) and click **Join Local Client**. In a build, `EchoProtocol.exe -echo-host` and `EchoProtocol.exe -echo-join` skip the panel.

**Controls:** `WASD` move, `Mouse` look, `Space` jump, `Shift` sprint, `V` (hold) echo with your voice, `E` interact / pick up / place relic / pass the relic to the teammate you look at, `G` drop relic, `Esc` release the cursor.

**Debug keys** (Editor and Development builds only, they are removed from release builds): `N` add team noise, `B` make the nearest relic cry, `R` ragdoll yourself, `H` freeze the monsters, `J` toggle full illumination, `M` mute or unmute the relics, `T` switch relic personality, `Y` force a relic roast, `F3` show or hide the debug readouts. On the end screens, `R` restarts the match.

**Networking:** the in-game panel offers *Host Local* and *Join Local Client* (Tugboat, works without Steam) and *Host Steam Lobby*. The Steam lobby path is not connected to a Steam transport yet and I have not verified it (see the [networking limitations](docs/NETWORKING.md#5-current-limitations)). All the multiplayer setup is one prefab, `Assets/Prefabs/Networking/NetworkRig.prefab`, which the level builders install and which tests guard. `steam_appid.txt` contains `480` (the Spacewar test AppID). I need to replace it with the real AppID before a release. The `-echo-no-steam` flag runs the game without touching Steam.

**Microphone calibration:** the game calibrates itself, so it feels the same on any headset, laptop or room. It learns the noise floor of the microphone (the floor drops at once and rises slowly, so speech is never mistaken for room noise) and how loudly you talk. Normal speech always gives the same echo, about 7 m with the default curve, whatever the hardware. A scream 26 dB above your normal voice lights the whole cavern. The learned values show in the debug bar and in the log (`[MicrophoneLoudness] Calibration`).
- To measure the room again, right-click `MicrophoneLoudness` and choose **Calibrate Silence**, then stay quiet for 2 seconds.
- To use fixed values, turn *Auto Calibrate* off and set *Silence Db* and *Scream Db*. The manual defaults are -55 and -12 dB.
- To make the echo easier or harder to trigger without changing what the monster hears, change *Voice Sensitivity* on `EchoVisionController`. 1 is the original feel, and 1.3 needs about 23% less noise for every radius.

## Building and testing

- **Windows build:** use the menu **Echo Protocol > Build > Windows (Release)** (or *Development*). The output goes to `Builds/Windows/EchoProtocol.exe`, with `steam_appid.txt` copied next to it. From the command line: `Unity.exe -batchmode -quit -projectPath . -executeMethod EchoProtocol.EditorTools.EchoBuildPipeline.BuildWindowsRelease`.
- **Tests:** open *Window > General > Test Runner*.
  - **EditMode** covers the pure game logic: hearing falloff, the microphone calibrator, decibels to loudness with hysteresis, push to talk, the team noise meter, syllable detection and rhythm matching for the ritual, the relic's mood, the noise registry, relic dialogue tables, the visual setup and the dungeon map parser and validator.
  - **PlayMode** loads `Dungeon_Main` and checks that the level can be completed: spawns, relics, NavMesh paths to every objective and a full win through the exit portal. It also hosts a local server and checks the networked flow, including the team noise alarm, a crying relic starting the ritual, and a docked relic falling asleep.
- Debug shortcuts go through `DebugKeys`, so they only exist in the Editor and in Development builds.

## Project structure

```
Assets/
  Scripts/
    Core/          Shared interfaces and game-wide systems (INoiseSource, NoiseRegistry, OfflineSandboxGuard, DebugKeys)
    Audio/         Microphone capture, calibration, loudness analysis, noise emitters
    Player/        First-person controller and interaction (E / G)
    Echo/          Loudness to light sphere (the bridge to the shader)
    Enemies/       Hearing sensor, Idle / Investigate / Hunt state machine, catch reaction, procedural body
    Ragdoll/       Switching between the controller and the ragdoll
    Interaction/   IInteractable, carryable relics, traps
    Relics/        Relic brain, personalities, mood (crying), procedural voice, dialogue UI
    Rituals/       Team noise director, human echo ritual (syllable detection, rhythm), its HUD and music
    Altar/         Altar receptacle and sockets (they trigger the symphony)
    Extraction/    Escape portal and match flow (victory / defeat)
    Level/         Text-map dungeon parser and validator, spawn points
    Networking/    Fish-Net and Steam code (bootstrap, lobbies)
    Visuals/       Post-processing, camera setup, dust, photo mode
    UI/            Menus and HUD (planned)
    Editor/        Editor tools (dungeon and sandbox builders, Windows build)
  Shaders/         EchoReveal URP shader
  Art/  Audio/  Prefabs/  Scenes/  Settings/  ThirdParty/
```

## Documentation

- [Architecture and design decisions](docs/ARCHITECTURE.md)
- [Enemy AI: hearing model and state machine](docs/ENEMY_AI.md)
- [Player ragdoll](docs/RAGDOLL.md)
- [Networking architecture (Fish-Net and Steam)](docs/NETWORKING.md)
- [Living relics, altar and game loop](docs/LIVING_RELICS_DESIGN.md)
- [Team noise and relic rituals](docs/TEAM_NOISE_AND_RITUALS.md)
- [Level design: the catacombs](docs/LEVEL_DESIGN.md)
- [Visual design: shader, post-processing and photo mode](docs/VISUAL_DESIGN.md)
- [Contributing and Git workflow](CONTRIBUTING.md)
- [Changelog](CHANGELOG.md)

## Roadmap

- [x] Project setup, folder structure, coding conventions
- [x] Microphone loudness capture
- [x] Automatic microphone calibration
- [x] First-person controller
- [x] Echo vision shader and sandbox scene
- [x] Enemy AI: Idle, Investigate, Hunt, driven by player loudness
- [x] Ragdoll system
- [x] Multiplayer with Fish-Net: P2P client-host over a local transport
- [x] Networked player: spawning, ownership, NetworkTransform
- [x] Networked monster and multi-origin echo (up to 4 players)
- [x] Interaction and relic carrying
- [x] Living relics: personalities, procedural voices, noise that attracts the monster
- [x] Altar, symphony choir and extraction portal (victory and defeat flow)
- [x] Catacombs level generated from a text map, with spawn points and a validated, tested layout
- [x] Networked altar and extraction loop, with one relic per shrine (the server decides docking, the symphony, win or lose, and the restart)
- [x] Procedural look, post-processing, animated monster and photo mode
- [x] Steam lobby code (friends-only lobbies, invites)
- [ ] Steam transport wired and tested between two Steam accounts
- [ ] Retro look: low-resolution render and dithering (grain and vignette are already in)
- [ ] Ambience audio and monster sound effects
- [ ] Main menu, microphone calibration screen and a verified release build
- [x] Proximity voice chat over the game's own network (works on LAN and over Steam)

## Author

I'm Daniel ([@DanDavD](https://github.com/DanDavD)). Echo Protocol is a solo portfolio project.
