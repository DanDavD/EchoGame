using UnityEngine;

/// <summary>
/// The sound of the catacombs around the local listener: a low room tone that never stops, water drips placed in 3D
/// around you, and a distant rumble of rock now and then. It is created automatically when the game starts and follows
/// whichever <see cref="AudioListener"/> is active, so it needs no scene setup.
///
/// The menus have no camera and no listener (the player's camera only exists during a match), so the ambience carries
/// a listener of its own that switches on only while no other one is active. That makes the menus sound like the cave
/// and keeps Unity from warning about a missing listener every frame.
///
/// It is local and purely cosmetic: the monster and the team noise meter never hear it. It is kept quiet on purpose,
/// because a player without headphones feeds the speakers back into the microphone. The "Ambience and music" setting
/// scales it.
/// </summary>
public sealed class CaveAmbience : MonoBehaviour
{
    [SerializeField, Range(0f, 1f)] private float _roomToneVolume = 0.16f;
    [SerializeField, Range(0f, 1f)] private float _dripVolume = 0.4f;
    [SerializeField, Range(0f, 1f)] private float _rumbleVolume = 0.35f;
    [SerializeField] private Vector2 _dripEverySeconds = new Vector2(2.5f, 7f);
    [SerializeField] private Vector2 _rumbleEverySeconds = new Vector2(35f, 80f);
    [SerializeField] private Vector2 _dripDistance = new Vector2(4f, 14f);

    private const float ListenerSearchSeconds = 0.5f;

    private static CaveAmbience _instance;

    private AudioSource _roomTone;
    private AudioSource _drip;
    private AudioSource _rumble;
    private AudioListener _fallbackListener;
    private AudioListener _listener;
    private float _nextDrip;
    private float _nextRumble;
    private float _nextListenerSearch;
    private System.Random _random;

    /// <summary>The ambience of this game session (null before it starts).</summary>
    public static CaveAmbience Instance => _instance;

    /// <summary>
    /// Where the game is heard from: the ambience sits on the active listener (or is the listener itself in the menus).
    /// Used by <see cref="SoundOcclusion"/> to find the walls between a sound and the player.
    /// </summary>
    public static bool TryGetListenerPosition(out Vector3 position)
    {
        position = _instance != null ? _instance.transform.position : Vector3.zero;
        return _instance != null;
    }

    /// <summary>True while the ambience's own listener is the one hearing the game (no camera listener is active).</summary>
    public bool UsingFallbackListener => _fallbackListener != null && _fallbackListener.enabled;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => _instance = null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (_instance != null) return;
        var go = new GameObject("[CaveAmbience]");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<CaveAmbience>();
    }

    private void Awake()
    {
        _random = new System.Random(System.Environment.TickCount);

        // Off until we know nobody else is listening, so there are never two listeners at once.
        _fallbackListener = gameObject.AddComponent<AudioListener>();
        _fallbackListener.enabled = false;

        _roomTone = gameObject.AddComponent<AudioSource>();
        _roomTone.clip = ProceduralAudio.RoomTone();
        _roomTone.loop = true;
        _roomTone.spatialBlend = 0f; // everywhere at once: it is the air of the cave
        _roomTone.volume = _roomToneVolume * AmbienceSetting;
        _roomTone.Play();

        _drip = ProceduralAudio.CreateSource(transform, "Drip", maxDistance: 25f, volume: _dripVolume);

        _rumble = gameObject.AddComponent<AudioSource>();
        _rumble.playOnAwake = false;
        _rumble.spatialBlend = 0f;
        _rumble.clip = ProceduralAudio.Rumble();

        _nextDrip = Time.time + Range(_dripEverySeconds);
        _nextRumble = Time.time + Range(_rumbleEverySeconds);
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    private static float AmbienceSetting => EchoProtocol.Settings.SettingsManager.Current.AmbienceVolume;

    private void Update()
    {
        float ambience = AmbienceSetting;
        _roomTone.volume = _roomToneVolume * ambience;

        if (Time.time >= _nextDrip)
        {
            _nextDrip = Time.time + Range(_dripEverySeconds);
            PlayDrip(ambience);
        }

        if (Time.time >= _nextRumble)
        {
            _nextRumble = Time.time + Range(_rumbleEverySeconds);
            _rumble.pitch = 0.85f + (float)_random.NextDouble() * 0.3f;
            _rumble.PlayOneShot(_rumble.clip, _rumbleVolume * ambience);
        }
    }

    // LateUpdate: after this frame's spawns and before Unity mixes the audio, so the fallback listener and a camera
    // listener are never on together.
    private void LateUpdate()
    {
        bool tracking = _listener != null && _listener.isActiveAndEnabled;
        if (!tracking && (UsingFallbackListener || Time.unscaledTime >= _nextListenerSearch))
        {
            // While the fallback is on, look every frame: a player's camera may appear at any moment.
            _nextListenerSearch = Time.unscaledTime + ListenerSearchSeconds;
            _listener = FindOtherListener();
            tracking = _listener != null;
        }

        _fallbackListener.enabled = !tracking;
        if (tracking) transform.position = _listener.transform.position;
    }

    private AudioListener FindOtherListener()
    {
        foreach (AudioListener candidate in FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude))
        {
            if (candidate != _fallbackListener && candidate.isActiveAndEnabled) return candidate;
        }
        return null;
    }

    private void PlayDrip(float ambience)
    {
        float angle = (float)_random.NextDouble() * Mathf.PI * 2f;
        float distance = Range(_dripDistance);
        float height = 1f + (float)_random.NextDouble() * 3f;
        _drip.transform.position = transform.position + new Vector3(Mathf.Cos(angle) * distance, height, Mathf.Sin(angle) * distance);
        _drip.pitch = 0.9f + (float)_random.NextDouble() * 0.25f;
        _drip.PlayOneShot(ProceduralAudio.Drip(_random.Next(4)), (0.7f + (float)_random.NextDouble() * 0.3f) * ambience);
    }

    private float Range(Vector2 range) => range.x + (float)_random.NextDouble() * Mathf.Max(0f, range.y - range.x);
}
