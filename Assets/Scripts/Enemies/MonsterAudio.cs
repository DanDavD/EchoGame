using UnityEngine;

/// <summary>
/// What the blind monster sounds like: a breath that gets faster and louder as it moves, heavy steps, a low growl every
/// few seconds and a roar when it starts to hunt. The sounds carry far (you can hear it coming from the next corridors)
/// and come out muffled when walls stand in between (<see cref="SoundOcclusion"/>), so players can track it by ear.
///
/// Like <see cref="MonsterVisual"/>, it is driven only by how fast the monster moves, so every network peer hears the
/// same thing (clients only receive the movement). It is purely cosmetic: it adds no noise source.
/// </summary>
[DisallowMultipleComponent]
public class MonsterAudio : MonoBehaviour
{
    [SerializeField, Range(0f, 1f)] private float _breathVolume = 0.6f;
    [SerializeField, Range(0f, 1f)] private float _stepVolume = 0.75f;
    [SerializeField, Range(0f, 1f)] private float _growlVolume = 0.8f;
    [SerializeField, Range(0f, 1f)] private float _roarVolume = 1f;
    [Tooltip("Metres covered by one step, used to space the footsteps.")]
    [SerializeField] private float _strideMetres = 1.6f;
    [Tooltip("Beyond this distance (m) the monster is silent.")]
    [SerializeField] private float _hearDistance = 60f;
    [Tooltip("Seconds between two growls, at random in this range (shorter while it hunts).")]
    [SerializeField] private Vector2 _growlEverySeconds = new Vector2(5f, 10f);

    private AudioSource _breath;
    private AudioSource _steps;
    private AudioSource _voice;
    private readonly MovementSounds.RoarTrigger _roar = new MovementSounds.RoarTrigger();

    private Vector3 _lastPosition;
    private float _speed;
    private float _stepTimer;
    private float _growlTimer;

    /// <summary>The smoothed horizontal speed (m/s) the sounds follow.</summary>
    public float Speed => _speed;

    /// <summary>How many growls it has let out (for tests and debugging).</summary>
    public int Growls { get; private set; }

    private void Awake()
    {
        // A large min distance keeps the monster audible from far away (the logarithmic rolloff starts there).
        _breath = CreateSource("MonsterBreath", 4f);
        _breath.clip = ProceduralAudio.MonsterBreath();
        _breath.loop = true;
        _breath.volume = 0f;
        _breath.Play();
        _breath.time = Random.value * (_breath.clip.length - 0.05f); // two monsters never breathe in sync

        _steps = CreateSource("MonsterSteps", 5f);
        _voice = CreateSource("MonsterVoice", 7f);

        _lastPosition = transform.position;
        _growlTimer = Range(_growlEverySeconds);
    }

    private AudioSource CreateSource(string name, float minDistance)
    {
        AudioSource source = ProceduralAudio.CreateSource(transform, name, _hearDistance, 1f, minDistance);
        source.gameObject.AddComponent<SoundOcclusion>();
        return source;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        Vector3 delta = transform.position - _lastPosition;
        delta.y = 0f;
        _lastPosition = transform.position;
        float instant = delta.magnitude / dt;
        if (instant > 20f) instant = _speed; // a teleport (spawn, warp, restart) is not a sprint
        _speed = Mathf.Lerp(_speed, instant, 1f - Mathf.Exp(-6f * dt));

        // Breath: slow and quiet while it wanders, fast and heavy while it hunts.
        float effort = Mathf.Clamp01(_speed / MovementSounds.RoarTrigger.HuntSpeed);
        _breath.volume = _breathVolume * Mathf.Lerp(0.55f, 1f, effort);
        _breath.pitch = Mathf.Lerp(0.85f, 1.35f, effort);

        if (_roar.Update(_speed, dt))
        {
            _voice.pitch = 0.92f + Random.value * 0.16f;
            _voice.PlayOneShot(ProceduralAudio.MonsterRoar(), _roarVolume);
            _growlTimer = Range(_growlEverySeconds); // no growl right on top of the roar
        }

        // Growls: a regular reminder of where it is, more often while it hunts.
        _growlTimer -= dt * Mathf.Lerp(1f, 2f, effort);
        if (_growlTimer <= 0f)
        {
            _growlTimer = Range(_growlEverySeconds);
            _voice.pitch = 0.85f + Random.value * 0.25f;
            _voice.PlayOneShot(ProceduralAudio.MonsterGrowl(), _growlVolume * Mathf.Lerp(0.75f, 1f, effort));
            Growls++;
        }

        float interval = MovementSounds.StepInterval(_speed, _strideMetres);
        if (interval <= 0f)
        {
            _stepTimer = 0f;
            return;
        }

        _stepTimer -= dt;
        if (_stepTimer <= 0f)
        {
            _stepTimer = interval;
            _steps.pitch = 0.9f + Random.value * 0.2f;
            _steps.PlayOneShot(ProceduralAudio.MonsterStep(), _stepVolume * Mathf.Lerp(0.6f, 1f, effort));
        }
    }

    private static float Range(Vector2 range) => range.x + Random.value * Mathf.Max(0f, range.y - range.x);
}
