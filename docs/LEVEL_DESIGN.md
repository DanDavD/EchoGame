# Level Design: the Catacombs

The playable level is **generated from a text map**, so changing the layout is a text edit followed by one menu click. Nothing in the scene is hand-placed.

```
Assets/Level/Catacombs.txt         the map (source of truth)
Assets/Scripts/Level/DungeonGrid   parses and validates the map (pure C#, unit-tested)
Assets/Scripts/Level/SpawnPoint    markers the match flow reads instead of hardcoded coordinates
Assets/Scripts/Editor/DungeonBuilder   map -> Dungeon_Main.unity (menu: Echo Protocol > Build > Dungeon Scene)
```

## The map format

One character per cell, one cell = 5 m, first row is the **north** edge. Lines that are empty or start with `;` are ignored.

| Char | Meaning |
|---|---|
| `#` | wall (6 m high) |
| `.` | floor |
| `S` | player start (exactly one) |
| `R` | relic shrine (exactly three, one per relic personality) |
| `A` | centre of the altar (exactly one) |
| `E` | exit portal, built against the adjacent wall (at least one) |
| `M` | monster spawn (at least one) |
| `T` | ragdoll trap plate |
| `P` | pillar |

## Validation

`DungeonGrid.Validate()` refuses to build a level that cannot be completed: wrong number of starts/altars/shrines, a missing exit or monster, a border that is not sealed, or any objective that cannot be reached on foot from the start (flood fill). The same check runs as a unit test against the shipped `Catacombs.txt`, so a broken map fails the test suite before it reaches a build.

## What the builder produces

- one floor slab, one ceiling slab and **merged wall runs** (a few long boxes per row instead of one box per wall cell)
- pillars, ragdoll traps, the altar with its three sockets and the exit portal
- a baked NavMesh saved next to the scene (`Assets/Scenes/Dungeon_Main/NavMesh-Dungeon.asset`), baked after the altar and portal exist and before the player and monster are created
- a `SpawnPoints` group: two player slots, three relic shrines (one personality each) and the monster spawn
- FishNet scene ids for the scene's `NetworkObject`s (they are assigned in `OnValidate`, which scripts never trigger)

## Spawning

`SpawnRegistry` is the single place that answers "where does X appear":

- `GameExtractionManager` places the three relics at the `Relic` spawn points (falls back to the sandbox positions when a scene has none).
- `NetworkBootstrap` uses the `Player` spawn points for joining players and the `Enemy` / `Relic` ones for networked spawns.

Any new level only has to place `SpawnPoint` components.

## Automated checks

- **EditMode** (`DungeonGridTests`): parsing, validation rules, world coordinates, wall merging and the shipped map.
- **PlayMode** (`DungeonSceneTests`): loads `Dungeon_Main` and checks that every system the match needs exists, relics appear at their shrines, the monster stands on the NavMesh, a NavMesh path links the start to the altar, every shrine and the exit, and that the match can actually be won (altar symphony unlocks the portal, entering it ends in victory).
