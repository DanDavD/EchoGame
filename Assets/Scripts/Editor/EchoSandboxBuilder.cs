using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using EchoProtocol.Altar;
using EchoProtocol.Extraction;
using EchoProtocol.Relics;
using EchoProtocol.Interaction;

/// <summary>
/// One-click builder for the Echo Protocol sandbox prototype.
/// Menu: Echo Protocol > Build Sandbox Scene.
/// Creates checker texture, EchoReveal materials, a pitch-black test arena, the player
/// (CharacterController + camera + scripts, all references wired) and saves Sandbox_Test.unity.
/// It also runs automatically ONCE after the first compile if the scene does not exist yet.
/// </summary>
[InitializeOnLoad]
public static class EchoSandboxBuilder
{
    private const string ScenePath = "Assets/Scenes/Sandbox_Test.unity";
    private const string ShaderPath = "Assets/Shaders/EchoReveal.shader";
    private const string TexturePath = "Assets/Art/Textures/Checker.png";
    private const string ActionsPath = "Assets/InputSystem_Actions.inputactions";

    static EchoSandboxBuilder()
    {
        EditorApplication.delayCall += AutoBuildOnce;
        EditorApplication.delayCall += CheckAndExpandArena;
    }

    private static void CheckAndExpandArena()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += CheckAndExpandArena;
            return;
        }

        if (!File.Exists(ScenePath)) return;

        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        var arenaObj = GameObject.Find("Arena");
        if (arenaObj == null) return;

        Transform floor = arenaObj.transform.Find("Floor");
        if (floor != null && floor.localScale.x >= 110f && arenaObj.transform.Find("Pillar_1") == null)
        {
            if (GameObject.Find("Altar") == null)
            {
                SetupAltarAndEscapePortalMenuItem();
            }
            return; // Already expanded and cleared
        }

        ExpandArenaAndClearObstacles();
    }

    private static void AutoBuildOnce()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += AutoBuildOnce; // assets not ready yet, try again later
            return;
        }
        if (File.Exists(ScenePath)) return;
        Build();
    }

    [MenuItem("Echo Protocol/Build Sandbox Scene")]
    public static void Build()
    {
        AssetDatabase.Refresh();

        var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
        var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsPath);
        if (shader == null || actions == null)
        {
            Debug.LogError($"[EchoSandboxBuilder] Missing shader ({ShaderPath}) or input actions ({ActionsPath}).");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        // --- Assets -------------------------------------------------------------------------
        Texture2D checker = CreateCheckerTexture();
        Material floorMat = GetOrCreateMaterial("Assets/Art/Materials/Echo_Floor.mat", shader, checker, new Color(0.55f, 0.55f, 0.6f), 0.5f);
        Material wallMat = GetOrCreateMaterial("Assets/Art/Materials/Echo_Wall.mat", shader, checker, new Color(0.65f, 0.45f, 0.45f), 0.5f);
        Material propMat = GetOrCreateMaterial("Assets/Art/Materials/Echo_Prop.mat", shader, checker, new Color(0.45f, 0.65f, 0.55f), 1f);
        Material enemyMat = GetOrCreateMaterial("Assets/Art/Materials/Echo_Enemy.mat", shader, checker, new Color(0.8f, 0.12f, 0.12f), 1f);

        // --- Scene --------------------------------------------------------------------------
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Pitch black lighting: no skybox, flat black ambient, no reflections.
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = Color.black;
        RenderSettings.reflectionIntensity = 0f;

        Transform arena = BuildArena(floorMat, wallMat, propMat);
        BuildNavMesh(arena.gameObject);
        BuildPlayer(actions);
        BuildEnemy(enemyMat);
        SetupAltarAndEscapePortal(arena, propMat);

        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets();
        Debug.Log("[EchoSandboxBuilder] Sandbox_Test scene created. Press Play and make some noise!");
    }

    // ------------------------------------------------------------------------------------------
    [MenuItem("Echo Protocol/Expand Arena & Clear Obstacles")]
    public static void ExpandArenaAndClearObstacles()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        var arenaObj = GameObject.Find("Arena");
        if (arenaObj == null)
        {
            Debug.LogError("[EchoSandboxBuilder] Arena GameObject not found.");
            return;
        }

        Transform arena = arenaObj.transform;

        // 1. Expand Floor to 120x120
        Transform floor = arena.Find("Floor");
        if (floor != null)
        {
            floor.localPosition = new Vector3(0f, -0.5f, 0f);
            floor.localScale = new Vector3(120f, 1f, 120f);
        }

        // 2. Expand Perimeter Walls to 120m bounds
        Transform wallN = arena.Find("Wall_N");
        if (wallN != null)
        {
            wallN.localPosition = new Vector3(0f, 3f, 60.5f);
            wallN.localScale = new Vector3(122f, 6f, 1f);
        }

        Transform wallS = arena.Find("Wall_S");
        if (wallS != null)
        {
            wallS.localPosition = new Vector3(0f, 3f, -60.5f);
            wallS.localScale = new Vector3(122f, 6f, 1f);
        }

        Transform wallE = arena.Find("Wall_E");
        if (wallE != null)
        {
            wallE.localPosition = new Vector3(60.5f, 3f, 0f);
            wallE.localScale = new Vector3(1f, 6f, 122f);
        }

        Transform wallW = arena.Find("Wall_W");
        if (wallW != null)
        {
            wallW.localPosition = new Vector3(-60.5f, 3f, 0f);
            wallW.localScale = new Vector3(1f, 6f, 122f);
        }

        // 3. Destroy all obstacles
        string[] obstacles = new[] {
            "Pillar_1", "Pillar_2", "Block_3", "Wall_4", "Wall_5", "Pillar_6", "Pillar_7",
            "JumpBar", "Step_0", "Step_1", "Step_2", "Step_3", "Step_4", "Step_5", "RagdollTrap"
        };

        foreach (var obsName in obstacles)
        {
            Transform obs = arena.Find(obsName);
            if (obs != null)
            {
                Object.DestroyImmediate(obs.gameObject);
            }
        }

        // 4. Rebuild NavMesh
        var surface = arenaObj.GetComponent<NavMeshSurface>();
        if (surface != null)
        {
            surface.BuildNavMesh();
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("<color=green>[EchoSandboxBuilder]</color> Arena expanded to 120x120m and obstacles cleared successfully!");
    }

    [MenuItem("Echo Protocol/Setup Altar and Escape Portal")]
    public static void SetupAltarAndEscapePortalMenuItem()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
        Texture2D checker = CreateCheckerTexture();
        Material propMat = GetOrCreateMaterial("Assets/Art/Materials/Echo_Prop.mat", shader, checker, new Color(0.45f, 0.65f, 0.55f), 1f);
        var arenaObj = GameObject.Find("Arena");
        Transform arena = arenaObj != null ? arenaObj.transform : null;

        SetupAltarAndEscapePortal(arena, propMat);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("<color=green>[EchoSandboxBuilder]</color> Altar, Sockets, Escape Portal, Game Manager and Relics successfully configured!");
    }

    public static void SetupAltarAndEscapePortal(Transform arena, Material propMat)
    {
        // 1. Central Altar
        var existingAltar = GameObject.Find("Altar");
        if (existingAltar != null) Object.DestroyImmediate(existingAltar);

        var altarObj = new GameObject("Altar");
        if (arena != null) altarObj.transform.SetParent(arena, false);
        altarObj.transform.position = Vector3.zero;

        // Base platform
        var basePlatform = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        basePlatform.name = "Altar_Base";
        basePlatform.transform.SetParent(altarObj.transform, false);
        basePlatform.transform.position = new Vector3(0f, 0.2f, 0f);
        basePlatform.transform.localScale = new Vector3(6f, 0.4f, 6f);
        if (propMat != null) basePlatform.GetComponent<MeshRenderer>().sharedMaterial = propMat;

        // Central Monolith
        var monolith = GameObject.CreatePrimitive(PrimitiveType.Cube);
        monolith.name = "Altar_Monolith";
        monolith.transform.SetParent(altarObj.transform, false);
        monolith.transform.position = new Vector3(0f, 1.4f, 0f);
        monolith.transform.localScale = new Vector3(1.6f, 2.4f, 1.6f);
        if (propMat != null) monolith.GetComponent<MeshRenderer>().sharedMaterial = propMat;

        // Central Altar Light
        var altarLightGo = new GameObject("Altar_CentralLight");
        altarLightGo.transform.SetParent(altarObj.transform, false);
        altarLightGo.transform.position = new Vector3(0f, 2.8f, 0f);
        var altarLight = altarLightGo.AddComponent<Light>();
        altarLight.type = LightType.Point;
        altarLight.color = new Color(0.2f, 0.4f, 0.8f);
        altarLight.range = 22f;
        altarLight.intensity = 3.5f;

        var altarReceptacle = altarObj.AddComponent<AltarReceptacle>();

        // Wire serialized fields on AltarReceptacle
        var receptacleSo = new SerializedObject(altarReceptacle);
        receptacleSo.FindProperty("_centralAltarLight").objectReferenceValue = altarLight;
        receptacleSo.FindProperty("_requiredSockets").intValue = 3;

        // 3 Pedestal Sockets around the altar
        Vector3[] socketOffsets = new[]
        {
            new Vector3(0f, 0f, 2.2f),
            new Vector3(1.9f, 0f, -1.1f),
            new Vector3(-1.9f, 0f, -1.1f)
        };

        var socketsProp = receptacleSo.FindProperty("_sockets");
        socketsProp.ClearArray();

        for (int i = 0; i < socketOffsets.Length; i++)
        {
            Vector3 offset = socketOffsets[i];
            var socketObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            socketObj.name = $"Socket_{i + 1}";
            socketObj.transform.SetParent(altarObj.transform, false);
            socketObj.transform.position = new Vector3(offset.x, 0.5f, offset.z);
            socketObj.transform.localScale = new Vector3(0.9f, 1.0f, 0.9f);
            if (propMat != null) socketObj.GetComponent<MeshRenderer>().sharedMaterial = propMat;

            // Socket Light
            var sLightGo = new GameObject($"SocketLight_{i + 1}");
            sLightGo.transform.SetParent(socketObj.transform, false);
            sLightGo.transform.localPosition = new Vector3(0f, 0.7f, 0f);
            var sLight = sLightGo.AddComponent<Light>();
            sLight.type = LightType.Point;
            sLight.color = new Color(0.2f, 0.7f, 1f);
            sLight.range = 5f;
            sLight.intensity = 2f;

            // Mount Point
            var mountGo = new GameObject("MountPoint");
            mountGo.transform.SetParent(socketObj.transform, false);
            mountGo.transform.localPosition = new Vector3(0f, 0.65f, 0f);

            var socketComp = socketObj.AddComponent<AltarSocket>();
            var socketSo = new SerializedObject(socketComp);
            socketSo.FindProperty("_socketMountPoint").objectReferenceValue = mountGo.transform;
            socketSo.FindProperty("_socketLight").objectReferenceValue = sLight;
            socketSo.FindProperty("_parentReceptacle").objectReferenceValue = altarReceptacle;
            socketSo.ApplyModifiedPropertiesWithoutUndo();

            socketsProp.InsertArrayElementAtIndex(i);
            socketsProp.GetArrayElementAtIndex(i).objectReferenceValue = socketComp;
        }
        receptacleSo.ApplyModifiedPropertiesWithoutUndo();

        // 2. Escape Portal on North Wall
        var existingPortal = GameObject.Find("EscapePortal");
        if (existingPortal != null) Object.DestroyImmediate(existingPortal);

        var portalRoot = new GameObject("EscapePortal");
        if (arena != null) portalRoot.transform.SetParent(arena, false);
        portalRoot.transform.position = new Vector3(0f, 0f, 59.8f);

        // Frame Pillars & Header
        Box("Portal_Pillar_L", portalRoot.transform, new Vector3(-2.8f, 3f, 59.8f), new Vector3(0.8f, 6f, 0.8f), propMat);
        Box("Portal_Pillar_R", portalRoot.transform, new Vector3(2.8f, 3f, 59.8f), new Vector3(0.8f, 6f, 0.8f), propMat);
        Box("Portal_Lintel", portalRoot.transform, new Vector3(0f, 6.4f, 59.8f), new Vector3(6.4f, 0.8f, 0.8f), propMat);

        // Portal Gateway Trigger
        var gateway = GameObject.CreatePrimitive(PrimitiveType.Cube);
        gateway.name = "Portal_Gateway";
        gateway.transform.SetParent(portalRoot.transform, false);
        gateway.transform.position = new Vector3(0f, 3f, 59.8f);
        gateway.transform.localScale = new Vector3(4.8f, 5.8f, 0.4f);
        if (propMat != null) gateway.GetComponent<MeshRenderer>().sharedMaterial = propMat;

        var gateCol = gateway.GetComponent<BoxCollider>();
        if (gateCol != null) gateCol.isTrigger = true;

        var pLightGo = new GameObject("PortalLight");
        pLightGo.transform.SetParent(gateway.transform, false);
        pLightGo.transform.localPosition = Vector3.zero;
        var pLight = pLightGo.AddComponent<Light>();
        pLight.type = LightType.Point;
        pLight.color = new Color(0.2f, 0.95f, 1f);
        pLight.range = 18f;
        pLight.intensity = 1f;

        var portalComp = gateway.AddComponent<EscapePortal>();
        var portalSo = new SerializedObject(portalComp);
        portalSo.FindProperty("_portalTrigger").objectReferenceValue = gateCol;
        portalSo.FindProperty("_portalLight").objectReferenceValue = pLight;
        portalSo.FindProperty("_portalVfxRenderer").objectReferenceValue = gateway.GetComponent<MeshRenderer>();
        portalSo.ApplyModifiedPropertiesWithoutUndo();

        // 3. GameExtractionManager
        var existingGm = GameObject.Find("GameManager");
        if (existingGm != null) Object.DestroyImmediate(existingGm);

        var gmObj = new GameObject("GameManager");
        var extractionMgr = gmObj.AddComponent<GameExtractionManager>();
        var gmSo = new SerializedObject(extractionMgr);
        gmSo.FindProperty("_altar").objectReferenceValue = altarReceptacle;
        gmSo.ApplyModifiedPropertiesWithoutUndo();

        // 4. Clean static relics (spawned at runtime by GameExtractionManager)
        SpawnSandboxRelics(arena);
    }

    private static void SpawnSandboxRelics(Transform arena)
    {
        // Destroy any static scene relics so FishNet does not complain about un-reserialized scene objects.
        // GameExtractionManager automatically spawns runtime instances with correct personalities upon Start().
        var existingRelics = Object.FindObjectsByType<RelicBrain>(FindObjectsInactive.Exclude);
        foreach (var r in existingRelics)
        {
            if (r != null) Object.DestroyImmediate(r.gameObject);
        }
    }

    // ------------------------------------------------------------------------------------------
    private static Transform BuildArena(Material floorMat, Material wallMat, Material propMat)
    {
        var root = new GameObject("Arena").transform;

        Box("Floor", root, new Vector3(0f, -0.5f, 0f), new Vector3(120f, 1f, 120f), floorMat);

        // Perimeter walls.
        Box("Wall_N", root, new Vector3(0f, 3f, 60.5f), new Vector3(122f, 6f, 1f), wallMat);
        Box("Wall_S", root, new Vector3(0f, 3f, -60.5f), new Vector3(122f, 6f, 1f), wallMat);
        Box("Wall_E", root, new Vector3(60.5f, 3f, 0f), new Vector3(1f, 6f, 122f), wallMat);
        Box("Wall_W", root, new Vector3(-60.5f, 3f, 0f), new Vector3(1f, 6f, 122f), wallMat);

        return root;
    }

    private static void BuildPlayer(InputActionAsset actions)
    {
        // Capsule body. The CharacterController replaces the primitive's own collider.
        var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        player.name = "Player";
        player.transform.position = new Vector3(0f, 1.1f, -2f);
        Object.DestroyImmediate(player.GetComponent<CapsuleCollider>());
        player.GetComponent<MeshRenderer>().enabled = false; // first person: hide own body for now

        var controller = player.AddComponent<CharacterController>();
        controller.height = 2f;
        controller.radius = 0.5f;
        controller.center = Vector3.zero;

        var pivot = new GameObject("CameraPivot").transform;
        pivot.SetParent(player.transform, false);
        pivot.localPosition = new Vector3(0f, 0.7f, 0f);

        // Camera: solid black background (this is what "Background Type = Solid Color" means in URP).
        var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
        camGo.transform.SetParent(pivot, false);
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        cam.fieldOfView = 70f;
        cam.nearClipPlane = 0.05f;
        camGo.AddComponent<AudioListener>();

        // Gameplay scripts.
        var move = player.AddComponent<PlayerController>();
        var mic = player.AddComponent<MicrophoneLoudness>();
        var echo = player.AddComponent<EchoVisionController>();

        // Wire serialized private fields.
        var moveSo = new SerializedObject(move);
        moveSo.FindProperty("_inputActions").objectReferenceValue = actions;
        moveSo.FindProperty("_cameraPivot").objectReferenceValue = pivot;
        moveSo.ApplyModifiedPropertiesWithoutUndo();

        var echoSo = new SerializedObject(echo);
        echoSo.FindProperty("_voiceSource").objectReferenceValue = mic;
        echoSo.ApplyModifiedPropertiesWithoutUndo();

        // Makes the player's voice audible to enemies.
        var emitter = player.AddComponent<VoiceNoiseEmitter>();
        var emitterSo = new SerializedObject(emitter);
        emitterSo.FindProperty("_voiceSource").objectReferenceValue = mic;
        emitterSo.ApplyModifiedPropertiesWithoutUndo();

        // Ragdoll physics and logic.
        player.AddComponent<Rigidbody>(); // Rigidbody settings are applied in PlayerRagdoll.Awake
        var noiseBurst = player.AddComponent<NoiseBurstEmitter>();
        var ragdoll = player.AddComponent<PlayerRagdoll>();
        
        var ragdollSo = new SerializedObject(ragdoll);
        ragdollSo.FindProperty("_cameraPivot").objectReferenceValue = pivot;
        ragdollSo.FindProperty("_impactNoise").objectReferenceValue = noiseBurst;
        ragdollSo.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>Bakes the NavMesh from the arena geometry and saves it as an asset next to the scene.</summary>
    private static void BuildNavMesh(GameObject arenaRoot)
    {
        var surface = arenaRoot.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.Children; // only arena geometry, never player/enemy
        surface.useGeometry = NavMeshCollectGeometry.RenderMeshes;
        surface.BuildNavMesh();

        // Runtime safety net in case the baked data is ever missing.
        arenaRoot.AddComponent<NavMeshBootstrap>();

        if (surface.navMeshData == null)
        {
            Debug.LogWarning("[EchoSandboxBuilder] NavMesh build produced no data. NavMeshBootstrap will build it at runtime.");
            return;
        }

        const string dir = "Assets/Scenes/Sandbox_Test";
        Directory.CreateDirectory(dir);
        string path = dir + "/NavMesh-Arena.asset";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(surface.navMeshData, path); // persist so the scene references a real asset
        EditorUtility.SetDirty(surface);
    }

    /// <summary>Places the blind enemy far from the player spawn, ready to hear you.</summary>
    private static void BuildEnemy(Material enemyMat)
    {
        var enemy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        enemy.name = "Enemy_Blind";
        enemy.transform.position = new Vector3(-6f, 1f, 16f);
        enemy.GetComponent<MeshRenderer>().sharedMaterial = enemyMat; // only visible inside echo light

        // RequireComponent adds NavMeshAgent and HearingSensor automatically.
        enemy.AddComponent<EnemyBrain>();
        enemy.AddComponent<EnemyCatchReaction>();

        var agent = enemy.GetComponent<NavMeshAgent>();
        agent.baseOffset = 1f;          // capsule pivot is its centre (height 2 / 2)
        agent.radius = 0.5f;
        agent.height = 2f;
        agent.stoppingDistance = 0.5f;
        agent.acceleration = 10f;
        agent.angularSpeed = 240f;
    }

    // ------------------------------------------------------------------------------------------
    private static void Box(string name, Transform parent, Vector3 pos, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        go.transform.localScale = scale;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
    }

    private static Material GetOrCreateMaterial(string path, Shader shader, Texture2D tex, Color tint, float texScale)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        bool isNew = mat == null;
        if (isNew) mat = new Material(shader);

        mat.shader = shader;
        mat.SetTexture("_BaseMap", tex);
        mat.SetColor("_Tint", tint);
        mat.SetColor("_EdgeColor", new Color(0.2f, 0.9f, 1f) * 1.5f);
        mat.SetFloat("_TexScale", texScale);

        if (isNew) AssetDatabase.CreateAsset(mat, path);
        else EditorUtility.SetDirty(mat);
        return mat;
    }

    private static Texture2D CreateCheckerTexture()
    {
        if (!File.Exists(TexturePath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(TexturePath));
            var tex = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            for (int y = 0; y < 16; y++)
            {
                for (int x = 0; x < 16; x++)
                {
                    bool light = ((x / 4) + (y / 4)) % 2 == 0;
                    float v = light ? 0.9f : 0.5f;
                    tex.SetPixel(x, y, new Color(v, v, v, 1f));
                }
            }
            File.WriteAllBytes(TexturePath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(TexturePath);

            // Point filtering = crunchy PS1 look.
            var importer = (TextureImporter)AssetImporter.GetAtPath(TexturePath);
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
    }
}
