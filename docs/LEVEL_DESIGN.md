# Level design: the catacombs

The playable level is **generated from a text map**. To change the layout I edit the text and click one menu item. Nothing in the scene is placed by hand.

```
Assets/Level/Catacombs.txt             the map (the source of truth)
Assets/Scripts/Level/DungeonGrid       parses and validates the map (plain C#, unit-tested)
Assets/Scripts/Level/SpawnPoint        markers that the match flow reads instead of hardcoded coordinates
Assets/Scripts/Editor/DungeonBuilder   builds Dungeon_Main.unity from the map (menu: Echo Protocol > Build > Dungeon Scene)
```

## The map format

One character per cell, and one cell is 5 m. The first row is the **north** edge. Empty lines and lines that start with `;` are ignored.

| Char | Meaning |
|---|---|
| `#` | Wall (6 m high) |
| `.` | Floor |
| `S` | Player start (exactly one) |
| `R` | Relic shrine (exactly three, one for each relic personality) |
| `A` | Centre of the altar (exactly one) |
| `E` | Exit portal, built against the wall next to it (at least one) |
| `M` | Monster spawn (at least one) |
| `T` | Ragdoll trap plate |
| `P` | Pillar |

## Validation

`DungeonGrid.Validate()` refuses to build a level that cannot be completed. It rejects the wrong number of starts, altars or shrines, a missing exit or monster, a border that is not sealed, and any objective that cannot be reached on foot from the start (it uses a flood fill). The same check runs as a unit test against the shipped `Catacombs.txt`, so a broken map fails the test suite before it can reach a build.

## What the builder produces

- One floor slab, one ceiling slab and **merged wall runs** (a few long boxes per row instead of one box for every wall cell).
- Pillars, ragdoll traps, the altar with its three sockets and the exit portal.
- A baked NavMesh saved next to the scene (`Assets/Scenes/Dungeon_Main/NavMesh-Dungeon.asset`). It is baked after the altar and the portal exist and before the player and the monster are created.
- A `SpawnPoints` group with two player slots, three relic shrines (one personality each) and the monster spawn.
- FishNet scene ids for the `NetworkObject`s in the scene. They are assigned in `OnValidate`, which scripts never trigger on their own.

## Spawning

`SpawnRegistry` is the one place that answers "where does X appear?":

- `GameExtractionManager` puts the three relics at the `Relic` spawn points. It falls back to the sandbox positions when a scene has none.
- `NetworkBootstrap` uses the `Player` spawn points for players who join, and the `Enemy` and `Relic` ones for networked spawns.

A new level only has to place `SpawnPoint` components.

## Automated checks

- **EditMode** (`DungeonGridTests`): parsing, the validation rules, world coordinates, wall merging and the shipped map.
- **PlayMode** (`DungeonSceneTests`): loads `Dungeon_Main` and checks that every system the match needs exists, that relics appear at their shrines, that the monster stands on the NavMesh, that a NavMesh path links the start to the altar, every shrine and the exit, and that the match can really be won (the altar symphony unlocks the portal, and entering it ends in victory).
