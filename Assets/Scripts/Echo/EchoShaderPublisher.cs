using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Collects every active <see cref="EchoVisionController"/> (one per player) and publishes them
/// to the EchoReveal shader as global arrays every frame:
///   _EchoSources  (Vector4[4])  xyz = sphere centre, w = radius
///   _EchoFalloffs (Vector4)     soft edge width per source
/// Also provides debug test controls:
///   [H] Toggle monster freeze (EnemyBrain)
///   [J] Toggle full scene illumination
/// The publisher GameObject is created automatically at startup, so no scene setup is needed.
/// </summary>
public class EchoShaderPublisher : MonoBehaviour
{
    /// <summary>Must match ECHO_MAX_SOURCES in EchoReveal.shader.</summary>
    public const int MaxSources = 4;

    private static readonly int EchoSourcesId = Shader.PropertyToID("_EchoSources");
    private static readonly int EchoFalloffsId = Shader.PropertyToID("_EchoFalloffs");

    private static readonly List<EchoVisionController> Sources = new List<EchoVisionController>();
    private static readonly Vector4[] SourceBuffer = new Vector4[MaxSources];
    private static EchoShaderPublisher _instance;

    private static bool _fullIllumination = false;

    /// <summary>True if full debug illumination is active (J key).</summary>
    public static bool IsFullIllumination => _fullIllumination;

    /// <summary>Toggles full illumination for testing.</summary>
    public static void ToggleFullIllumination()
    {
        _fullIllumination = !_fullIllumination;
        Debug.Log($"<color=cyan>[EchoReveal]</color> Full Illumination: {(_fullIllumination ? "ON (Everything Illuminated)" : "OFF (Normal Echolocation)")}");
    }

    /// <summary>Adds a controller to the set of echo sources. Called from OnEnable.</summary>
    public static void Register(EchoVisionController source)
    {
        if (source != null && !Sources.Contains(source)) Sources.Add(source);
    }

    /// <summary>Removes a controller from the set of echo sources. Called from OnDisable.</summary>
    public static void Unregister(EchoVisionController source)
    {
        Sources.Remove(source);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        // Static state survives domain-reload-less play sessions; reset it.
        Sources.Clear();
        _instance = null;
        _fullIllumination = false;
        EnemyBrain.SetFrozen(false);

        var go = new GameObject("[EchoShaderPublisher]") { hideFlags = HideFlags.HideAndDontSave };
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<EchoShaderPublisher>();
    }

    private void Update()
    {
        if (Keyboard.current == null) return;

        // [J] Toggle full scene illumination
        if (Keyboard.current.jKey.wasPressedThisFrame)
        {
            ToggleFullIllumination();
        }

        // [H] Toggle monster freeze
        if (Keyboard.current.hKey.wasPressedThisFrame)
        {
            EnemyBrain.ToggleFreeze();
        }
    }

    private void OnDestroy()
    {
        // Back to pitch black when the publisher goes away (e.g. leaving play mode).
        System.Array.Clear(SourceBuffer, 0, MaxSources);
        Shader.SetGlobalVectorArray(EchoSourcesId, SourceBuffer);
        Shader.SetGlobalVector(EchoFalloffsId, Vector4.zero);
        if (_instance == this) _instance = null;
    }

    // LateUpdate so every controller has already smoothed its radius / moved this frame.
    private void LateUpdate()
    {
        var falloffs = Vector4.zero;

        if (_fullIllumination)
        {
            // Fully illuminate the entire world with an immense sphere
            SourceBuffer[0] = new Vector4(0f, 0f, 0f, 99999f);
            falloffs[0] = 5f;
            for (int i = 1; i < MaxSources; i++)
            {
                SourceBuffer[i] = Vector4.zero;
            }

            Shader.SetGlobalVectorArray(EchoSourcesId, SourceBuffer);
            Shader.SetGlobalVector(EchoFalloffsId, falloffs);
            return;
        }

        int count = Mathf.Min(Sources.Count, MaxSources);

        for (int i = 0; i < MaxSources; i++)
        {
            if (i < count)
            {
                EchoVisionController s = Sources[i];
                Vector3 o = s.Origin;
                SourceBuffer[i] = new Vector4(o.x, o.y, o.z, s.CurrentRadius);
                falloffs[i] = s.CurrentFalloff;
            }
            else
            {
                SourceBuffer[i] = Vector4.zero; // inactive slot
            }
        }

        Shader.SetGlobalVectorArray(EchoSourcesId, SourceBuffer);
        Shader.SetGlobalVector(EchoFalloffsId, falloffs);
    }

    private void OnGUI()
    {
        if (!_fullIllumination && !EnemyBrain.IsFrozen) return;

        GUILayout.BeginArea(new Rect(Screen.width - 230, 10, 220, 65), GUI.skin.box);
        if (_fullIllumination)
        {
            GUI.color = Color.yellow;
            GUILayout.Label("💡 [J] Full Light: ON");
        }
        if (EnemyBrain.IsFrozen)
        {
            GUI.color = Color.cyan;
            GUILayout.Label("❄️ [H] Monster: FROZEN");
        }
        GUI.color = Color.white;
        GUILayout.EndArea();
    }
}
