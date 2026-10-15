using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using EchoProtocol.Level;
using EchoProtocol.Relics;

namespace EchoProtocol.EditorTools
{
    /// <summary>
    /// Generates the playable catacombs level from the ASCII map in <c>Assets/Level/Catacombs.txt</c>.
    /// Menu: Echo Protocol > Build > Dungeon Scene (Catacombs). Edit the text map, rebuild, done.
    /// The scene is fully derived data: floor, merged wall runs, ceiling, pillars, traps, altar, exit portal,
    /// spawn points for players / relics / monster, a baked NavMesh saved next to the scene and the network rig.
    /// </summary>
    public static class DungeonBuilder
    {
        public const string LayoutPath = "Assets/Level/Catacombs.txt";
        public const string ScenePath = "Assets/Scenes/Dungeon_Main.unity";
        private const string NavMeshDir = "Assets/Scenes/Dungeon_Main";

        /// <summary>Edge length of one map cell in metres.</summary>
        public const float CellSize = 5f;

        /// <summary>Height of the walls (and the ceiling above them) in metres.</summary>
        public const float WallHeight = 6f;

        private static readonly RelicPersonalityType[] RelicOrder =
        {
            RelicPersonalityType.SarcasticCritic,
            RelicPersonalityType.AnxiousCoward,
            RelicPersonalityType.DumbGloop
        };

        private static readonly Vector2Int[] Directions =
        {
            new Vector2Int(0, -1), new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(-1, 0)
        };

        [MenuItem("Echo Protocol/Build/Dungeon Scene (Catacombs)")]
        public static void BuildCatacombs()
        {
            var layoutAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(LayoutPath);
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(EchoSandboxBuilder.ShaderPath);
            if (layoutAsset == null || shader == null)
            {
                Debug.LogError($"[DungeonBuilder] Missing layout ({LayoutPath}) or shader.");
                return;
            }

            DungeonGrid grid;
            try
            {
                grid = DungeonGrid.Parse(layoutAsset.text);
            }
            catch (System.FormatException e)
            {
                Debug.LogError($"[DungeonBuilder] {LayoutPath}: {e.Message}");
                return;
            }

            var errors = grid.Validate(RelicOrder.Length);
            if (errors.Count > 0)
            {
                Debug.LogError($"[DungeonBuilder] {LayoutPath} is not playable:\n - " + string.Join("\n - ", errors));
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Texture2D checker = EchoSandboxBuilder.CreateCheckerTexture();
            Material floorMat = EchoSandboxBuilder.GetOrCreateMaterial("Assets/Art/Materials/Echo_Floor.mat", shader, checker, new Color(0.55f, 0.55f, 0.6f), 0.5f);
            Material wallMat = EchoSandboxBuilder.GetOrCreateMaterial("Assets/Art/Materials/Echo_Wall.mat", shader, checker, new Color(0.65f, 0.45f, 0.45f), 0.5f);
            Material propMat = EchoSandboxBuilder.GetOrCreateMaterial("Assets/Art/Materials/Echo_Prop.mat", shader, checker, new Color(0.45f, 0.65f, 0.55f), 1f);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Pitch black lighting: the echo shader is the only thing that reveals the geometry.
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Color.black;
            RenderSettings.reflectionIntensity = 0f;

            Transform root = new GameObject("Dungeon").transform;
            BuildGeometry(grid, root, floorMat, wallMat, propMat);

            Vector2Int altar = grid.Find(DungeonGrid.Altar)[0];
            Vector2Int exit = grid.Find(DungeonGrid.Exit)[0];
            Vector2Int wallDir = WallDirection(grid, exit);
            Vector3 wallOffset = new Vector3(wallDir.x, 0f, -wallDir.y) * (CellSize * 0.5f - 0.2f);
            Quaternion portalRot = Quaternion.LookRotation(-wallOffset.normalized);
            EchoSandboxBuilder.SetupAltarAndEscapePortal(
                root, propMat,
                grid.CellToWorld(altar.x, altar.y, CellSize),
                grid.CellToWorld(exit.x, exit.y, CellSize) + wallOffset,
                portalRot);

            // Bake after the altar and portal exist: they block paths.
            BuildNavMesh(root.gameObject);

            // Players, the monster and the relics are spawned by the network (NetworkBootstrap) at these markers, so the
            // scene holds no Player or Enemy of its own: a scene copy would duplicate the spawned one.
            Vector2Int start = grid.Find(DungeonGrid.Start)[0];
            Vector3 startPos = grid.CellToWorld(start.x, start.y, CellSize);
            Quaternion startRot = Quaternion.LookRotation(OpenDirection(grid, start));
            Vector2Int monster = grid.Find(DungeonGrid.Monster)[0];
            Vector3 monsterPos = grid.CellToWorld(monster.x, monster.y, CellSize);
            BuildSpawnPoints(grid, root, startPos, startRot, monsterPos);

            // The level is networked: install the rig (and keep it there on every rebuild).
            if (NetworkRigInstaller.Ensure(scene) == null)
            {
                Debug.LogError("[DungeonBuilder] Could not install the network rig; aborting so a scene without multiplayer is not saved.");
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);

            // Scene ids need a saved (named) scene, so assign them now and save again.
            InitializeSceneNetworkObjects();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            RegisterInBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log($"[DungeonBuilder] Built {ScenePath} from {LayoutPath} ({grid.Width}x{grid.Height} cells).");
        }

        private static void BuildGeometry(DungeonGrid grid, Transform root, Material floorMat, Material wallMat, Material propMat)
        {
            float sizeX = grid.Width * CellSize;
            float sizeZ = grid.Height * CellSize;

            EchoSandboxBuilder.Box("Floor", root, new Vector3(0f, -0.5f, 0f), new Vector3(sizeX, 1f, sizeZ), floorMat);

            GameObject ceiling = EchoSandboxBuilder.Box("Ceiling", root, new Vector3(0f, WallHeight + 0.25f, 0f), new Vector3(sizeX, 0.5f, sizeZ), wallMat);
            IgnoreInNavMesh(ceiling);

            var walls = new GameObject("Walls").transform;
            walls.SetParent(root, false);
            for (int y = 0; y < grid.Height; y++)
            {
                foreach (Vector2Int run in grid.WallRuns(y))
                {
                    float centreCol = run.x + (run.y - 1) * 0.5f;
                    Vector3 centre = grid.CellToWorld(0, y, CellSize);
                    centre.x = (centreCol - (grid.Width - 1) * 0.5f) * CellSize;
                    centre.y = WallHeight * 0.5f;
                    EchoSandboxBuilder.Box($"Wall_r{y}_c{run.x}", walls, centre, new Vector3(run.y * CellSize, WallHeight, CellSize), wallMat);
                }
            }

            var props = new GameObject("Props").transform;
            props.SetParent(root, false);
            foreach (Vector2Int cell in grid.Find(DungeonGrid.Pillar))
            {
                Vector3 pos = grid.CellToWorld(cell.x, cell.y, CellSize);
                EchoSandboxBuilder.Box($"Pillar_{cell.x}_{cell.y}", props, pos + new Vector3(0f, WallHeight * 0.5f, 0f), new Vector3(1.2f, WallHeight, 1.2f), propMat);
            }

            foreach (Vector2Int cell in grid.Find(DungeonGrid.Trap))
            {
                Vector3 pos = grid.CellToWorld(cell.x, cell.y, CellSize);
                GameObject plate = EchoSandboxBuilder.Box($"RagdollTrap_{cell.x}_{cell.y}", props, pos + new Vector3(0f, 0.1f, 0f), new Vector3(3f, 0.2f, 3f), propMat);
                plate.GetComponent<BoxCollider>().isTrigger = true;
                plate.AddComponent<RagdollTrap>();
                IgnoreInNavMesh(plate);
            }
        }

        /// <summary>
        /// FishNet assigns a scene id to every scene NetworkObject in its OnValidate, which Unity only calls for objects
        /// touched in the editor UI. Objects created by this builder never get it, and FishNet then logs
        /// "expected to be initialized" at runtime. Run the same step the "Reserialize NetworkObjects" tool runs.
        /// </summary>
        internal static void InitializeSceneNetworkObjects()
        {
            var onValidate = typeof(FishNet.Object.NetworkObject).GetMethod("OnValidate",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (onValidate == null)
            {
                Debug.LogWarning("[DungeonBuilder] FishNet's NetworkObject.OnValidate was not found; run Tools > Fish-Networking > Utility > Reserialize NetworkObjects on the scene.");
                return;
            }

            foreach (var networkObject in Object.FindObjectsByType<FishNet.Object.NetworkObject>(FindObjectsInactive.Include))
            {
                onValidate.Invoke(networkObject, null);
                EditorUtility.SetDirty(networkObject);
            }
        }

        private static void IgnoreInNavMesh(GameObject go)
        {
            go.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
        }

        /// <summary>Bakes the NavMesh from the dungeon geometry and saves it as an asset next to the scene.</summary>
        private static void BuildNavMesh(GameObject dungeonRoot)
        {
            var surface = dungeonRoot.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.RenderMeshes;
            surface.BuildNavMesh();

            // Runtime safety net in case the baked data is ever missing.
            dungeonRoot.AddComponent<NavMeshBootstrap>();

            if (surface.navMeshData == null)
            {
                Debug.LogWarning("[DungeonBuilder] NavMesh build produced no data. NavMeshBootstrap will build it at runtime.");
                return;
            }

            Directory.CreateDirectory(NavMeshDir);
            string path = NavMeshDir + "/NavMesh-Dungeon.asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(surface.navMeshData, path);
            EditorUtility.SetDirty(surface);
        }

        private static void BuildSpawnPoints(DungeonGrid grid, Transform root, Vector3 startPos, Quaternion startRot, Vector3 monsterPos)
        {
            var spawns = new GameObject("SpawnPoints").transform;
            spawns.SetParent(root, false);

            // Two player slots side by side, facing into the level.
            Vector3 side = startRot * Vector3.right * 1.5f;
            AddSpawn(spawns, "Spawn_Player_1", SpawnKind.Player, startPos - side + Vector3.up * 1.1f, startRot);
            AddSpawn(spawns, "Spawn_Player_2", SpawnKind.Player, startPos + side + Vector3.up * 1.1f, startRot);

            var relicCells = grid.Find(DungeonGrid.Relic);
            for (int i = 0; i < relicCells.Count; i++)
            {
                Vector3 pos = grid.CellToWorld(relicCells[i].x, relicCells[i].y, CellSize);
                AddSpawn(spawns, $"Spawn_Relic_{i + 1}", SpawnKind.Relic, pos + Vector3.up * 0.6f, Quaternion.identity, RelicOrder[i % RelicOrder.Length]);
            }

            AddSpawn(spawns, "Spawn_Enemy_1", SpawnKind.Enemy, monsterPos + Vector3.up, Quaternion.identity);
        }

        private static void AddSpawn(Transform parent, string name, SpawnKind kind, Vector3 position, Quaternion rotation,
            RelicPersonalityType personality = default)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, rotation);
            go.AddComponent<SpawnPoint>().Configure(kind, personality);
        }

        // Grid directions use (dx, dy) in map space where +y is SOUTH; world +Z is north.
        private static Vector3 ToWorldDirection(Vector2Int mapDir) => new Vector3(mapDir.x, 0f, -mapDir.y);

        private static Vector3 OpenDirection(DungeonGrid grid, Vector2Int cell)
        {
            // Prefer facing north (-y in map space), then any other walkable neighbour.
            Vector2Int[] order = { new Vector2Int(0, -1), new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1) };
            foreach (Vector2Int d in order)
            {
                if (grid.IsWalkable(cell.x + d.x, cell.y + d.y)) return ToWorldDirection(d);
            }
            return Vector3.forward;
        }

        private static Vector2Int WallDirection(DungeonGrid grid, Vector2Int cell)
        {
            foreach (Vector2Int d in Directions)
            {
                if (!grid.IsWalkable(cell.x + d.x, cell.y + d.y)) return d;
            }
            return new Vector2Int(0, -1);
        }

        private static void RegisterInBuildSettings()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true),
                new EditorBuildSettingsScene("Assets/Scenes/Sandbox_Test.unity", true)
            };
        }
    }
}
