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
- Altar, symphony choir and escape portal: the full objective → extraction → victory/defeat loop (currently local/offline-first, see [networking limitations](docs/NETWORKING.md#5-current-limitations)).
- One-click sandbox scene builder (editor tool) with baked NavMesh and test arena.

**In progress / planned:** modular dungeon level, ambience audio, PS1-style post-processing, menu and release build, proximity voice chat. See the [roadmap](#roadmap).

## Tech Stack

| Area | Choice |
|---|---|
| Engine | Unity 6 (`6000.6.4f1`) |
| Rendering | Universal Render Pipeline (URP 17.6), custom HLSL shader |
| Input | Unity Input System (`InputSystem_Actions.inputactions`) |
| Networking | [Fish-Net](https://fish-networking.gitbook.io/docs/) (client-hosted P2P) + FishySteamworks + Steamworks.NET |
| Voice chat *(planned)* | Proximity voice (Dissonance or Steam Voice) |
| Language | C# |

## Getting Started

**Requirements:** Unity `6000.6.4f1` (via Unity Hub) and a working microphone.

1. Clone the repository. The project uses **Git LFS**, so install it first (`git lfs install`).
2. Open the folder in Unity Hub with the exact editor version above.
3. Open `Assets/Scenes/Sandbox_Test.unity`.
   - If it does not exist, use the menu **Echo Protocol → Build Sandbox Scene**.
4. Press **Play** and talk into your microphone.

**Controls:** `WASD` move · `Mouse` look · `Space` jump · `Shift` sprint · `E` interact / pick up / place relic · `G` drop relic · `Esc` release cursor.

**Debug keys (prototype only):** `V` simulated voice burst · `R` ragdoll yourself · `H` freeze monsters · `J` toggle full illumination · `T` switch relic personality · `Y` force a relic roast. In extraction end screens `R` restarts the match.

**Networking:** the in-game panel offers *Host Steam Lobby*, *Host Local* and *Join Local Client* (Tugboat, for testing with Multiplayer Play Mode). `steam_appid.txt` contains `480` (Spacewar test AppID); replace it with the real AppID before a release.

**Calibration:** if the loudness bar in the top-left corner does not react well to your voice, select the Player and either tweak *Silence Db* / *Scream Db* on `MicrophoneLoudness`, or right-click the component → **Calibrate Silence** (stay quiet for 2 seconds).

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
│   ├── Networking/    # Fish-Net / Steam code (bootstrap, lobbies)
│   ├── UI/            # (planned) Menus and HUD
│   └── Editor/        # Editor tooling (sandbox scene builder)
├── Shaders/           # EchoReveal URP shader
├── Art/  Audio/  Prefabs/  Scenes/  Settings/  ThirdParty/
```

## Documentation

- 📐 [Architecture & design decisions](docs/ARCHITECTURE.md)
- 👹 [Enemy AI: hearing model & state machine](docs/ENEMY_AI.md)
- 🤸 [Player ragdoll](docs/RAGDOLL.md)
- 🌐 [Networking architecture (Fish-Net + Steam)](docs/NETWORKING.md)
- 🗿 [Living relics, altar & game loop](docs/LIVING_RELICS_DESIGN.md)
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
- [ ] Modular dungeon level (replaces the flat sandbox arena)
- [ ] Ambience audio, monster SFX and retro/dither VFX
- [ ] Main menu, mic calibration UI and verified release build
- [ ] Network the altar / extraction loop and relic spawning
- [ ] Proximity voice chat (TBD: Steam Voice or Dissonance)

## Author

Built by [@DanDavD](https://github.com/DanDavD) as a solo portfolio project.
