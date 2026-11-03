# Echo Protocol

A co-op horror-comedy game for up to 4 players where your voice is your only light. Whisper into your microphone and you see a few metres; scream and the whole cave lights up, but the blind monsters that hunt by sound now know exactly where you are.

![Unity](https://img.shields.io/badge/Unity-6000.6-black?logo=unity)
![URP](https://img.shields.io/badge/URP-17.6-blue)
![Fish-Net](https://img.shields.io/badge/multiplayer-Fish--Net%20%2B%20Steam-1b2838)
![Status](https://img.shields.io/badge/status-v0.2.0%20vertical%20slice-blue)

[![Gameplay video](https://img.youtube.com/vi/14y3lMGHbK8/maxresdefault.jpg)](https://youtu.be/14y3lMGHbK8)

**[Download for Windows](https://github.com/DanDavD/EchoGame/releases/latest)** | **[Watch the multiplayer gameplay video](https://youtu.be/14y3lMGHbK8)**

You need a microphone. For online co-op both players need Steam running and must be friends on Steam; on the same network, *Host Local* and *Join IP* work without Steam.

## In short

- **Solo project, a complete vertical slice (`v0.2.0`)**: menu, waiting room, a full level, the end screen and back, in co-op over LAN or Steam.
- **Voice is the core mechanic**: it lights the cave, draws the monster, fills a shared team noise meter, and carries proximity voice chat.
- **Over 240 automated tests** (EditMode and PlayMode), including PlayMode tests that host a local server and play the level to a win.

## The idea

A "friendslop" game for streamers: a few simple systems that create chaotic moments on their own, like accidental betrayals, panic and ragdoll falls. The echo is push to talk: your voice only lights the cave while you hold `V`.

| Holding `V`, you | The game does |
|---|---|
| **Whisper** | A small sphere of light, a few metres wide. |
| **Talk** | A bigger sphere (about 7 m at a normal voice). Monsters investigate. |
| **Scream** | The whole cavern is revealed, and so is your position. Monsters hunt you. |

Find the three living relics, carry them to the altar and escape through the portal before it closes. The team shares 3 lives and one noise meter: fill it and the beast charges whoever made the racket.

## Engineering highlights

- **Microphone to gameplay.** RMS to decibels to a 0-1 loudness with attack and release smoothing and Silent, Whisper, Talk and Scream states. It calibrates itself to each microphone's noise floor and the player's voice, so normal speech gives the same echo on any headset or room. See [Architecture](docs/ARCHITECTURE.md).
- **A blind monster that hunts by ear.** A hearing sensor feeds an Idle, Investigate, Hunt state machine that moves on a NavMesh. See [Enemy AI](docs/ENEMY_AI.md).
- **Server-authoritative multiplayer** with Fish-Net in client-host mode, over LAN (Tugboat) or Steam (friends-only lobbies, invites, the Steam relay). The server owns the monster, relics, altar and match flow, and validates what clients ask for: only the owner can pick up, dock or escape, and only near an open portal. See [Networking](docs/NETWORKING.md).
- **Proximity voice chat on the game's own network.** 8 kHz mu-law on the unreliable channel, a jitter buffer, 3D playback from the speaker's body, no allocations per packet. The server only relays a voice to players within hearing range and rate-limits each speaker with a token bucket.
- **Procedural look and sound, no art or audio files.** One URP HLSL shader draws bricks, slabs, rock, flesh and the portal, lit only by the sound that reveals them, plus post-processing and a monster animated in code. The ambience, monster, footsteps and talking relics' voices are generated at runtime, and walls muffle the sounds behind them. See [Visual design](docs/VISUAL_DESIGN.md) and [Audio design](docs/AUDIO_DESIGN.md).
- **Data-driven level and a scripted build.** The catacombs come from a text map with a parser and a validator under test, and a one-command batch build produces the Windows release. See [Level design](docs/LEVEL_DESIGN.md).

## Screenshots

| Before: placeholder checkerboard | After: the procedural look |
|---|---|
| ![Before](docs/media/look-before-checkerboard.png) | ![After](docs/media/look-after-corridor.png) |
| ![The blind monster](docs/media/monster-closeup.png) | ![The escape portal](docs/media/portal-open.png) |

## What is in the game

- Living relics with personalities, procedural voices and jokes, that react to the microphone, sprinting and the monster. Hand one to a teammate with `E`.
- The altar, a symphony choir that stuns the monster, and an escape portal against the clock.
- A shared team noise meter, shared team lives, a physics ragdoll for catches and traps, and Steam name tags above teammates.
- First-match hints, a match menu, and settings for volumes, mouse, microphone device and echo sensitivity with a live test bar.

## Getting started

You need Unity `6000.6.5f1`, Git LFS (`git lfs install` before cloning) and a microphone.

1. Open the folder in Unity Hub and open `Assets/Scenes/Dungeon_Main.unity`.
2. Press **Play**, click **Host Local**, then **Start Match**, and talk into your microphone.
3. For co-op, start a second instance and click **Join IP** before the host starts.

**Controls:** `WASD` move, mouse look, `Space` jump, `Shift` sprint, `V` (hold) echo with your voice, `E` interact or pass the relic, `G` drop it, `Esc` match menu.

**Build:** *Echo Protocol > Build > Windows (Release)*, or from the command line `Unity.exe -batchmode -quit -projectPath . -executeMethod EchoProtocol.EditorTools.EchoBuildPipeline.BuildWindowsRelease`. **Tests:** *Window > General > Test Runner*, or `Unity.exe -batchmode -nographics -projectPath . -runTests -testPlatform PlayMode -testResults results.xml`.

Microphone calibration, Steam setup and the debug keys are described in [Architecture](docs/ARCHITECTURE.md) and [Networking](docs/NETWORKING.md).

## Known limits

- If the host leaves, the match ends for everyone (no host migration), and a player who drops cannot rejoin their old avatar.
- No browser version: co-op needs UDP or the Steam relay, and the game needs the microphone.
- Steam co-op needs two accounts, so I test it by hand between two PCs; LAN play is covered by automated tests.

## Documentation

[Architecture](docs/ARCHITECTURE.md) | [Enemy AI](docs/ENEMY_AI.md) | [Networking](docs/NETWORKING.md) | [Team noise](docs/TEAM_NOISE.md) | [Living relics](docs/LIVING_RELICS_DESIGN.md) | [Level design](docs/LEVEL_DESIGN.md) | [Visual design](docs/VISUAL_DESIGN.md) | [Audio design](docs/AUDIO_DESIGN.md) | [Ragdoll](docs/RAGDOLL.md) | [Changelog](CHANGELOG.md) | [Contributing](CONTRIBUTING.md)

## Roadmap

- [x] `v0.1.0` First public release: the voice-driven echo, the blind monster, relics, altar and portal, co-op over LAN and Steam
- [x] `v0.2.0` Vertical slice: proximity voice chat, procedural soundscape, settings, team lives, first-match hints and a server that validates clients
- [ ] Retro look: low-resolution render and dithering

I'm Daniel ([@DanDavD](https://github.com/DanDavD)). Echo Protocol is a solo portfolio project.
