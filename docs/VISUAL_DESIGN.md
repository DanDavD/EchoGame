# Visual Design

Echo Protocol has no art assets: every surface, creature and effect is generated in code and in one shader, so the look is
consistent, versioned and reviewable in a pull request. This page explains how it works and how it is checked.

## Art direction

The world is black. **Sound is light**: whatever your voice reaches is revealed, brick by brick, inside a growing sphere with
a glowing edge. Everything else does not exist. The look leans on that:

- one accent colour for the world (cold cyan), warm gold for what is alive or sacred (relics, the sanctuary light), red for the monster
- heavy darkness, a vignette and film grain, so what is revealed feels fragile
- voice-centred lighting: surfaces facing you are brighter, distance dims them

## The EchoReveal shader (`Assets/Shaders/EchoReveal.shader`)

One unlit URP shader draws the whole game. Its surface is **procedural**, projected in world space so scaled boxes never
stretch the pattern. `_Pattern` picks the material:

| Pattern | Used for | Look |
|---|---|---|
| 0 bricks | walls, ceiling | running-bond bricks with mortar, wear, cracks and a tone per brick |
| 1 slabs | floors | large irregular slabs with grout and dirt |
| 2 rock | pillars, altar, relics, dust | layered rock |
| 3 flesh | the monster | wet dark flesh with bright veins |
| 4 portal | the escape portal | dormant stone that fills with swirling energy when open (`_PortalActive`) |

Inside an echo sphere the shader adds: lighting from the sphere's centre, a pulse wave travelling towards the edge, a thin
rim light on silhouettes and an HDR edge ring that the bloom turns into a glow. An open portal is a beacon: its energy shows
in the dark, not only inside an echo. Up to four spheres (one per player) are published each frame by
`EchoShaderPublisher`.

The look of every material (pattern, tint, edge colour, emission) lives in **one place**, `EchoMaterials.Looks`
(menu *Echo Protocol → Build → Materials*). The level builders apply it, and an EditMode test checks that the assets in the
repo match it.

## Post-processing and camera (`Scripts/Visuals`)

- `PostProcessingRig` builds the look in code and creates a global Volume when a scene loads: ACES tonemapping, bloom
  (threshold just under 1, so only the HDR echo edges glow), vignette, film grain, a touch of chromatic aberration.
- `CameraPolish` turns on what URP leaves off by default for a gameplay camera: post-processing, HDR, SMAA and dithering.
- `DustMotes` drifts specks of dust around the player. They use the same shader, so they exist only where the echo lights the air.

## Creatures

`MonsterVisual` builds the blind monster from primitives and animates it in code: a gaunt body, enormous ears that flick and
open wide when it hunts, long arms that swing and reach forward. The animation depends only on how fast the monster moves, so
it looks the same on every peer (the server owns the real state). The old capsule keeps doing the physics.

## Reviewing the look with real frames: photo mode

```
EchoProtocol.exe -echo-photo [folder]
```

Hosts a local match, walks the player through a fixed set of viewpoints placed from the level's own markers (spawn in the
dark, whisper, talk, scream, altar, shrine, portal, monster, sanctuary light, open portal) and saves one PNG per shot, then
quits. The shots come from the real player camera, so post-processing, HUD and shaders are exactly what a player sees. Debug
overlays are hidden. Use a Development build (the viewpoints are chosen with the NavMesh, so the camera never ends up
inside a wall). The same tool produces press screenshots.

## Debug overlays

The microphone bar, monster state and networking panel (once connected) are debug readouts: visible in the Editor and in
Development builds, hidden in release builds. `F3` toggles them while developing.

## Checks

- EditMode: the look profile has every effect active; the camera polish settings; the photo flag and aiming maths; every game
  material has its defined pattern and the shader compiles.
- PlayMode: the monster builds its body, adds no colliders, swings its legs when it moves and leans into a chase.
