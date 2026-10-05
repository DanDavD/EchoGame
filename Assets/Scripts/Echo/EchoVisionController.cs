using UnityEngine;

/// <summary>
/// Turns the player's voice loudness into a growing sphere of light, by publishing
/// global shader values read by the EchoReveal Shader Graph:
///   _EchoOrigin  (Vector4)  world position of the sphere centre
///   _EchoRadius  (float)    sphere radius in metres
///   _EchoFalloff (float)    width of the soft edge in metres
/// Single-player prototype: one global origin. For multiplayer we will switch to
/// Shader.SetGlobalVectorArray with one entry per player.
/// </summary>
public class EchoVisionController : MonoBehaviour
{
    private static readonly int EchoOriginId = Shader.PropertyToID("_EchoOrigin");
    private static readonly int EchoRadiusId = Shader.PropertyToID("_EchoRadius");
    private static readonly int EchoFalloffId = Shader.PropertyToID("_EchoFalloff");

    [Header("Source")]
    [Tooltip("Any component implementing IVoiceLevelSource (e.g. MicrophoneLoudness). " +
             "If empty, it is searched on this GameObject.")]
    [SerializeField] private MonoBehaviour _voiceSource;
    [Tooltip("Offset from this transform (e.g. head height) for the centre of the light.")]
    [SerializeField] private Vector3 _originOffset = new Vector3(0f, 1f, 0f);

    [Header("Radius mapping")]
    [Tooltip("Radius (m) when Level01 = 1, i.e. a full scream lights the whole cavern.")]
    [SerializeField] private float _maxRadius = 60f;
    [Tooltip("X = Level01 (0..1), Y = fraction of Max Radius. Default: whisper ~2.5 m, talk ~12 m, scream = everything.")]
    [SerializeField] private AnimationCurve _radiusCurve = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.25f, 0.04f),
        new Keyframe(0.65f, 0.2f),
        new Keyframe(1f, 1f));
    [Tooltip("Below this Level01 nothing is revealed (ignores breathing / background noise).")]
    [SerializeField, Range(0f, 0.5f)] private float _noiseGate = 0.08f;

    [Header("Look & feel")]
    [Tooltip("Seconds for the light to grow (small = snappy).")]
    [SerializeField] private float _growSmoothTime = 0.05f;
    [Tooltip("Seconds for the light to fade out (big = lingers, scarier).")]
    [SerializeField] private float _shrinkSmoothTime = 0.6f;
    [Tooltip("Soft edge width as a fraction of the current radius.")]
    [SerializeField, Range(0.05f, 1f)] private float _falloffRatio = 0.5f;
    [SerializeField] private float _minFalloff = 0.4f;

    /// <summary>Current radius of the revealed sphere (metres). Useful for gameplay/debug.</summary>
    public float CurrentRadius => _currentRadius;

    private IVoiceLevelSource _source;
    private float _currentRadius;
    private float _radiusVelocity;

    private void Awake()
    {
        _source = _voiceSource as IVoiceLevelSource;
        if (_source == null) _source = GetComponent<IVoiceLevelSource>();

        if (_source == null)
        {
            Debug.LogError("[EchoVisionController] No IVoiceLevelSource found. Add MicrophoneLoudness.", this);
            enabled = false;
        }
    }

    private void LateUpdate()
    {
        // LateUpdate so the origin uses the player's final position after movement this frame.
        float level = _source.Level01;
        float targetRadius = level < _noiseGate ? 0f : _maxRadius * _radiusCurve.Evaluate(level);

        float smoothTime = targetRadius > _currentRadius ? _growSmoothTime : _shrinkSmoothTime;
        _currentRadius = Mathf.SmoothDamp(_currentRadius, targetRadius, ref _radiusVelocity, smoothTime);
        if (_currentRadius < 0.01f) _currentRadius = 0f;

        Publish(_currentRadius);
    }

    private void OnDisable()
    {
        // Back to pitch black when the player goes away.
        _currentRadius = 0f;
        _radiusVelocity = 0f;
        Publish(0f);
    }

    private void Publish(float radius)
    {
        Vector3 origin = transform.position + _originOffset;
        Shader.SetGlobalVector(EchoOriginId, origin);
        Shader.SetGlobalFloat(EchoRadiusId, radius);
        Shader.SetGlobalFloat(EchoFalloffId, Mathf.Max(_minFalloff, radius * _falloffRatio));
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.9f, 0.4f, 0.6f);
        Gizmos.DrawWireSphere(transform.position + _originOffset, _currentRadius);
    }
}
