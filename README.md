# Echo Protocol — Co-op Multiplayer Horror in Unity 6

A cooperative horror-comedy game for up to 4 players where voice audio is the only source of light. Whispering illuminates a few meters around you; speaking expands visibility; screaming reveals the entire cavern, but alerts blind hearing monsters that hunt purely by sound.

- **Windows Release (v0.2.0):** [github.com/DanDavD/EchoGame/releases/latest](https://github.com/DanDavD/EchoGame/releases/latest)
- **Multiplayer Gameplay Video:** [youtu.be/14y3lMGHbK8](https://youtu.be/14y3lMGHbK8)
- **Author:** Daniel David ([@DanDavD](https://github.com/DanDavD))
- **Stack:** Unity 6, C#, HLSL, Universal Render Pipeline (URP), Fish-Net Networking, Steamworks SDK

---

## Executive Summary

- **Complete Vertical Slice (v0.2.0):** Shipped as a solo project featuring main menus, lobby waiting room, full procedural catacombs level, escape portal loop, and end-of-match state across LAN and Steam peer-to-peer sessions.
- **Microphone as Core Gameplay:** Player voice loudness directly drives real-time shader lighting, monster sensory perception, shared team noise risk, and localized 3D proximity chat.
- **Production QA & Network Testing:** Backed by **247 automated tests** (208 EditMode and 39 PlayMode), including end-to-end integration tests that host headless local servers, validate client-server state synchronization, and simulate match completion.

---

## Systems Engineering Highlights

### 1. Microphone DSP to Real-Time HLSL Shader Lighting
- Converts microphone input from raw RMS into calibrated decibels, applying dynamic attack and release smoothing to classify loudness into discrete states: **Silent**, **Whisper**, **Talk**, and **Scream**.
- Features automated self-calibration against ambient noise floors and hardware variations, ensuring balanced light propagation regardless of player microphone sensitivity.
- Loudness metrics feed directly into a custom URP HLSL fragment shader that dynamically renders cavern geometries, brick slabs, and environment depth exclusively where sound waves propagate.

### 2. Blind Sensor-Based Monster AI
- The enemy navigates via Unity's NavMesh without sight, relying entirely on an auditory sensor pipeline.
- Hearing stimuli feed into a hierarchical state machine: **Idle -> Investigate -> Hunt**.
- Whispers draw local curiosity; screams trigger immediate aggressive sprints toward the source coordinates.

### 3. Server-Authoritative Multiplayer (Fish-Net)
- Architected using Fish-Net in a client-host topology over LAN (Tugboat transport) and Steamworks (friends-only lobbies, invites, and Steam Relay network).
- The host server maintains authoritative simulation over monster positions, relic pickup states, altar docking logic, and victory portal validation, mitigating client-side desynchronization.

### 4. Custom Low-Latency Proximity Voice Chat
- Built directly into the game's network layer using an unreliable UDP channel with 8 kHz mu-law audio compression and a dedicated jitter buffer.
- Employs zero-allocation packet processing and 3D spatialized audio playback anchored to player avatar positions.
- Implements server-side proximity culling (voice packets are relayed exclusively to teammates within audible range) and token-bucket rate limiting to protect network throughput.

### 5. Procedural Graphics & Runtime Audio Synthesis
- Built without external static sprite or texture assets: environment visuals, brick patterns, and portal effects are computed mathematically at runtime.
- Ambient dungeon drone, footsteps, creature audio, and talking relic dialogue are synthesized programmatically, including real-time acoustic muffling through occluding walls.

---

## Gameplay Mechanics

| Voice Threshold | Visual Radius | Monster Behavior |
| :--- | :--- | :--- |
| **Whisper** | Small radius (~2 to 3 meters) | Ignored unless within point-blank proximity |
| **Talk** | Moderate radius (~7 meters) | Alerts nearby monsters to investigate sound origin |
| **Scream** | Full cavern illuminated | Triggers immediate enrage; monsters sprint to kill |

- **Living Relics:** 3 unique relics with procedural reactive dialogue must be found, carried, and docked at the central altar.
- **Shared Team Constraints:** Players share 3 collective lives and a central team noise meter. Filling the noise bar forces a beast attack on the loudest player.

---

## Architecture & Codebase Structure

```text
EchoGame/
├── Assets/
│   ├── Scripts/
│   │   ├── Audio/             # Real-time microphone capture, RMS calibration, and voice chat
│   │   ├── Gameplay/          # Player locomotion, relic mechanics, altar logic, team noise
│   │   ├── AI/                # Hearing sensor pipeline, state machine, and NavMesh movement
│   │   └── Networking/        # Fish-Net network managers, Steamworks lobby, server validation
│   ├── Shaders/               # Custom URP HLSL shaders computing sound-driven illumination
│   └── Tests/
│       ├── EditMode/          # 208 unit tests (microphone calibration, audio math, state logic)
│       └── PlayMode/          # 39 integration tests (local server hosting, network packet flows)
└── docs/                      # Architectural design documentation (Audio, Networking, Enemy AI)
```

---

## Running & Testing Locally

### Prerequisites
- Unity `6000.6.5f1`
- Git LFS installed
- USB or integrated microphone

### Launching in Unity Editor
1. Open the project in Unity Hub and load `Assets/Scenes/Dungeon_Main.unity`.
2. Press **Play**, select **Host Local**, and press **Start Match**.
3. Hold `V` and speak into your microphone to cast echo illumination.

### Running Automated Test Suites
Execute tests headlessly via the command line:
```bash
# Execute PlayMode integration and networking tests
Unity.exe -batchmode -nographics -projectPath . \
  -runTests -testPlatform PlayMode \
  -testResults results-playmode.xml
```

---

## Author & License

- **Developer:** Daniel David ([@DanDavD](https://github.com/DanDavD))
- **Status:** Complete vertical slice (v0.2.0)
- **License:** Open source under the [MIT License](LICENSE)
