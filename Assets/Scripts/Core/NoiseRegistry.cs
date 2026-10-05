using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Global list of active noise sources. Emitters register themselves while enabled,
/// and HearingSensors read the list. Avoids slow scene searches every frame.
/// </summary>
public static class NoiseRegistry
{
    private static readonly List<INoiseSource> _sources = new List<INoiseSource>();

    /// <summary>All noise sources currently active in the scene (read-only view).</summary>
    public static IReadOnlyList<INoiseSource> Sources => _sources;

    public static void Register(INoiseSource source)
    {
        if (source != null && !_sources.Contains(source)) _sources.Add(source);
    }

    public static void Unregister(INoiseSource source)
    {
        _sources.Remove(source);
    }

    // With "Enter Play Mode Options" (domain reload disabled) statics survive between runs. Reset them.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        _sources.Clear();
    }
}
