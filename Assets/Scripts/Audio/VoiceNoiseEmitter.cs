using UnityEngine;

/// <summary>
/// Turns a player's voice (any IVoiceLevelSource) into a noise that enemies can hear.
/// Put it on the same GameObject as the voice source. In multiplayer, the server will
/// create one of these per remote player, fed with the loudness byte sent by that client.
/// With push to talk, plain voice chat is quieter for the monster than an echo (the V key): seeing costs noise.
/// </summary>
public class VoiceNoiseEmitter : MonoBehaviour, INoiseSource
{
    [Tooltip("Component implementing IVoiceLevelSource (e.g. MicrophoneLoudness). Searched on this object if empty.")]
    [SerializeField] private MonoBehaviour _voiceSource;
    [Tooltip("Multiplier applied to the voice level (e.g. 0.5 for a quiet character, 1.5 for a loud one).")]
    [SerializeField, Range(0f, 2f)] private float _gain = 1f;

    [Header("Push to talk")]
    [Tooltip("Noise multiplier for plain voice chat (echo key not held). Talking still carries, just less.")]
    [SerializeField, Range(0f, 2f)] private float _chatNoiseScale = 0.6f;
    [Tooltip("Noise multiplier while echoing (echo key held). Lighting the cave is the loudest thing you can do.")]
    [SerializeField, Range(0f, 2f)] private float _echoNoiseScale = 1.25f;

    private IVoiceLevelSource _source;
    private EchoVisionController _echo;

    /// <inheritdoc/>
    public Transform Owner => transform;

    /// <inheritdoc/>
    public float NoiseLevel01
    {
        get
        {
            if (_source == null) return 0f;
            bool echoing = _echo == null || _echo.IsEchoing;
            return Weigh(_source.Level01, _gain, echoing, _chatNoiseScale, _echoNoiseScale);
        }
    }

    /// <summary>
    /// Noise (0..1) a voice at <paramref name="level"/> makes: echoing uses <paramref name="echoScale"/>, plain chat
    /// <paramref name="chatScale"/>, both on top of the character's <paramref name="gain"/>.
    /// </summary>
    public static float Weigh(float level, float gain, bool echoing, float chatScale, float echoScale)
    {
        float scale = echoing ? echoScale : chatScale;
        return Mathf.Clamp01(Mathf.Max(0f, level) * gain * scale);
    }

    private void Awake()
    {
        _echo = GetComponent<EchoVisionController>();
        _source = _echo;
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
