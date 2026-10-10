using UnityEngine;

/// <summary>
/// Turns a player's voice (any IVoiceLevelSource) into a noise that enemies can hear.
/// Put it on the same GameObject as the voice source. In multiplayer, the server will
/// create one of these per remote player, fed with the loudness byte sent by that client.
/// </summary>
public class VoiceNoiseEmitter : MonoBehaviour, INoiseSource
{
    [Tooltip("Component implementing IVoiceLevelSource (e.g. MicrophoneLoudness). Searched on this object if empty.")]
    [SerializeField] private MonoBehaviour _voiceSource;
    [Tooltip("Multiplier applied to the voice level (e.g. 0.5 for a quiet character, 1.5 for a loud one).")]
    [SerializeField, Range(0f, 2f)] private float _gain = 1f;

    private IVoiceLevelSource _source;

    /// <inheritdoc/>
    public Transform Owner => transform;

    /// <inheritdoc/>
    public float NoiseLevel01 => _source == null ? 0f : Mathf.Clamp01(_source.Level01 * _gain);

    private void Awake()
    {
        _source = GetComponent<EchoVisionController>();
        if (_source == null) _source = (_voiceSource as IVoiceLevelSource) ?? GetComponent<IVoiceLevelSource>();
        if (_source == null)
            Debug.LogWarning("[VoiceNoiseEmitter] No IVoiceLevelSource found; this emitter will be silent.", this);
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
