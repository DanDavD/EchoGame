# Audio design

Like the visuals, the game's sound effects use no audio files. Every sound is generated at runtime from maths in `ProceduralAudio` (fixed seeds, so it sounds the same on every machine), built once and cached. The relic voices (`RelicVoiceSynthesizer`) and the team noise music (`TeamNoiseAudio`) work the same way.

## What you hear

| Sound | Script | Behaviour |
|---|---|---|
| Room tone | `CaveAmbience` | A low hum with soft moving air that never stops. It loops every 8 s with a crossfaded seam, so you never hear the loop point. |
| Water drips | `CaveAmbience` | Every 2.5 to 7 s, a drip in 3D somewhere 4 to 14 m around you, with random pitch and volume. |
| Distant rumble | `CaveAmbience` | Every 35 to 80 s, a deep rumble of rock. |
| Monster breath | `MonsterAudio` | A 3D loop that gets faster and louder as the monster speeds up. Two monsters never breathe in sync. |
| Monster steps | `MonsterAudio` | Heavy thuds spaced by its speed (1.6 m stride). |
| Monster roar | `MonsterAudio` | When it starts to hunt: once per hunt, after a calm spell, with an 8 s cooldown. |
| Monster growl | `MonsterAudio` | Every 5 to 10 s, twice as often while it hunts, so players always have a hint of where it is. Breath, steps and growls carry up to 60 m. |
| Footsteps | `FootstepAudio` | Soft when walking, faster and louder when sprinting, silent in the air and while ragdolling. |

## Design decisions

- **Driven by movement, not by state.** On clients the monster's AI is disabled and only its movement is replicated, so the monster sounds and the footsteps read the horizontal speed, like `MonsterVisual`. Every peer hears the same thing with no extra network traffic. The rules live in `MovementSounds` (step pace, step volume, `RoarTrigger`) and are unit-tested.
- **Cosmetic only.** None of these sounds registers in `NoiseRegistry`. The monster hunts by the players' voices, relics and impacts, not by the ambience, and the team noise meter ignores it too. Making footsteps audible to the monster is a gameplay change for later.
- **Quiet on purpose.** A player without headphones feeds the speakers back into the microphone, and that would light the echo and alert the monster. The ambience volume is low, and the roar is the loudest sound, so it is never covered.
- **No scene setup.** `CaveAmbience` boots after the first scene loads and follows whichever `AudioListener` is active. `EnemyBrain` adds `MonsterAudio` and `PlayerController` adds `FootstepAudio`, so any prefab or level gets them.
- **A listener for the menus.** `Dungeon_Main` has no camera until a player spawns, so there is no listener in the menus. `CaveAmbience` carries a spare `AudioListener` that switches on only while no other one is active. It checks in `LateUpdate`, before Unity mixes the frame, so a player's camera and the spare one are never on together.

## Hearing through walls

`SoundOcclusion` sits on the monster's sources and on every player's footsteps. A few times a second it casts a ray from the sound to the listener and counts the solid level colliders in between (triggers, players, physics objects such as relics and the sound's own body are ignored). It then drives a low-pass filter on the source: 22 kHz with a clear line (the sound is untouched), 1.4 kHz behind one wall (dull, like the next room) and 600 Hz behind two or more (a distant thud). The cutoff glides, so a sound turning a corner fades into the muffle instead of clicking. Together with the long range of the monster's sounds, this lets players hunt and hide by ear: a clear breath means it is in your corridor.

## Volume settings

The master volume is `AudioListener.volume`, so it scales every sound, the relic voices included. The *Ambience and music* setting scales the cave ambience and the team noise music on top of it. Both are in the settings screen (main menu, and the `Esc` menu during a match).

## Tuning

Every volume, interval and distance is a serialized field on the component (`CaveAmbience`, `MonsterAudio`, `FootstepAudio`). To change what a sound is, edit its generator in `ProceduralAudio`. The tests check that no sound clips, that none is silent, that the room tone loops without a click and that the roar is louder than the background.

## Checks

- **EditMode:** `ProceduralAudioTests` and `MovementSoundsTests`.
- **PlayMode:** `GameAudioTests` (the ambience starts by itself, and walking plays footsteps at a walking pace).
