# Visual design

Echo Protocol has no art assets. Every surface, creature and effect is generated in code and in one shader, so the look stays consistent, is versioned with the project and can be reviewed in a pull request. This page explains how it works and how I check it.

## Art direction

The world is black. **Sound is light**: whatever your voice reaches is revealed, brick by brick, inside a growing sphere with a glowing edge. Everything else does not exist. The look follows from that:

- One accent colour for the world (cold cyan), warm gold for what is alive or sacred (relics, the sanctuary light) and red for the monster.
- Heavy darkness, a vignette and film grain, so that what you reveal feels fragile.
- Lighting that comes from the voice: surfaces that face you are brighter, and distance makes them darker.

## The EchoReveal shader (`Assets/Shaders/EchoReveal.shader`)

One unlit URP shader draws the whole game. Its surface is **procedural** and projected in world space, so scaled boxes never stretch the pattern. The `_Pattern` property picks the material:

| Pattern | Used for | Look |
|---|---|---|
| 0 bricks | Walls, ceiling | Running-bond bricks with mortar, wear, cracks and a different tone for each brick |
| 1 slabs | Floors | Large irregular slabs with grout and dirt |
| 2 rock | Pillars, altar, relics, dust | Layered rock |
| 3 flesh | The monster | Wet dark flesh with bright veins |
| 4 portal | The escape portal | Dormant stone that fills with swirling energy when the portal opens (`_PortalActive`) |

Inside an echo sphere the shader adds lighting from the centre of the sphere, a pulse wave that travels towards the edge, a thin rim light on silhouettes and an HDR edge ring that the bloom turns into a glow. An open portal works as a beacon: its energy shows in the dark, not only inside an echo. `EchoShaderPublisher` sends up to four spheres (one per player) to the shader every frame.

The look of every material (pattern, tint, edge colour, emission) is defined in **one place**: `EchoMaterials.Looks`, which you can apply with the menu *Echo Protocol > Build > Materials*. The level builders apply it too, and an EditMode test checks that the assets in the repo match it.

## Post-processing and camera (`Scripts/Visuals`)

- `PostProcessingRig` builds the look in code and creates a global Volume when a scene loads: ACES tonemapping, bloom (the threshold is just under 1, so only the HDR echo edges glow), vignette, film grain and a little chromatic aberration.
- `CameraPolish` turns on what URP leaves off by default for a gameplay camera: post-processing, HDR, SMAA and dithering.
- `DustMotes` drifts specks of dust around the player. They use the same shader, so they only exist where the echo lights the air.

## Creatures

`MonsterVisual` builds the blind monster from primitives and animates it in code. It has a gaunt body, huge ears that flick and open wide when it hunts, and long arms that swing and reach forward. The animation depends only on how fast the monster moves, so it looks the same on every peer (the server owns the real state). The old capsule still does the physics.

## Reviewing the look with real frames: photo mode

```
EchoProtocol.exe -echo-photo [folder]
```

This hosts a local match and takes the player to a fixed set of viewpoints, chosen from the level's own markers: the spawn in the dark, a whisper, talking, a scream, the altar, a shrine, the portal, the monster, the sanctuary light and the open portal. It saves one PNG for each viewpoint and then quits. The shots come from the real player camera, so post-processing, HUD and shaders are exactly what a player sees. The debug overlays are hidden. The viewpoints are chosen with the NavMesh, so the camera never ends up inside a wall. I also use this tool to make the screenshots for the README and for press material. Run it from a release build for clean shots, because a Development build draws a "Development Build" watermark in the corner of every frame.

## Debug overlays

The microphone bar, the monster state and the networking panel (once connected) are debug readouts. They show in the Editor and in Development builds, and they are hidden in release builds. `F3` toggles them while I develop.

## Checks

- **EditMode:** the look profile has every effect active, the camera settings, the photo flag and the aiming maths, every game material has its defined pattern, and the shader compiles.
- **PlayMode:** the monster builds its body, adds no colliders, swings its legs when it moves and leans into a chase.
