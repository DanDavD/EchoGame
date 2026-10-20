using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Transporting;
using UnityEngine;

/// <summary>
/// Turns a player's voice loudness into a growing sphere of light (networked).
///   * Push to talk: the voice only lights the cave while the player holds the echo key (V). Talking without it is
///     plain voice chat: it still makes noise for the monster and the team meter, but reveals nothing.
///   * The OWNER reads its own microphone, computes the radius and writes it to a SyncVar.
///   * Every OTHER client receives that radius and smooths towards it, so all players see
///     everyone else's echo. The sphere centre follows the player's NetworkTransform.
/// Each instance registers itself in <see cref="EchoShaderPublisher"/>, which feeds the
/// EchoReveal shader (up to 4 simultaneous spheres).
/// Offline (not spawned on the network) it behaves like the original single-player prototype.
/// </summary>
public class EchoVisionController : NetworkBehaviour, IVoiceLevelSource
{
    [Header("Push to talk")]
    [Tooltip("When on, the voice only lights the cave while the echo key (V) is held. Off = the old always-on echo.")]
    [SerializeField] private bool _pushToTalk = true;

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
    [Tooltip("How easy the echo is to trigger. The voice level is multiplied by this before it is mapped to a radius, so " +
             "every radius (whisper, talk, scream) needs the same fraction less noise: 1 = the original feel, 1.3 = about " +
             "23% less noise for any radius. Only the echo changes; the monster still hears the real loudness.")]
    [SerializeField, Range(0.5f, 3f)] private float _voiceSensitivity = 1.3f;

    [Header("Look & feel")]
    [Tooltip("Seconds for the light to grow (small = snappy).")]
    [SerializeField] private float _growSmoothTime = 0.05f;
    [Tooltip("Seconds for the light to fade out (big = lingers, scarier).")]
    [SerializeField] private float _shrinkSmoothTime = 0.6f;
    [Tooltip("Soft edge width as a fraction of the current radius.")]
    [SerializeField, Range(0.05f, 1f)] private float _falloffRatio = 0.5f;
    [SerializeField] private float _minFalloff = 0.4f;

    [Header("Networking")]
    [Tooltip("Seconds between radius updates sent over the network.")]
    [SerializeField] private float _syncSendRate = 0.05f;
    [Tooltip("Radius is rounded to this step (m) before syncing to avoid sending tiny changes.")]
    [SerializeField] private float _syncQuantization = 0.25f;
    [Tooltip("Seconds remote clients take to follow the synced radius (hides the send-rate steps).")]
    [SerializeField] private float _remoteSmoothTime = 0.08f;

    /// <summary>Radius (m) replicated from the owner to all observers.</summary>
    private readonly SyncVar<float> _syncedRadius = new SyncVar<float>();
    /// <summary>Whether the owner holds the echo key, replicated so the server weighs the noise and peers can show it.</summary>
    private readonly SyncVar<bool> _syncedEchoing = new SyncVar<bool>();
    private float _lastSentRadius = -1f;
    private bool _lastSentEchoing;
    private float _lastSentTime;
    private float _networkedLoudness;

    /// <summary>Smoothed, calibrated loudness for IVoiceLevelSource.</summary>
    public float Level01
    {
        get
        {
            if (IsLocalSource) return _source != null ? _source.Level01 : 0f;
            return _networkedLoudness;
        }
    }

    /// <summary>
    /// True while this player is echoing (holding the push-to-talk key, or always when push to talk is off).
    /// On the owner it reads the key; on every other peer it is the replicated value.
    /// </summary>
    public bool IsEchoing => IsLocalSource ? LocalEchoHeld : _syncedEchoing.Value;

    private bool LocalEchoHeld => !_pushToTalk || _player == null || _player.IsEchoHeld;

    /// <summary>Current radius of the revealed sphere (metres). Useful for gameplay/debug.</summary>
    public float CurrentRadius => _currentRadius;
    /// <summary>World-space centre of the sphere.</summary>
    public Vector3 Origin => transform.position + _originOffset;
    /// <summary>Soft edge width (m) for the current radius.</summary>
    public float CurrentFalloff => Mathf.Max(_minFalloff, _currentRadius * _falloffRatio * FalloffVisualScale);

    // Tighter than the serialized ratio: a wide, soft frontier washes the whole lit area in the edge colour.
    private const float FalloffVisualScale = 0.7f;

    private IVoiceLevelSource _source;
    private PlayerController _player;
    private float _currentRadius;
    private float _radiusVelocity;
    private float _forcedRadius = -1f;

    /// <summary>How easy the echo is to trigger (see the Voice Sensitivity tooltip). Meant for a settings menu.</summary>
    public float VoiceSensitivity
    {
        get => _voiceSensitivity;
        set => _voiceSensitivity = Mathf.Clamp(value, 0.5f, 3f);
    }

    /// <summary>
    /// The radius (m) the echo aims for at a given voice <paramref name="level"/> (0..1).
    /// The noise gate looks at the real level, so background noise is not amplified. Above the gate the level is multiplied by
    /// <paramref name="sensitivity"/> before the curve: the same radius is reached with 1 / sensitivity of the loudness, at every
    /// point of the curve (a 1.3 sensitivity needs about 23% less noise for a whisper, a normal voice and a scream alike).
    /// </summary>
    public static float TargetRadius(float level, float sensitivity, float noiseGate, float maxRadius, AnimationCurve curve)
    {
        if (level < noiseGate) return 0f;

        float shaped = Mathf.Clamp01(level * Mathf.Max(sensitivity, 0f));
        return maxRadius * curve.Evaluate(shaped);
    }

    /// <summary>
    /// Push-to-talk version of <see cref="TargetRadius(float, float, float, float, AnimationCurve)"/>: nothing is revealed
    /// unless <paramref name="echoHeld"/>, whatever the voice level.
    /// </summary>
    public static float TargetRadius(float level, float sensitivity, float noiseGate, float maxRadius, AnimationCurve curve, bool echoHeld)
    {
        return echoHeld ? TargetRadius(level, sensitivity, noiseGate, maxRadius, curve) : 0f;
    }

    /// <summary>
    /// Makes the local sphere aim for <paramref name="radius"/> metres whatever the microphone says. Used by the photo mode
    /// and demos; pass a negative value to hand control back to the voice.
    /// </summary>
    public void ForceRadius(float radius)
    {
        _forcedRadius = radius;
    }

    /// <summary>True when this instance drives its own radius from a local microphone.</summary>
    private bool IsLocalSource => !IsSpawned || IsOwner;

    private void Awake()
    {
        _source = _voiceSource as IVoiceLevelSource;
        if (_source == null) _source = GetComponent<IVoiceLevelSource>();
        _player = GetComponent<PlayerController>();

        if (_source == null)
            Debug.LogError("[EchoVisionController] No IVoiceLevelSource found. Add MicrophoneLoudness.", this);
    }

    private void OnEnable()
    {
        EchoShaderPublisher.Register(this);
    }

    private void OnDisable()
    {
        EchoShaderPublisher.Unregister(this);
        _currentRadius = 0f;
        _radiusVelocity = 0f;
    }

    public override void OnStartNetwork()
    {
        base.OnStartNetwork();
        _syncedRadius.UpdateSendRate(_syncSendRate);
        _syncedEchoing.UpdateSendRate(0f);
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        // Only the owner may capture a microphone. Remote avatars must not open the device
        // nor draw the debug loudness bar.
        if (!IsOwner && _source is Behaviour voiceBehaviour)
            voiceBehaviour.enabled = false;
    }

    public override void OnStopClient()
    {
        base.OnStopClient();
        _currentRadius = 0f;
        _radiusVelocity = 0f;
    }

    private void LateUpdate()
    {
        // LateUpdate so the origin uses the player's final position after movement this frame.
        if (IsLocalSource)
        {
            UpdateLocalRadius();
        }
        else
        {
            UpdateRemoteRadius();
            if (IsServerStarted)
            {
                // Smoothly decay networked loudness on server between RPC packets
                _networkedLoudness = Mathf.MoveTowards(_networkedLoudness, 0f, Time.deltaTime * 3f);
            }
        }
    }

    private void UpdateLocalRadius()
    {
        if (_source == null) return;

        float level = _source.Level01;
        bool echoing = LocalEchoHeld;
        float targetRadius = _forcedRadius >= 0f
            ? _forcedRadius
            : TargetRadius(level, _voiceSensitivity, _noiseGate, _maxRadius, _radiusCurve, echoing);

        float smoothTime = targetRadius > _currentRadius ? _growSmoothTime : _shrinkSmoothTime;
        _currentRadius = Mathf.SmoothDamp(_currentRadius, targetRadius, ref _radiusVelocity, smoothTime);
        if (_currentRadius < 0.01f) _currentRadius = 0f;

        if (IsSpawned)
        {
            float q = Quantize(_currentRadius);
            bool radiusChanged = Mathf.Abs(_lastSentRadius - q) > 0.05f;
            bool periodicLoudness = level > _noiseGate && Time.time - _lastSentTime > 0.15f;
            bool echoChanged = echoing != _lastSentEchoing;

            if (radiusChanged || periodicLoudness || echoChanged)
            {
                _lastSentRadius = q;
                _lastSentTime = Time.time;
                _lastSentEchoing = echoing;
                if (IsServerStarted)
                {
                    _syncedRadius.Value = q;
                    _syncedEchoing.Value = echoing;
                    _networkedLoudness = level;
                }
                else
                {
                    ServerSetRadius(q, level, echoing);
                }
            }
        }
    }

    [ServerRpc]
    private void ServerSetRadius(float radius, float loudness, bool echoing)
    {
        _syncedRadius.Value = radius;
        _syncedEchoing.Value = echoing;
        _networkedLoudness = loudness;
    }

    private void UpdateRemoteRadius()
    {
        float target = _syncedRadius.Value;
        _currentRadius = Mathf.SmoothDamp(_currentRadius, target, ref _radiusVelocity, _remoteSmoothTime);
        if (_currentRadius < 0.01f && target <= 0f) _currentRadius = 0f;
    }

    private float Quantize(float radius)
    {
        if (radius <= 0f || _syncQuantization <= 0f) return radius;
        return Mathf.Round(radius / _syncQuantization) * _syncQuantization;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.9f, 0.4f, 0.6f);
        Gizmos.DrawWireSphere(Origin, _currentRadius);
    }
}
