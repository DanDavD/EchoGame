using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

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

        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets();
        Debug.Log("[EchoSandboxBuilder] Sandbox_Test scene created. Press Play and make some noise!");
    }

    // ------------------------------------------------------------------------------------------
    private static Transform BuildArena(Material floorMat, Material wallMat, Material propMat)
    {
        var root = new GameObject("Arena").transform;

        Box("Floor", root, new Vector3(0f, -0.5f, 0f), new Vector3(60f, 1f, 60f), floorMat);

        // Perimeter walls.
        Box("Wall_N", root, new Vector3(0f, 3f, 30.5f), new Vector3(62f, 6f, 1f), wallMat);
        Box("Wall_S", root, new Vector3(0f, 3f, -30.5f), new Vector3(62f, 6f, 1f), wallMat);
        Box("Wall_E", root, new Vector3(30.5f, 3f, 0f), new Vector3(1f, 6f, 62f), wallMat);
        Box("Wall_W", root, new Vector3(-30.5f, 3f, 0f), new Vector3(1f, 6f, 62f), wallMat);

        // Pillars / blocks to reveal.
        Box("Pillar_1", root, new Vector3(8f, 2f, 6f), new Vector3(2f, 4f, 2f), propMat);
        Box("Pillar_2", root, new Vector3(-10f, 2f, 12f), new Vector3(3f, 4f, 3f), propMat);
        Box("Block_3", root, new Vector3(14f, 1f, -9f), new Vector3(4f, 2f, 4f), propMat);
        Box("Wall_4", root, new Vector3(-14f, 1.5f, -10f), new Vector3(2f, 3f, 6f), propMat);
        Box("Wall_5", root, new Vector3(0f, 1f, 18f), new Vector3(6f, 2f, 1.5f), propMat);
        Box("Pillar_6", root, new Vector3(-20f, 2.5f, 0f), new Vector3(2f, 5f, 2f), propMat);
        Box("Pillar_7", root, new Vector3(20f, 2f, 14f), new Vector3(2f, 4f, 2f), propMat);

        // Low obstacle to jump over.
        Box("JumpBar", root, new Vector3(0f, 0.4f, 6f), new Vector3(4f, 0.8f, 0.8f), propMat);

        // Staircase of growing blocks (clunky climbing practice).
        for (int i = 0; i < 6; i++)
        {
            float h = 0.4f * (i + 1);
            Box($"Step_{i}", root, new Vector3(6f, h * 0.5f, -6f - 2f * i), new Vector3(3f, h, 2f), propMat);
        }

        // Comedy ragdoll trap.
        var trap = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        trap.name = "RagdollTrap";
        trap.transform.SetParent(root, false);
        trap.transform.position = new Vector3(-8f, 0.05f, 5f);
        trap.transform.localScale = new Vector3(3f, 0.1f, 3f);
        trap.GetComponent<MeshRenderer>().sharedMaterial = propMat;
        trap.GetComponent<CapsuleCollider>().isTrigger = true;
        trap.AddComponent<RagdollTrap>();

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

        // Visual effects triggers
        player.AddComponent<PS1GlitchController>();
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
