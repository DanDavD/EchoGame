# Echo Protocol

> A co-op "friendslop" horror-comedy where **your voice is your only light**.
> The cave is pitch black. Whisper to see a few metres. Scream to light the whole cavern, and reveal exactly where you are to the blind monsters that hunt by sound.

![Unity](https://img.shields.io/badge/Unity-6000.6-black?logo=unity)
![Render Pipeline](https://img.shields.io/badge/URP-17.6-blue)
![Status](https://img.shields.io/badge/status-prototype-orange)

<!-- TODO: replace with a short GIF of the echolocation prototype (docs/media/echo-demo.gif) -->
> 🎬 *Demo GIF coming soon.*

---

## The Idea

Echo Protocol is designed as a "viral clip machine" for streamers: simple systemic mechanics that
produce chaotic, shareable moments (accidental betrayal, panic, ragdoll falls) instead of large amounts of content.

| You do… | The game does… |
|---|---|
| **Stay silent** | You are blind. Nothing is revealed. |
| **Whisper** | A small sphere of light (~1–3 m) appears around you. |
| **Talk** | A medium sphere (~12 m) lights up. Monsters start to investigate. |
| **Scream** | The whole cavern is revealed, *and* so is your position. Monsters hunt you. |

## Screenshots

The same corridor before and after the procedural look (captured with the built-in photo mode, `-echo-photo`):

| Before: placeholder checkerboard | After: procedural bricks, slabs, dust and post-processing |
|---|---|
| ![Before](docs/media/look-before-checkerboard.png) | ![After](docs/media/look-after-corridor.png) |

| The blind monster | The escape portal, open |
|---|---|
| ![Monster](docs/media/monster-closeup.png) | ![Portal](docs/media/portal-open.png) |

## Current Status

This is an active prototype built solo under a tight deadline. Progress is tracked in the [roadmap](#roadmap) and the [changelog](CHANGELOG.md).

**Implemented:**
- Microphone capture → RMS → calibrated 0–1 loudness, with attack/release smoothing and Silent/Whisper/Talk/Scream states.
- "Clunky on purpose" first-person controller (new Input System).
- Echo vision: loudness drives spheres of light (up to 4 players) through a URP shader using global shader values.
- Blind enemy AI: sound-driven hearing sensor with Idle / Investigate / Hunt finite state machine on NavMesh.
- Physics-driven player ragdoll (enemy catches, traps) that makes noise on impact.
- Multiplayer: Fish-Net client-host over Steam (FishySteamworks + Steamworks.NET), Steam lobbies, networked players, enemy and echo pulses.
- Living relics: carryable, talking relics with procedural voices, comedic dialogue and mic/sprint/monster reactions.
- Altar, symphony choir and escape portal: the full objective → extraction → victory/defeat loop, decided by the server in multiplayer and playable offline in the sandbox (see [networking](docs/NETWORKING.md)).
- One-click sandbox scene builder (editor tool) with baked NavMesh and test arena.
- Procedural look with no art assets: one shader draws brick walls, stone slabs, rock, flesh and an energy portal, lit from the sound that reveals them, with post-processing (bloom, vignette, grain), drifting dust and a procedurally animated monster. A photo mode (`-echo-photo`) takes reference screenshots of the level. See [Visual design](docs/VISUAL_DESIGN.md).

**In progress / planned:** modular dungeon level, ambience audio, PS1-style post-processing, menu and release build, proximity voice chat. See the [roadmap](#roadmap).

## Tech Stack

| Area | Choice |
|---|---|
| Engine | Unity 6 (`6000.6.5f1`) |
| Rendering | Universal Render Pipeline (URP 17.6), custom HLSL shader |
| Input | Unity Input System (`InputSystem_Actions.inputactions`) |
| Networking | [Fish-Net](https://fish-networking.gitbook.io/docs/) (client-hosted P2P) + FishySteamworks + Steamworks.NET |
| Voice chat *(planned)* | Proximity voice (Dissonance or Steam Voice) |
| Language | C# |

## Getting Started

**Requirements:** Unity `6000.6.5f1` (via Unity Hub) and a working microphone.

1. Clone the repository. The project uses **Git LFS**, so install it first (`git lfs install`).
2. Open the folder in Unity Hub with the exact editor version above.
3. Open `Assets/Scenes/Dungeon_Main.unity` (the catacombs level). `Sandbox_Test.unity` is the small test arena.
   - If `Dungeon_Main` does not exist, use the menu **Echo Protocol → Build → Dungeon Scene (Catacombs)**.
4. Press **Play**, click **Host Local (No Steam)** in the panel (top-left) and talk into your microphone. To test co-op, start a second instance (Multiplayer Play Mode or a build) and click **Join Local Client**. In a build, `EchoProtocol.exe -echo-host` and `EchoProtocol.exe -echo-join` skip the panel.

**Controls:** `WASD` move · `Mouse` look · `Space` jump · `Shift` sprint · `E` interact / pick up / place relic · `G` drop relic · `Esc` release cursor.

**Debug keys (Editor and Development builds only, stripped from release builds):** `V` simulated voice burst · `R` ragdoll yourself · `H` freeze monsters · `J` toggle full illumination · `M` mute / unmute relic chatter · `T` switch relic personality · `Y` force a relic roast · `F3` show / hide the debug readouts. In extraction end screens `R` restarts the match.

**Networking:** the in-game panel offers *Host Local* and *Join Local Client* (Tugboat, works without Steam) and *Host Steam Lobby*. The Steam lobby path is not wired to a Steam transport yet and is unverified (see [networking limitations](docs/NETWORKING.md#5-current-limitations)). All multiplayer wiring is one prefab, `Assets/Prefabs/Networking/NetworkRig.prefab`, installed by the level builders and guarded by tests. `steam_appid.txt` contains `480` (Spacewar test AppID); replace it with the real AppID before a release. `-echo-no-steam` runs the game without touching Steam.

**Calibration:** the microphone calibrates itself, so the game feels the same on any headset, laptop or room. It learns the noise floor of the microphone (it drops at once and rises slowly, so speech is never mistaken for the room) and how loudly you talk, and maps normal speech to the same echo (about 7 m with the default curve) whatever the hardware; a scream 26 dB above your normal voice lights the whole cavern. The learned values appear in the debug bar and in the log (`[MicrophoneLoudness] Calibration`). To re-measure the room, right-click `MicrophoneLoudness` → **Calibrate Silence** (stay quiet for 2 seconds). To use fixed values instead, turn *Auto Calibrate* off and set *Silence Db* / *Scream Db* (the manual fallback is -55 / -12 dB). To make the echo easier or harder to trigger without touching the monster's hearing, change *Voice Sensitivity* on `EchoVisionController` (1 = original, 1.3 = about 23% less noise for every radius).

## Building and Testing

- **Windows build:** menu **Echo Protocol → Build → Windows (Release)** (or *Development*). Output goes to `Builds/Windows/EchoProtocol.exe`, with `steam_appid.txt` copied next to it. Headless: `Unity.exe -batchmode -quit -projectPath . -executeMethod EchoProtocol.EditorTools.EchoBuildPipeline.BuildWindowsRelease`.
- **Tests:** *Window → General → Test Runner*. **EditMode** covers the pure game logic (hearing falloff, microphone dB → loudness buckets with hysteresis, the noise registry, relic dialogue tables and the dungeon map parser/validator). **PlayMode** loads `Dungeon_Main` and checks the level is completable: spawns, relics, NavMesh paths to every objective, and a full win through the exit portal.
- Debug shortcuts go through `DebugKeys`, so they only exist in the Editor and in Development builds.

## Project Structure

```
Assets/
├── Scripts/
│   ├── Core/          # Shared interfaces and game-wide systems (INoiseSource, NoiseRegistry, OfflineSandboxGuard)
│   ├── Audio/         # Microphone capture, loudness analysis, noise emitters
│   ├── Player/        # First-person controller and interaction (E / G)
│   ├── Echo/          # Loudness → light sphere (shader bridge)
│   ├── Enemies/       # Hearing sensor + state machine AI (Idle/Investigate/Hunt) + catch reaction
│   ├── Ragdoll/       # Controller ⇄ ragdoll switching
│   ├── Interaction/   # IInteractable, carryable relics, traps
│   ├── Relics/        # Living relic brain, personalities, procedural voice, dialogue UI
│   ├── Altar/         # Altar receptacle and sockets (symphony trigger)
│   ├── Extraction/    # Escape portal and match flow (victory / defeat)
│   ├── Level/         # Text-map dungeon parser/validator and spawn points
│   ├── Networking/    # Fish-Net / Steam code (bootstrap, lobbies)
│   ├── UI/            # (planned) Menus and HUD
│   └── Editor/        # Editor tooling (dungeon + sandbox builders, Windows build pipeline)
├── Shaders/           # EchoReveal URP shader
├── Art/  Audio/  Prefabs/  Scenes/  Settings/  ThirdParty/
```

## Documentation

- 📐 [Architecture & design decisions](docs/ARCHITECTURE.md)
- 👹 [Enemy AI: hearing model & state machine](docs/ENEMY_AI.md)
- 🤸 [Player ragdoll](docs/RAGDOLL.md)
- 🌐 [Networking architecture (Fish-Net + Steam)](docs/NETWORKING.md)
- 🗿 [Living relics, altar & game loop](docs/LIVING_RELICS_DESIGN.md)
- 🕳️ [Level design: the catacombs](docs/LEVEL_DESIGN.md)
- 🎨 [Visual design: shader, post-processing and photo mode](docs/VISUAL_DESIGN.md)
- 🤝 [Contributing & Git workflow](CONTRIBUTING.md)
- 📝 [Changelog](CHANGELOG.md)

## Roadmap

- [x] Project setup, folder structure, coding conventions
- [x] Microphone loudness capture
- [x] First-person controller
- [x] Echo vision shader + sandbox scene
- [x] Enemy AI: Idle → Investigate → Hunt, driven by player loudness
- [x] Ragdoll system
- [ ] PS1 look: low-res render, film grain, vignette
- [x] Multiplayer (Fish-Net, FishySteamworks, Steamworks.NET): P2P client-host
- [x] Steam integration: Lobby matchmaking, friends overlay (AppID 480)
- [x] Networked Player: Instantiation, ownership, NetworkTransform
- [x] Networked enemy and multi-origin echo scanner (up to 4 players)
- [x] Interaction and relic carrying
- [x] Living relics: personalities, procedural voices, monster-attracting noise
- [x] Altar, symphony choir and extraction portal (victory / defeat flow)
- [x] Catacombs dungeon level generated from a text map, with spawn points and a validated, tested layout
- [ ] Ambience audio, monster SFX and retro/dither VFX
- [ ] Main menu, mic calibration UI and verified release build
- [x] Network the altar / extraction loop and relic spawning (server-decided docking, symphony, win/lose and restart; one relic per shrine)
- [ ] Proximity voice chat (TBD: Steam Voice or Dissonance)

## Author

Built by [@DanDavD](https://github.com/DanDavD) as a solo portfolio project.
