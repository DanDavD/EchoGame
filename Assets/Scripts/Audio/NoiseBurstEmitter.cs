using UnityEngine;

/// <summary>
/// A short, decaying burst of noise (an impact, a crash, a scream of surprise).
/// Unlike the continuous voice, it is triggered by code and fades out on its own.
/// Used so a ragdoll crash is audible to enemies. Registers itself as an INoiseSource.
/// </summary>
public class NoiseBurstEmitter : MonoBehaviour, INoiseSource
{
    private float _level;
    private float _decayPerSecond;

    /// <inheritdoc/>
    public Transform Owner => transform;

    /// <inheritdoc/>
    public float NoiseLevel01 => _level;

    /// <summary>Emits a noise of the given loudness that fades to silence over <paramref name="seconds"/>.</summary>
    public void Trigger(float level01, float seconds)
    {
        _level = Mathf.Clamp01(level01);
        _decayPerSecond = seconds > 0.01f ? _level / seconds : float.MaxValue;
    }

    private void Update()
    {
        if (_level <= 0f) return;
        _level = Mathf.Max(0f, _level - _decayPerSecond * Time.deltaTime);
    }

    private void OnEnable()
    {
        NoiseRegistry.Register(this);
    }

    private void OnDisable()
    {
        NoiseRegistry.Unregister(this);
    }
}
